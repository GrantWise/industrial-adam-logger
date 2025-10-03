using System.Collections.Concurrent;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Devices;
using Industrial.Adam.Logger.Core.Models;
using Industrial.Adam.Logger.Core.Processing;
using Industrial.Adam.Logger.Core.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Industrial.Adam.Logger.Core.Services;

/// <summary>
/// Main service that orchestrates ADAM device monitoring and data logging
/// </summary>
public sealed class AdamLoggerService : IHostedService, IDisposable
{
    private readonly ILogger<AdamLoggerService> _logger;
    private readonly IOptions<LoggerConfiguration> _configuration;
    private readonly ModbusDevicePool _devicePool;
    private readonly DeviceHealthTracker _healthTracker;
    private readonly IDataProcessor _dataProcessor;
    private readonly ITimescaleStorage _timescaleStorage;
    private readonly ConcurrentDictionary<string, DeviceReading> _lastReadings = new();
    private readonly SemaphoreSlim _startStopLock = new(1, 1);
    private CancellationTokenSource? _stoppingCts;
    private DateTimeOffset? _actualStartTime;
    private bool _disposed;

    /// <summary>
    /// Initialize the ADAM logger service
    /// </summary>
    public AdamLoggerService(
        ILogger<AdamLoggerService> logger,
        IOptions<LoggerConfiguration> configuration,
        ModbusDevicePool devicePool,
        DeviceHealthTracker healthTracker,
        IDataProcessor dataProcessor,
        ITimescaleStorage timescaleStorage)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _devicePool = devicePool ?? throw new ArgumentNullException(nameof(devicePool));
        _healthTracker = healthTracker ?? throw new ArgumentNullException(nameof(healthTracker));
        _dataProcessor = dataProcessor ?? throw new ArgumentNullException(nameof(dataProcessor));
        _timescaleStorage = timescaleStorage ?? throw new ArgumentNullException(nameof(timescaleStorage));

