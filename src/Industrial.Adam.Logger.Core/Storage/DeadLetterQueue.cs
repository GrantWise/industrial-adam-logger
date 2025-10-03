using System.Collections.Concurrent;
using System.Text.Json;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Core.Storage;

/// <summary>
/// Handles storage and retrieval of failed database write batches to prevent data loss
/// </summary>
public sealed class DeadLetterQueue : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _deadLetterPath;
    private readonly ConcurrentQueue<FailedBatch> _inMemoryQueue = new();
    private readonly Timer _persistenceTimer;
    private readonly SemaphoreSlim _persistenceLock = new(1, 1);
    private volatile bool _disposed;

    private const int MaxInMemoryItems = 1000;
    private const int PersistenceIntervalMs = 30000; // 30 seconds

    /// <summary>
    /// Initialize the dead letter queue
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="deadLetterPath">Directory path for storing failed batches</param>
    public DeadLetterQueue(ILogger logger, string? deadLetterPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _deadLetterPath = !string.IsNullOrWhiteSpace(deadLetterPath)
            ? deadLetterPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AdamLogger", "DeadLetterQueue");

        Directory.CreateDirectory(_deadLetterPath);

        // Start periodic persistence timer
        _persistenceTimer = new Timer(PersistPendingBatches, null,
            TimeSpan.FromMilliseconds(PersistenceIntervalMs),
            TimeSpan.FromMilliseconds(PersistenceIntervalMs));

        _logger.LogInformation("Dead letter queue initialized at {Path}", _deadLetterPath);
    }

    /// <summary>
    /// Add a failed batch to the dead letter queue
    /// </summary>
    /// <param name="readings">Failed batch of readings</param>
    /// <param name="error">Error that caused the failure</param>
    /// <param name="retryAttempts">Number of retry attempts made</param>
    public void AddFailedBatch(IEnumerable<DeviceReading> readings, Exception error, int retryAttempts = 0)
    {
        if (_disposed)
            return;

        var failedBatch = new FailedBatch
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            Readings = readings.ToList(),
            Error = error.Message,
            RetryAttempts = retryAttempts
        };

        _inMemoryQueue.Enqueue(failedBatch);

        _logger.LogWarning(
            "Added failed batch {BatchId} with {Count} readings to dead letter queue. Error: {Error}",
            failedBatch.Id, failedBatch.Readings.Count, error.Message);

        // If in-memory queue is getting large, persist immediately
        if (_inMemoryQueue.Count > MaxInMemoryItems)
        {
            _ = Task.Run(PersistPendingBatchesAsync);
        }
    }

    /// <summary>
    /// Get all failed batches for retry processing
    /// </summary>
    /// <returns>Collection of failed batches</returns>
    public async Task<List<FailedBatch>> GetFailedBatchesAsync()
    {
        if (_disposed)
        {
            return new List<FailedBatch>();
        }

        var failedBatches = new List<FailedBatch>();

        // Get in-memory batches
        while (_inMemoryQueue.TryDequeue(out var batch))
        {
            failedBatches.Add(batch);
        }

        // Get persisted batches
        await _persistenceLock.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_deadLetterPath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var batch = JsonSerializer.Deserialize<FailedBatch>(json);
                    if (batch != null)
                    {
                        failedBatches.Add(batch);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading dead letter file {File}", file);
                }
            }
        }
        finally
        {
            _persistenceLock.Release();
        }

        _logger.LogInformation("Retrieved {Count} failed batches from dead letter queue", failedBatches.Count);
        return failedBatches;
    }

    /// <summary>
    /// Mark a failed batch as successfully processed and remove it
    /// </summary>
    /// <param name="batchId">ID of the batch to remove</param>
    public async Task MarkBatchProcessedAsync(Guid batchId)
    {
        if (_disposed)
        {
            return;
        }

        await _persistenceLock.WaitAsync();
        try
        {
            var fileName = Path.Combine(_deadLetterPath, $"{batchId}.json");
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
                _logger.LogDebug("Removed processed batch {BatchId} from dead letter storage", batchId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing processed batch {BatchId} from dead letter storage", batchId);
        }
        finally
        {
            _persistenceLock.Release();
        }
    }

    /// <summary>
    /// Get current size of the dead letter queue
    /// </summary>
    /// <returns>Number of failed batches in queue</returns>
    public async Task<int> GetQueueSizeAsync()
    {
        if (_disposed)
        {
            return 0;
        }

        var inMemoryCount = _inMemoryQueue.Count;

        await _persistenceLock.WaitAsync();
        try
        {
            var fileCount = Directory.GetFiles(_deadLetterPath, "*.json").Length;
            return inMemoryCount + fileCount;
        }
        finally
        {
            _persistenceLock.Release();
        }
    }

    /// <summary>
    /// Clear all failed batches (use with caution!)
    /// </summary>
    public async Task ClearAllAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Clear in-memory queue
        while (_inMemoryQueue.TryDequeue(out _))
        { }

        // Clear persisted files
        await _persistenceLock.WaitAsync();
        try
        {
            var files = Directory.GetFiles(_deadLetterPath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting dead letter file {File}", file);
                }
            }
        }
        finally
        {
            _persistenceLock.Release();
        }

        _logger.LogWarning("Dead letter queue cleared");
    }

    /// <summary>
    /// Timer callback to persist pending batches
    /// </summary>
    private void PersistPendingBatches(object? state)
    {
        if (_disposed)
            return;
        _ = Task.Run(PersistPendingBatchesAsync);
    }

    /// <summary>
    /// Persist in-memory batches to disk
    /// </summary>
    private async Task PersistPendingBatchesAsync()
    {
        if (_disposed)
            return;

        var batchesToPersist = new List<FailedBatch>();

        // Dequeue up to a reasonable batch size
        for (int i = 0; i < 100 && _inMemoryQueue.TryDequeue(out var batch); i++)
        {
            batchesToPersist.Add(batch);
        }

        if (batchesToPersist.Count == 0)
            return;

        await _persistenceLock.WaitAsync();
        try
        {
            foreach (var batch in batchesToPersist)
            {
                var fileName = Path.Combine(_deadLetterPath, $"{batch.Id}.json");
                var json = JsonSerializer.Serialize(batch, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(fileName, json);
            }

            _logger.LogDebug("Persisted {Count} failed batches to dead letter storage", batchesToPersist.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting failed batches to dead letter storage");

            // Re-queue the batches if persistence failed
            foreach (var batch in batchesToPersist)
            {
                _inMemoryQueue.Enqueue(batch);
            }
        }
        finally
        {
            _persistenceLock.Release();
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _persistenceTimer?.Dispose();

        // Final persistence of remaining batches
        try
        {
            PersistPendingBatchesAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during final persistence on dispose");
        }

        _persistenceLock?.Dispose();

        _logger.LogInformation("Dead letter queue disposed");
    }
}

/// <summary>
/// Represents a failed batch of device readings
/// </summary>
public class FailedBatch
{
    /// <summary>
    /// Unique identifier for this failed batch
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Timestamp when the batch failed
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Device readings that failed to write
    /// </summary>
    public List<DeviceReading> Readings { get; set; } = new();

    /// <summary>
    /// Error message describing the failure
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Number of retry attempts made for this batch
    /// </summary>
    public int RetryAttempts { get; set; }

    /// <summary>
    /// Whether this batch should be retried
    /// </summary>
    public bool ShouldRetry => RetryAttempts < 3 &&
                               DateTimeOffset.UtcNow - Timestamp < TimeSpan.FromHours(24);
}
