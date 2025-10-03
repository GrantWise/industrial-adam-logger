using System.Collections.Concurrent;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Core.Devices;

/// <summary>
/// Manages a pool of concurrent Modbus device connections with per-device polling
/// </summary>
public sealed class ModbusDevicePool : IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, DeviceContext> _devices = new();
    private readonly ILogger<ModbusDevicePool> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DeviceHealthTracker _healthTracker;
    private volatile bool _disposed;

    /// <summary>
    /// Event raised when device readings are available
    /// </summary>
    public event Action<DeviceReading>? ReadingReceived;

    /// <summary>
    /// Currently active device IDs
    /// </summary>
    public IEnumerable<string> ActiveDeviceIds => _devices.Keys;

    /// <summary>
    /// Number of active devices
    /// </summary>
    public int DeviceCount => _devices.Count;

    /// <summary>
    /// Initialize the Modbus device pool
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="loggerFactory">Logger factory for creating device loggers</param>
    /// <param name="healthTracker">Device health tracker</param>
    public ModbusDevicePool(
        ILogger<ModbusDevicePool> logger,
        ILoggerFactory loggerFactory,
        DeviceHealthTracker healthTracker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _healthTracker = healthTracker ?? throw new ArgumentNullException(nameof(healthTracker));
    }

    /// <summary>
    /// Add a new device to the pool
    /// </summary>
    public Task<bool> AddDeviceAsync(DeviceConfig config, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ModbusDevicePool));

        ArgumentNullException.ThrowIfNull(config);

        // Check if device already exists
        if (_devices.ContainsKey(config.DeviceId))
        {
            _logger.LogWarning("Device {DeviceId} already exists in pool", config.DeviceId);
            return Task.FromResult(false);
        }

        var context = new DeviceContext
        {
            Connection = new ModbusDeviceConnection(
                config,
                _loggerFactory.CreateLogger<ModbusDeviceConnection>()),
            Config = config,
            CancellationTokenSource = new CancellationTokenSource()
        };

        if (!_devices.TryAdd(config.DeviceId, context))
        {
            context.Dispose();
            return Task.FromResult(false);
        }

        // Start polling for this device and track the task
        context.PollingTask = Task.Run(
            async () => await PollDeviceAsync(context).ConfigureAwait(false),
            context.CancellationTokenSource.Token);

        _logger.LogInformation(
            "Added device {DeviceId} to pool (total: {Count})",
            config.DeviceId, _devices.Count);

        return Task.FromResult(true);
    }

    /// <summary>
    /// Remove a device from the pool
    /// </summary>
    public async Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ModbusDevicePool));

        if (!_devices.TryRemove(deviceId, out var context))
        {
            _logger.LogWarning("Device {DeviceId} not found in pool", deviceId);
            return false;
        }

        // Cancel polling
        await context.CancellationTokenSource.CancelAsync();

        // Disconnect and cleanup
        await context.Connection.DisconnectAsync();
        context.Dispose();

        // Reset health data
        _healthTracker.ResetDeviceHealth(deviceId);

        _logger.LogInformation(
            "Removed device {DeviceId} from pool (remaining: {Count})",
            deviceId, _devices.Count);

        return true;
    }

    /// <summary>
    /// Restart a device connection with proper synchronization to prevent race conditions
    /// </summary>
    public async Task<bool> RestartDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ModbusDevicePool));

        if (!_devices.TryGetValue(deviceId, out var context))
        {
            _logger.LogWarning("Device {DeviceId} not found in pool", deviceId);
            return false;
        }

        // Acquire restart lock to prevent concurrent restarts of the same device
        await context.RestartLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Restarting device {DeviceId}", deviceId);

            // Cancel old polling task
            var oldCts = context.CancellationTokenSource;
            await oldCts.CancelAsync().ConfigureAwait(false);

            // Wait for old polling task to complete with timeout
            if (context.PollingTask != null && !context.PollingTask.IsCompleted)
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    await context.PollingTask.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Check which token was cancelled
                    if (timeoutCts.IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            "Old polling task for device {DeviceId} did not complete within 5 seconds",
                            deviceId);
                    }
                    else if (cancellationToken.IsCancellationRequested)
                    {
                        // User requested cancellation, rethrow
                        throw;
                    }
                    // If only the polling task's own cancellation triggered, continue
                }
            }

            // Disconnect
            await context.Connection.DisconnectAsync().ConfigureAwait(false);

            // Dispose old cancellation token source
            oldCts.Dispose();

            // Create new cancellation token source
            context.CancellationTokenSource = new CancellationTokenSource();

            // Start new polling task and track it
            context.PollingTask = Task.Run(
                () => PollDeviceAsync(context),
                context.CancellationTokenSource.Token);

            _logger.LogInformation("Device {DeviceId} restarted successfully", deviceId);

            return true;
        }
        finally
        {
            context.RestartLock.Release();
        }
    }

    /// <summary>
    /// Get connection status for a device
    /// </summary>
    public bool IsDeviceConnected(string deviceId)
    {
        return _devices.TryGetValue(deviceId, out var context) && context.Connection.IsConnected;
    }

    /// <summary>
    /// Get health data for all devices
    /// </summary>
    public Dictionary<string, DeviceHealth> GetAllDeviceHealth()
    {
        return _healthTracker.GetAllDeviceHealth();
    }

    /// <summary>
    /// Stop all device polling
    /// </summary>
    public async Task StopAllAsync()
    {
        _logger.LogInformation("Stopping all device polling");

        // Cancel all polling tasks
        var tasks = _devices.Values.Select(async context =>
        {
            await context.CancellationTokenSource.CancelAsync();
        });

        await Task.WhenAll(tasks);

        // Wait a bit for tasks to complete
        await Task.Delay(500);

        // Disconnect all devices
        var disconnectTasks = _devices.Values.Select(async context =>
        {
            await context.Connection.DisconnectAsync();
        });

        await Task.WhenAll(disconnectTasks);
    }

    /// <summary>
    /// Poll a device continuously
    /// </summary>
    private async Task PollDeviceAsync(DeviceContext context)
    {
        var deviceId = context.Config.DeviceId;
        var token = context.CancellationTokenSource.Token;

        _logger.LogInformation(
            "Starting polling for device {DeviceId} with interval {Interval}ms",
            deviceId, context.Config.PollIntervalMs);

        while (!token.IsCancellationRequested)
        {
            try
            {
                // Read all configured channels
                foreach (var channel in context.Config.Channels)
                {
                    if (token.IsCancellationRequested)
                        break;

                    // Choose read method based on register type
                    var result = channel.RegisterType switch
                    {
                        ModbusRegisterType.InputRegister => await context.Connection.ReadInputRegistersAsync(
                            channel.StartRegister, (ushort)channel.RegisterCount, token),
                        ModbusRegisterType.HoldingRegister => await context.Connection.ReadRegistersAsync(
                            channel.StartRegister, (ushort)channel.RegisterCount, token),
                        _ => throw new NotSupportedException($"Register type {channel.RegisterType} not supported")
                    };

                    if (result.Success && result.Registers != null)
                    {
                        // Record success
                        _healthTracker.RecordSuccess(deviceId, result.Duration);

                        // Process the reading
                        var reading = ProcessReading(context.Config, channel, result.Registers);

                        // Raise event
                        ReadingReceived?.Invoke(reading);
                    }
                    else
                    {
                        // Record failure
                        _healthTracker.RecordFailure(deviceId, result.Error ?? "Unknown error");

                        _logger.LogWarning(
                            "Failed to read channel {Channel} from device {DeviceId}: {Error}",
                            channel.ChannelNumber, deviceId, result.Error);

                        // Create unavailable reading to maintain data integrity and transparency
                        // This ensures downstream systems know the device is offline/unreachable
                        var unavailableReading = CreateUnavailableReading(
                            context.Config,
                            channel,
                            result.Error ?? "Communication failure");

                        // Raise event with unavailable reading
                        ReadingReceived?.Invoke(unavailableReading);
                    }
                }

                // Wait for next poll interval
                await Task.Delay(context.Config.PollIntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error polling device {DeviceId}",
                    deviceId);

                // Brief delay before retry
                await Task.Delay(1000, token);
            }
        }

        _logger.LogInformation("Stopped polling for device {DeviceId}", deviceId);
    }

    /// <summary>
    /// Process raw register values into a device reading
    /// </summary>
    private DeviceReading ProcessReading(DeviceConfig config, ChannelConfig channel, ushort[] registers)
    {
        // Convert registers to numeric value based on data type
        long rawValue = channel.DataType switch
        {
            ChannelDataType.UInt32Counter => ConvertToUInt32(registers),
            ChannelDataType.Int16 => (short)registers[0],
            ChannelDataType.UInt16 => registers[0],
            ChannelDataType.Int32 => ConvertToInt32(registers),
            ChannelDataType.Float32 => ConvertToFloat32AsLong(registers),
            _ => throw new NotSupportedException($"Data type {channel.DataType} not supported")
        };

        // Apply scaling factor and offset
        var processedValue = rawValue * channel.ScaleFactor + channel.Offset;

        return new DeviceReading
        {
            DeviceId = config.DeviceId,
            Channel = channel.ChannelNumber,
            RawValue = rawValue,
            ProcessedValue = processedValue,
            Timestamp = DateTimeOffset.UtcNow,
            Quality = DataQuality.Good,
            Unit = channel.Unit
        };
    }

    /// <summary>
    /// Convert two 16-bit registers to 32-bit unsigned integer (little-endian)
    /// Used for digital counters (ADAM-6051, 6052, etc.)
    /// </summary>
    private static long ConvertToUInt32(ushort[] registers)
    {
        if (registers.Length < 2)
            return registers.Length == 1 ? registers[0] : 0;

        return ((long)registers[1] << 16) | registers[0];
    }

    /// <summary>
    /// Convert two 16-bit registers to 32-bit signed integer (little-endian)
    /// </summary>
    private static long ConvertToInt32(ushort[] registers)
    {
        return (int)ConvertToUInt32(registers);
    }

    /// <summary>
    /// Convert two 16-bit registers to IEEE 754 float, return as long for storage
    /// Used for analog modules (ADAM-6017, 6015, etc.)
    /// </summary>
    private static long ConvertToFloat32AsLong(ushort[] registers)
    {
        if (registers.Length < 2)
            return 0;

        // ADAM modules use big-endian byte order for floats
        var bytes = new byte[4];
        bytes[0] = (byte)(registers[0] >> 8);
        bytes[1] = (byte)(registers[0] & 0xFF);
        bytes[2] = (byte)(registers[1] >> 8);
        bytes[3] = (byte)(registers[1] & 0xFF);

        var floatValue = BitConverter.ToSingle(bytes, 0);

        // Store as scaled long (multiply by 1000 to preserve 3 decimal places)
        return (long)(floatValue * 1000);
    }

    /// <summary>
    /// Create an unavailable reading when device communication fails.
    /// This maintains data integrity by clearly indicating when data cannot be obtained.
    /// </summary>
    /// <param name="config">Device configuration</param>
    /// <param name="channel">Channel configuration</param>
    /// <param name="errorMessage">Error message describing the failure</param>
    /// <returns>Device reading with Unavailable quality</returns>
    private DeviceReading CreateUnavailableReading(
        DeviceConfig config,
        ChannelConfig channel,
        string errorMessage)
    {
        return new DeviceReading
        {
            DeviceId = config.DeviceId,
            Channel = channel.ChannelNumber,
            RawValue = 0,
            ProcessedValue = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Quality = DataQuality.Unavailable,
            Unit = channel.Unit,
            Tags = new Dictionary<string, string>
            {
                { "error", errorMessage },
                { "reason", "communication_failure" }
            }
        };
    }

    /// <summary>
    /// Asynchronously dispose of all device connections and resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Stop all polling
        await StopAllAsync().ConfigureAwait(false);

        // Dispose all contexts
        foreach (var context in _devices.Values)
        {
            context.Dispose();
        }

        _devices.Clear();
    }

    /// <summary>
    /// Dispose of all device connections and resources (synchronous fallback)
    /// </summary>
    public void Dispose()
    {
        // Use synchronous disposal pattern - call async version and block
        // This is acceptable as a fallback for consumers that don't support IAsyncDisposable
        DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Internal context for managing a device
    /// </summary>
    private sealed class DeviceContext : IDisposable
    {
        public required ModbusDeviceConnection Connection { get; init; }
        public required DeviceConfig Config { get; init; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
        public Task? PollingTask { get; set; }
        public SemaphoreSlim RestartLock { get; } = new(1, 1);

        public void Dispose()
        {
            RestartLock?.Dispose();
            CancellationTokenSource?.Dispose();
            Connection?.Dispose();
        }
    }
}
