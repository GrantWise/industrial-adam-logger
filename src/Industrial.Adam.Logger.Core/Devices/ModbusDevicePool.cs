using System.Collections.Concurrent;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Core.Devices;

/// <summary>
/// Manages a pool of concurrent Modbus device connections with per-device polling
/// </summary>
public sealed class ModbusDevicePool : IDisposable
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

        // Start polling for this device with improved task creation
        _ = Task.Run(async () => await PollDeviceAsync(context).ConfigureAwait(false),
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
    /// Restart a device connection
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

        _logger.LogInformation("Restarting device {DeviceId}", deviceId);

        // Cancel current polling
        await context.CancellationTokenSource.CancelAsync();

        // Disconnect
        await context.Connection.DisconnectAsync();

        // Create new cancellation token
        context.CancellationTokenSource = new CancellationTokenSource();

        // Restart polling
        _ = Task.Run(() => PollDeviceAsync(context), context.CancellationTokenSource.Token);

        return true;
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

                    var result = await context.Connection.ReadRegistersAsync(
                        channel.StartRegister,
                        (ushort)channel.RegisterCount,
                        token);

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
        // Combine registers into counter value (assuming 32-bit counter)
        long rawValue = 0;
        if (registers.Length >= 2)
        {
            rawValue = ((long)registers[1] << 16) | registers[0];
        }
        else if (registers.Length == 1)
        {
            rawValue = registers[0];
        }

        // Apply scaling factor if configured
        var processedValue = rawValue * channel.ScaleFactor;

        return new DeviceReading
        {
            DeviceId = config.DeviceId,
            Channel = channel.ChannelNumber,
            RawValue = rawValue,
            ProcessedValue = processedValue,
            Timestamp = DateTimeOffset.UtcNow,
            Quality = DataQuality.Good
        };
    }

    /// <summary>
    /// Dispose of all device connections and resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Stop all polling
        Task.Run(async () => await StopAllAsync()).Wait(TimeSpan.FromSeconds(10));

        // Dispose all contexts
        foreach (var context in _devices.Values)
        {
            context.Dispose();
        }

        _devices.Clear();
    }

    /// <summary>
    /// Internal context for managing a device
    /// </summary>
    private sealed class DeviceContext : IDisposable
    {
        public required ModbusDeviceConnection Connection { get; init; }
        public required DeviceConfig Config { get; init; }
        public required CancellationTokenSource CancellationTokenSource { get; set; }

        public void Dispose()
        {
            CancellationTokenSource?.Dispose();
            Connection?.Dispose();
        }
    }
}
