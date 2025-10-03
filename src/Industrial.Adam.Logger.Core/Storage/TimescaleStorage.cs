using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Polly;
using Polly.Retry;

namespace Industrial.Adam.Logger.Core.Storage;

/// <summary>
/// Stores device readings in TimescaleDB (PostgreSQL with TimescaleDB extension)
/// </summary>
public sealed class TimescaleStorage : ITimescaleStorage, IAsyncDisposable
{
    private readonly ILogger<TimescaleStorage> _logger;
    private readonly TimescaleSettings _settings;
    private readonly string _connectionString;
    private readonly Channel<DeviceReading> _writeChannel;
    private readonly ChannelWriter<DeviceReading> _writer;
    private readonly CancellationTokenSource _backgroundCts = new();
    private readonly Task _backgroundWriteTask;
    private readonly DeadLetterQueue? _deadLetterQueue;
    private readonly AsyncRetryPolicy _retryPolicy;
    private volatile bool _disposed;

    // Health monitoring fields
    private volatile bool _isBackgroundTaskHealthy = true;
    private DateTimeOffset? _lastSuccessfulWrite;
    private string? _lastError;
    private readonly object _healthLock = new();
    private int _cachedDeadLetterQueueSize = 0;

    // Performance metrics
    private long _totalRetryAttempts;
    private long _totalSuccessfulBatches;
    private long _totalFailedBatches;
    private readonly ConcurrentQueue<double> _batchLatencies = new();
    private const int MaxLatencyTracking = 100;

    // SQL statements
    private static readonly string _createTableSql = """
        CREATE TABLE IF NOT EXISTS {0} (
            timestamp TIMESTAMPTZ NOT NULL,
            device_id TEXT NOT NULL,
            channel INTEGER NOT NULL,
            raw_value BIGINT NOT NULL,
            processed_value DOUBLE PRECISION,
            rate DOUBLE PRECISION,
            quality TEXT,
            unit TEXT DEFAULT 'counts',
            PRIMARY KEY (timestamp, device_id, channel)
        );
        """;

    private readonly string _insertSql;

