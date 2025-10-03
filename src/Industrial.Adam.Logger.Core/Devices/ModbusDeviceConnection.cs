using System.Diagnostics;
using System.Net.Sockets;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;
using NModbus;
using Polly;
using Polly.Retry;

namespace Industrial.Adam.Logger.Core.Devices;

/// <summary>
/// Manages a single Modbus TCP connection with industrial-grade reliability
/// </summary>
public sealed class ModbusDeviceConnection : IDisposable
{
    private readonly DeviceConfig _config;
    private readonly ILogger<ModbusDeviceConnection> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly AsyncRetryPolicy _retryPolicy;

    private TcpClient? _tcpClient;
    private IModbusMaster? _modbusMaster;
    private volatile bool _isConnected;
    private DateTimeOffset _lastConnectionAttempt = DateTimeOffset.MinValue;
    private bool _disposed;

    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId => _config.DeviceId;

    /// <summary>
    /// Current connection status
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Device configuration
    /// </summary>
    public DeviceConfig Configuration => _config;

    /// <summary>
    /// Initialize a new Modbus device connection
    /// </summary>
    /// <param name="config">Device configuration</param>
    /// <param name="logger">Logger instance</param>
    public ModbusDeviceConnection(DeviceConfig config, ILogger<ModbusDeviceConnection> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<SocketException>()
            .Or<TimeoutException>()
            .Or<InvalidOperationException>()
            .WaitAndRetryAsync(
                _config.MaxRetries,
                retryAttempt => TimeSpan.FromMilliseconds(
                    Math.Min(Constants.DefaultRetryDelayMs * Math.Pow(2, retryAttempt - 1), 30000)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception,
                        "Device {DeviceId}: Retry {RetryCount}/{MaxRetries} after {Delay}ms",
                        _config.DeviceId, retryCount, _config.MaxRetries, timeSpan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Establish connection with throttling and industrial features
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // Connection throttling to prevent spam
        if (DateTimeOffset.UtcNow - _lastConnectionAttempt < TimeSpan.FromSeconds(Constants.ConnectionRetryCooldownSeconds))
        {
            return _isConnected;
        }

        _lastConnectionAttempt = DateTimeOffset.UtcNow;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Clean up existing connection
            await DisconnectInternalAsync();

            // Create TCP client with industrial settings
            _tcpClient = new TcpClient
            {
                ReceiveTimeout = _config.TimeoutMs,
                SendTimeout = _config.TimeoutMs,
                NoDelay = true, // Disable Nagle for real-time data
                ReceiveBufferSize = _config.ReceiveBufferSize,
                SendBufferSize = _config.SendBufferSize
            };

            // Configure TCP Keep-Alive for connection monitoring
            if (_config.KeepAlive)
            {
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                // Platform-specific keep-alive settings
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
                {
                    // Keep-alive time: 30 seconds
                    _tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
                    // Keep-alive interval: 5 seconds
                    _tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                    // Keep-alive retry count: 3
                    _tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
                }
            }

            // Connect with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_config.TimeoutMs);

            await _tcpClient.ConnectAsync(_config.IpAddress, _config.Port, cts.Token);

            // Create Modbus master
            var factory = new ModbusFactory();
            _modbusMaster = factory.CreateMaster(_tcpClient);
            _modbusMaster.Transport.ReadTimeout = _config.TimeoutMs;
            _modbusMaster.Transport.WriteTimeout = _config.TimeoutMs;
            _modbusMaster.Transport.Retries = 0; // We handle retries with Polly

            _isConnected = true;
            _logger.LogInformation(
                "Connected to device {DeviceId} at {IpAddress}:{Port}",
                _config.DeviceId, _config.IpAddress, _config.Port);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to connect to device {DeviceId} at {IpAddress}:{Port}",
                _config.DeviceId, _config.IpAddress, _config.Port);

            _isConnected = false;
            await DisconnectInternalAsync();
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Read registers with retry policy
    /// </summary>
    public async Task<ReadResult> ReadRegistersAsync(
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Ensure connection
            if (!_isConnected && !await ConnectAsync(cancellationToken))
            {
                return new ReadResult
                {
                    Success = false,
                    Error = "Failed to establish connection",
                    Duration = stopwatch.Elapsed
                };
            }

            // Read with retry policy
            var registers = await _retryPolicy.ExecuteAsync(async (ct) =>
            {
                if (_modbusMaster == null)
                    throw new InvalidOperationException("Modbus master not initialized");

                // Check connection before read
                if (!_isConnected || _tcpClient?.Connected != true)
                {
                    _isConnected = false;
                    await ConnectAsync(ct);
                    if (!_isConnected)
                        throw new InvalidOperationException("Connection lost");
                }

                return await Task.Run(() =>
                    _modbusMaster.ReadHoldingRegisters(_config.UnitId, startAddress, count), ct);
            }, cancellationToken);

            return new ReadResult
            {
                Success = true,
                Registers = registers,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to read registers from device {DeviceId} after retries",
                _config.DeviceId);

            // Mark as disconnected on failure
            _isConnected = false;

            return new ReadResult
            {
                Success = false,
                Error = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Test connection with lightweight read
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to read a single register
            var result = await ReadRegistersAsync(0, 1, cancellationToken);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disconnect from device
    /// </summary>
    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            await DisconnectInternalAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task DisconnectInternalAsync()
    {
        try
        {
            _modbusMaster?.Dispose();

            if (_tcpClient != null)
            {
                if (_tcpClient.Connected)
                {
                    _tcpClient.Close();
                }
                _tcpClient.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect for device {DeviceId}", _config.DeviceId);
        }
        finally
        {
            _modbusMaster = null;
            _tcpClient = null;
            _isConnected = false;
        }

        // Small delay to ensure socket cleanup
        await Task.Delay(100);
    }

    /// <summary>
    /// Dispose of connection resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Synchronous disconnect for disposal
        Task.Run(async () => await DisconnectAsync()).Wait(TimeSpan.FromSeconds(5));

        _connectionLock.Dispose();
    }
}

/// <summary>
/// Result of a register read operation
/// </summary>
public sealed class ReadResult
{
    /// <summary>
    /// Whether the read operation was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The register values read from the device
    /// </summary>
    public ushort[]? Registers { get; init; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Duration of the read operation
    /// </summary>
    public TimeSpan Duration { get; init; }
}