        // Subscribe to device readings
        _devicePool.ReadingReceived += OnReadingReceived;
    }

    /// <summary>
    /// Start the service
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _startStopLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Starting ADAM Logger Service");

            // Validate configuration
            var config = _configuration.Value;
            var validationResult = config.Validate();
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors);
                throw new InvalidOperationException($"Invalid configuration: {errors}");
            }

            // Test TimescaleDB connection
            _logger.LogInformation("Testing TimescaleDB connection");
            if (!await _timescaleStorage.TestConnectionAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Failed to connect to TimescaleDB");
            }

            // Create cancellation token for stopping
            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Add all configured devices to the pool
            _logger.LogInformation("Initializing {Count} devices", config.Devices.Count);
            foreach (var deviceConfig in config.Devices)
            {
                if (!deviceConfig.Enabled)
                {
                    _logger.LogInformation("Skipping disabled device {DeviceId}", deviceConfig.DeviceId);
                    continue;
                }

                var added = await _devicePool.AddDeviceAsync(deviceConfig, cancellationToken).ConfigureAwait(false);
                if (added)
                {
                    _logger.LogInformation(
                        "Added device {DeviceId} at {IpAddress}:{Port}",
                        deviceConfig.DeviceId, deviceConfig.IpAddress, deviceConfig.Port);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to add device {DeviceId}",
                        deviceConfig.DeviceId);
                }
            }

            // Record actual start time after successful initialization
            _actualStartTime = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "ADAM Logger Service started with {DeviceCount} active devices",
                _devicePool.DeviceCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ADAM Logger Service");
            throw;
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    /// <summary>
    /// Stop the service
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _startStopLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Stopping ADAM Logger Service");

            // Signal cancellation
            _stoppingCts?.Cancel();

            // Stop all device polling
            await _devicePool.StopAllAsync().ConfigureAwait(false);

            // Force flush any pending data and process dead letter queue
            var flushResult = await _timescaleStorage.ForceFlushAsync(cancellationToken).ConfigureAwait(false);
            if (!flushResult)
            {
                _logger.LogWarning("Force flush operation completed with errors during shutdown");
            }

            // Reset start time on stop
            _actualStartTime = null;

            _logger.LogInformation("ADAM Logger Service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping ADAM Logger Service");
            throw;
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    /// <summary>
    /// Handle device reading events
    /// </summary>
    private async void OnReadingReceived(DeviceReading reading)
    {
        try
        {
            // Get previous reading for rate calculation
            var channelKey = GetChannelKey(reading.DeviceId, reading.Channel);
            _lastReadings.TryGetValue(channelKey, out var previousReading);

            // Process the reading
            var processedReading = _dataProcessor.ProcessReading(reading, previousReading);

            // Store the processed reading for next time
            _lastReadings[channelKey] = processedReading;

            // Log any quality issues
            if (processedReading.Quality != DataQuality.Good)
            {
                _logger.LogWarning(
                    "Device {DeviceId} channel {Channel}: Quality={Quality}, Value={Value}, Rate={Rate}",
                    processedReading.DeviceId, processedReading.Channel,
                    processedReading.Quality, processedReading.ProcessedValue, processedReading.Rate);
            }

            // Write to InfluxDB
            await _timescaleStorage.WriteReadingAsync(processedReading).ConfigureAwait(false);

            // Log high-frequency updates at debug level
            _logger.LogDebug(
                "Processed reading from {DeviceId} channel {Channel}: Value={Value}, Rate={Rate}",
                processedReading.DeviceId, processedReading.Channel,
                processedReading.ProcessedValue, processedReading.Rate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing reading from device {DeviceId} channel {Channel}",
                reading.DeviceId, reading.Channel);
        }
    }

    /// <summary>
    /// Get current service status
    /// </summary>
    public ServiceStatus GetStatus()
    {
        var deviceHealth = _healthTracker.GetAllDeviceHealth();
        var connectedDevices = deviceHealth.Count(h => h.Value.IsConnected);
        var totalDevices = _devicePool.DeviceCount;

        return new ServiceStatus
        {
            IsRunning = _stoppingCts != null && !_stoppingCts.Token.IsCancellationRequested,
            StartTime = _actualStartTime ?? DateTimeOffset.UtcNow,
            TotalDevices = totalDevices,
            ConnectedDevices = connectedDevices,
            DeviceHealth = deviceHealth
        };
    }

    /// <summary>
    /// Add a new device dynamically
    /// </summary>
    public async Task<bool> AddDeviceAsync(DeviceConfig config)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AdamLoggerService));

        _logger.LogInformation("Adding new device {DeviceId}", config.DeviceId);
        return await _devicePool.AddDeviceAsync(config).ConfigureAwait(false);
    }

    /// <summary>
    /// Remove a device dynamically
    /// </summary>
    public async Task<bool> RemoveDeviceAsync(string deviceId)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AdamLoggerService));

        _logger.LogInformation("Removing device {DeviceId}", deviceId);

        // Remove last readings for all channels
        var keysToRemove = _lastReadings.Keys
            .Where(k => k.StartsWith($"{deviceId}:"))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _lastReadings.TryRemove(key, out _);
        }

        return await _devicePool.RemoveDeviceAsync(deviceId).ConfigureAwait(false);
    }

    /// <summary>
    /// Restart a device connection
    /// </summary>
    public async Task<bool> RestartDeviceAsync(string deviceId)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AdamLoggerService));

        _logger.LogInformation("Restarting device {DeviceId}", deviceId);
        return await _devicePool.RestartDeviceAsync(deviceId).ConfigureAwait(false);
    }

    private static string GetChannelKey(string deviceId, int channel)
    {
        return $"{deviceId}:{channel}";
    }

    /// <summary>
    /// Dispose of service resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Unsubscribe from events
        _devicePool.ReadingReceived -= OnReadingReceived;

        // Stop the service if running
        try
        {
            StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during dispose");
        }

        // Dispose resources
        _stoppingCts?.Dispose();
        _startStopLock?.Dispose();
    }
}

/// <summary>
/// Service status information
/// </summary>
public class ServiceStatus
{
    /// <summary>
    /// Whether the service is currently running
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// When the service was started
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Total number of configured devices
    /// </summary>
    public int TotalDevices { get; init; }

    /// <summary>
    /// Number of currently connected devices
    /// </summary>
    public int ConnectedDevices { get; init; }

    /// <summary>
    /// Health information for all devices
    /// </summary>
    public Dictionary<string, DeviceHealth> DeviceHealth { get; init; } = new();
}