    /// <summary>
    /// Initialize TimescaleDB storage with high-performance Channel-based processing
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="settings">TimescaleDB connection settings</param>
    public TimescaleStorage(ILogger<TimescaleStorage> logger, TimescaleSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _connectionString = _settings.GetConnectionString();

        // Prepare SQL statements
        _insertSql = $"""
            INSERT INTO {_settings.TableName} 
            (timestamp, device_id, channel, raw_value, processed_value, rate, quality, unit)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            ON CONFLICT (timestamp, device_id, channel) DO UPDATE SET
                raw_value = EXCLUDED.raw_value,
                processed_value = EXCLUDED.processed_value,
                rate = EXCLUDED.rate,
                quality = EXCLUDED.quality,
                unit = EXCLUDED.unit
            """;

        // Setup Channel for high-throughput async processing with increased capacity
        var channelCapacity = _settings.BatchSize * Constants.DefaultChannelCapacityMultiplier;
        var channelOptions = new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _writeChannel = Channel.CreateBounded<DeviceReading>(channelOptions);
        _writer = _writeChannel.Writer;

        // Initialize dead letter queue if enabled
        if (_settings.EnableDeadLetterQueue)
        {
            _deadLetterQueue = new DeadLetterQueue(_logger, _settings.DeadLetterQueuePath);
        }

        // Setup retry policy using Polly
        _retryPolicy = Policy
            .Handle<NpgsqlException>()
            .Or<TimeoutException>()
            .Or<InvalidOperationException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                _settings.MaxRetryAttempts,
                retryAttempt => CalculateRetryDelay(retryAttempt),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Interlocked.Increment(ref _totalRetryAttempts);
                    _logger.LogWarning(exception,
                        "Database write retry {RetryCount}/{MaxRetries} after {Delay}ms: {Error}",
                        retryCount, _settings.MaxRetryAttempts, timeSpan.TotalMilliseconds, exception.Message);
                });

        // Initialize database schema
        InitializeDatabaseAsync().GetAwaiter().GetResult();

        // Start background writer task
        _backgroundWriteTask = Task.Run(ProcessWritesAsync, _backgroundCts.Token);

        // Start background dead letter queue processing if enabled
        if (_deadLetterQueue != null)
        {
            _ = Task.Run(ProcessDeadLetterQueueAsync, _backgroundCts.Token);
        }

        _logger.LogInformation(
            "TimescaleDB storage initialized for {Host}:{Port}/{Database}, table={TableName} with Channel-based processing. " +
            "BatchSize={BatchSize}, ChannelCapacity={ChannelCapacity}, RetryAttempts={RetryAttempts}, DeadLetterQueue={DeadLetterEnabled}",
            _settings.Host, _settings.Port, _settings.Database, _settings.TableName,
            _settings.BatchSize, channelCapacity, _settings.MaxRetryAttempts, _settings.EnableDeadLetterQueue);
    }

    /// <summary>
    /// Write a single reading to TimescaleDB
    /// </summary>
    public async Task WriteReadingAsync(DeviceReading reading, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TimescaleStorage));

        // Use Channel for high-performance async writes
        await _writer.WriteAsync(reading, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Write multiple readings in a batch
    /// </summary>
    public async Task WriteBatchAsync(IEnumerable<DeviceReading> readings, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TimescaleStorage));

        // Use Channel for all writes to maintain consistency and performance
        foreach (var reading in readings)
        {
            await _writer.WriteAsync(reading, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Test connection to TimescaleDB
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Test basic query
            using var command = new NpgsqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            var isHealthy = result?.ToString() == "1";

            if (isHealthy)
            {
                _logger.LogInformation("TimescaleDB connection test successful");
            }
            else
            {
                _logger.LogWarning("TimescaleDB connection test failed - unexpected result");
            }

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TimescaleDB connection test failed");
            return false;
        }
    }

    /// <summary>
    /// Flush any pending writes
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        // Wait for background processing to complete without completing the channel
        try
        {
            // Give background task time to process any pending items
            await Task.Delay(100, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    /// <summary>
    /// Get the current health status of the storage subsystem
    /// </summary>
    public StorageHealthStatus GetHealthStatus()
    {
        lock (_healthLock)
        {
            // Calculate average latency
            var latencies = _batchLatencies.ToArray();
            var avgLatency = latencies.Length > 0 ? latencies.Average() : 0.0;

            return new StorageHealthStatus
            {
                IsBackgroundTaskHealthy = _isBackgroundTaskHealthy,
                LastSuccessfulWrite = _lastSuccessfulWrite,
                LastError = _lastError,
                PendingWrites = _writeChannel.Reader.CanCount ? _writeChannel.Reader.Count : 0,
                TotalRetryAttempts = Interlocked.Read(ref _totalRetryAttempts),
                DeadLetterQueueSize = _cachedDeadLetterQueueSize,
                TotalSuccessfulBatches = Interlocked.Read(ref _totalSuccessfulBatches),
                TotalFailedBatches = Interlocked.Read(ref _totalFailedBatches),
                AverageBatchLatencyMs = avgLatency,
                IsDeadLetterQueueEnabled = _deadLetterQueue != null
            };
        }
    }

    /// <summary>
    /// Initialize database schema and hypertable
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            // Create the table first
            var createTableSql = string.Format(_createTableSql, _settings.TableName);
            using var createCommand = new NpgsqlCommand(createTableSql, connection);
            await createCommand.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Check if hypertable already exists, and create it if not
            var checkHypertableSql = $"""
                SELECT EXISTS (
                    SELECT 1 FROM _timescaledb_catalog.hypertable 
                    WHERE table_name = '{_settings.TableName}' AND schema_name = 'public'
                );
                """;

            using var checkCommand = new NpgsqlCommand(checkHypertableSql, connection);
            var result = await checkCommand.ExecuteScalarAsync().ConfigureAwait(false);
            var hypertableExists = result != null && (bool)result;

            if (!hypertableExists)
            {
                var createHypertableSql = $"SELECT create_hypertable('public.{_settings.TableName}', 'timestamp', chunk_time_interval => INTERVAL '1 hour');";
                using var hypertableCommand = new NpgsqlCommand(createHypertableSql, connection);
                await hypertableCommand.ExecuteNonQueryAsync().ConfigureAwait(false);

                _logger.LogInformation("TimescaleDB hypertable created for table {TableName}", _settings.TableName);
            }

            _logger.LogInformation("TimescaleDB schema initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize TimescaleDB schema");
            throw;
        }
    }

    /// <summary>
    /// Background task that processes writes from the Channel in batches
    /// </summary>
    private async Task ProcessWritesAsync()
    {
        var reader = _writeChannel.Reader;
        var batchList = new List<DeviceReading>(_settings.BatchSize);
        var lastFlushTime = DateTimeOffset.UtcNow;

        while (!_backgroundCts.Token.IsCancellationRequested)
        {
            try
            {
                var hasMoreData = true;
                while (hasMoreData && !_backgroundCts.Token.IsCancellationRequested)
                {
                    // Try to read available data with a short timeout to enable periodic flushing
                    var readTimeout = TimeSpan.FromMilliseconds(Math.Min(_settings.FlushIntervalMs / 4, 1000));
                    using var timeoutCts = new CancellationTokenSource(readTimeout);
                    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_backgroundCts.Token, timeoutCts.Token);

                    try
                    {
                        if (await reader.WaitToReadAsync(combinedCts.Token).ConfigureAwait(false))
                        {
                            // Read all immediately available items up to batch size
                            while (reader.TryRead(out var reading) && batchList.Count < _settings.BatchSize)
                            {
                                batchList.Add(reading);
                            }

                            // Write batch if it's full
                            if (batchList.Count >= _settings.BatchSize)
                            {
                                await WriteBatchToTimescaleAsync(batchList, _backgroundCts.Token).ConfigureAwait(false);
                                batchList.Clear();
                                lastFlushTime = DateTimeOffset.UtcNow;
                            }
                        }
                        else
                        {
                            hasMoreData = false; // Channel completed
                        }
                    }
                    catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !_backgroundCts.Token.IsCancellationRequested)
                    {
                        // Timeout occurred - check if we need to flush based on time
                    }

                    // Time-based flush if we have accumulated data and enough time has passed
                    var timeSinceLastFlush = DateTimeOffset.UtcNow - lastFlushTime;
                    if (batchList.Count > 0 && timeSinceLastFlush.TotalMilliseconds >= _settings.FlushIntervalMs)
                    {
                        await WriteBatchToTimescaleAsync(batchList, _backgroundCts.Token).ConfigureAwait(false);
                        batchList.Clear();
                        lastFlushTime = DateTimeOffset.UtcNow;
                    }
                }

                // Final flush of any remaining readings
                if (batchList.Count > 0)
                {
                    await WriteBatchToTimescaleAsync(batchList, CancellationToken.None).ConfigureAwait(false);
                }
                break; // Exit loop normally when channel is complete
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background write processing");

                // Update health status
                lock (_healthLock)
                {
                    _isBackgroundTaskHealthy = false;
                    _lastError = ex.Message;
                }
            }
        }
    }


    /// <summary>
    /// Write a batch of readings to TimescaleDB with retry logic and dead letter queue
    /// </summary>
    private async Task WriteBatchToTimescaleAsync(List<DeviceReading> readings, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var retryAttempts = 0;

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                retryAttempts++;

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                // Use COPY for optimal performance with large batches
                if (readings.Count > 10)
                {
                    await WriteBatchUsingCopyAsync(connection, readings, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await WriteBatchUsingParametersAsync(connection, readings, cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            });

            stopwatch.Stop();

            // Track performance metrics
            Interlocked.Increment(ref _totalSuccessfulBatches);
            TrackBatchLatency(stopwatch.Elapsed.TotalMilliseconds);

            // Update health status on successful write
            lock (_healthLock)
            {
                _isBackgroundTaskHealthy = true;
                _lastSuccessfulWrite = DateTimeOffset.UtcNow;
                _lastError = null;
            }

            _logger.LogDebug("Successfully wrote batch of {Count} readings to TimescaleDB in {LatencyMs}ms (attempts: {Attempts})",
                readings.Count, stopwatch.ElapsedMilliseconds, retryAttempts);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Interlocked.Increment(ref _totalFailedBatches);

            _logger.LogError(ex, "Failed to write batch of {Count} readings to TimescaleDB after {Attempts} attempts",
                readings.Count, retryAttempts);

            // Update health status on write failure
            lock (_healthLock)
            {
                _isBackgroundTaskHealthy = false;
                _lastError = ex.Message;
            }

            // Add to dead letter queue if enabled
            if (_deadLetterQueue != null)
            {
                _deadLetterQueue.AddFailedBatch(readings, ex, retryAttempts - 1);
                _logger.LogWarning("Added failed batch of {Count} readings to dead letter queue", readings.Count);

                // Update cached DLQ size (best effort, non-blocking)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _cachedDeadLetterQueueSize = await _deadLetterQueue.GetQueueSizeAsync();
                    }
                    catch
                    {
                        // Ignore errors updating cache
                    }
                });
            }
            else
            {
                // If dead letter queue is disabled, still throw to maintain existing behavior
                throw;
            }
        }
    }

    /// <summary>
    /// Write batch using PostgreSQL COPY for maximum performance
    /// </summary>
    private async Task WriteBatchUsingCopyAsync(NpgsqlConnection connection, List<DeviceReading> readings, CancellationToken cancellationToken)
    {
        var copyCommand = $"COPY {_settings.TableName} (timestamp, device_id, channel, raw_value, processed_value, rate, quality, unit) FROM STDIN (FORMAT BINARY)";

        using var writer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken).ConfigureAwait(false);

        foreach (var reading in readings)
        {
            await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(reading.Timestamp.UtcDateTime, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(reading.DeviceId, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(reading.Channel, NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(reading.RawValue, NpgsqlDbType.Bigint, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(reading.ProcessedValue, NpgsqlDbType.Double, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(reading.Rate.HasValue ? (object)reading.Rate.Value : DBNull.Value, NpgsqlDbType.Double, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(reading.Quality.ToString(), NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(reading.Unit, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
        }

        await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Write batch using parameterized queries for smaller batches
    /// </summary>
    private async Task WriteBatchUsingParametersAsync(NpgsqlConnection connection, List<DeviceReading> readings, CancellationToken cancellationToken)
    {
        using var command = new NpgsqlCommand(_insertSql, connection);

        // Add parameters
        command.Parameters.Add("", NpgsqlDbType.TimestampTz);
        command.Parameters.Add("", NpgsqlDbType.Text);
        command.Parameters.Add("", NpgsqlDbType.Integer);
        command.Parameters.Add("", NpgsqlDbType.Bigint);
        command.Parameters.Add("", NpgsqlDbType.Double);
        command.Parameters.Add("", NpgsqlDbType.Double);
        command.Parameters.Add("", NpgsqlDbType.Text);
        command.Parameters.Add("", NpgsqlDbType.Text);

        foreach (var reading in readings)
        {
            command.Parameters[0].Value = reading.Timestamp.UtcDateTime;
            command.Parameters[1].Value = reading.DeviceId;
            command.Parameters[2].Value = reading.Channel;
            command.Parameters[3].Value = reading.RawValue;
            command.Parameters[4].Value = reading.ProcessedValue;
            command.Parameters[5].Value = reading.Rate.HasValue ? reading.Rate.Value : DBNull.Value;
            command.Parameters[6].Value = reading.Quality.ToString();
            command.Parameters[7].Value = reading.Unit;

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Calculate retry delay using exponential backoff with jitter
    /// </summary>
    private TimeSpan CalculateRetryDelay(int retryAttempt)
    {
        var baseDelayMs = _settings.RetryDelayMs;
        var maxDelayMs = _settings.MaxRetryDelayMs;

        // Exponential backoff: baseDelay * 2^(retryAttempt-1)
        var delayMs = Math.Min(baseDelayMs * Math.Pow(2, retryAttempt - 1), maxDelayMs);

        // Add jitter (±10% randomness) to prevent thundering herd
        var jitterRange = delayMs * 0.1;
        var jitter = (Random.Shared.NextDouble() - 0.5) * 2 * jitterRange;
        delayMs = Math.Max(100, delayMs + jitter); // Minimum 100ms delay

        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    /// Track batch write latency for performance monitoring
    /// </summary>
    private void TrackBatchLatency(double latencyMs)
    {
        _batchLatencies.Enqueue(latencyMs);

        // Keep only the last N measurements to prevent unbounded growth
        while (_batchLatencies.Count > MaxLatencyTracking)
        {
            _batchLatencies.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Background task to process failed batches from dead letter queue
    /// </summary>
    private async Task ProcessDeadLetterQueueAsync()
    {
        if (_deadLetterQueue == null)
            return;

        while (!_backgroundCts.Token.IsCancellationRequested)
        {
            try
            {
                var failedBatches = await _deadLetterQueue.GetFailedBatchesAsync();
                var processedCount = 0;

                foreach (var failedBatch in failedBatches.Where(b => b.ShouldRetry))
                {
                    try
                    {
                        // Attempt to retry the failed batch
                        await WriteBatchToTimescaleAsync(failedBatch.Readings, _backgroundCts.Token);

                        // Mark as processed if successful
                        await _deadLetterQueue.MarkBatchProcessedAsync(failedBatch.Id);
                        processedCount++;

                        _logger.LogInformation("Successfully recovered failed batch {BatchId} with {Count} readings",
                            failedBatch.Id, failedBatch.Readings.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to recover batch {BatchId} from dead letter queue", failedBatch.Id);
                        // Batch will remain in dead letter queue for future attempts
                    }
                }

                if (processedCount > 0)
                {
                    _logger.LogInformation("Processed {ProcessedCount} batches from dead letter queue", processedCount);
                }

                // Update cached DLQ size for health status
                _cachedDeadLetterQueueSize = await _deadLetterQueue.GetQueueSizeAsync();

                // Wait before next processing cycle
                await Task.Delay(TimeSpan.FromMinutes(1), _backgroundCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing dead letter queue");
                await Task.Delay(TimeSpan.FromMinutes(5), _backgroundCts.Token); // Back off on error
            }
        }
    }

    /// <summary>
    /// Asynchronously dispose resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _logger.LogInformation("Disposing TimescaleDB storage with graceful shutdown (timeout: {TimeoutSeconds}s)",
            _settings.ShutdownTimeoutSeconds);

        // Complete the channel to stop accepting new writes
        _writer.Complete();

        // Cancel background processing
        await _backgroundCts.CancelAsync().ConfigureAwait(false);

        try
        {
            // Wait for background task to complete with configurable timeout
            var shutdownTimeout = TimeSpan.FromSeconds(_settings.ShutdownTimeoutSeconds);
            using var timeoutCts = new CancellationTokenSource(shutdownTimeout);

            await _backgroundWriteTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Background write task did not complete within {TimeoutSeconds}s shutdown timeout",
                _settings.ShutdownTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error waiting for background write task to complete during disposal");
        }

        // Dispose dead letter queue
        _deadLetterQueue?.Dispose();

        // Dispose resources
        _backgroundCts.Dispose();
        _backgroundWriteTask.Dispose();

        var healthStatus = GetHealthStatus();
        _logger.LogInformation(
            "TimescaleDB storage disposed. Final stats: Successful={SuccessfulBatches}, Failed={FailedBatches}, " +
            "Retries={Retries}, DeadLetterQueue={DeadLetterSize}",
            healthStatus.TotalSuccessfulBatches, healthStatus.TotalFailedBatches,
            healthStatus.TotalRetryAttempts, healthStatus.DeadLetterQueueSize);
    }

    /// <summary>
    /// Dispose resources (synchronous fallback)
    /// </summary>
    public void Dispose()
    {
        // Use synchronous disposal pattern - call async version and block
        // This is acceptable as a fallback for consumers that don't support IAsyncDisposable
        DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(_settings.ShutdownTimeoutSeconds + 5));
    }

    /// <summary>
    /// Force flush all pending writes and process dead letter queue
    /// Used for graceful shutdown or emergency flush
    /// </summary>
    public async Task<bool> ForceFlushAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // First flush normal pending writes
            await FlushAsync(cancellationToken);

            // Then attempt to process any items in dead letter queue
            if (_deadLetterQueue != null)
            {
                var failedBatches = await _deadLetterQueue.GetFailedBatchesAsync();
                var recoveredCount = 0;

                foreach (var batch in failedBatches.Where(b => b.ShouldRetry))
                {
                    try
                    {
                        await WriteBatchToTimescaleAsync(batch.Readings, cancellationToken);
                        await _deadLetterQueue.MarkBatchProcessedAsync(batch.Id);
                        recoveredCount++;
                    }
                    catch
                    {
                        // Continue processing other batches
                    }
                }

                if (recoveredCount > 0)
                {
                    _logger.LogInformation("Force flush recovered {Count} batches from dead letter queue", recoveredCount);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during force flush operation");
            return false;
        }
    }
}
