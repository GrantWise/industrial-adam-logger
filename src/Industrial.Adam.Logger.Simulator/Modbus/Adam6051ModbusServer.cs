using System.Net;
using System.Net.Sockets;
using NModbus;
using NModbus.Data;

namespace Industrial.Adam.Logger.Simulator.Modbus;

/// <summary>
/// Modbus TCP server that simulates an ADAM-6051 device using NModbus
/// </summary>
public class Adam6051ModbusServer : IDisposable
{
    private readonly Adam6051RegisterMap _registerMap;
    private readonly ILogger<Adam6051ModbusServer> _logger;
    private readonly int _port;
    private TcpListener? _tcpListener;
    private IModbusSlaveNetwork? _slaveNetwork;
    private ISlaveDataStore? _dataStore;
    private CancellationTokenSource? _serverCts;
    private Task? _serverTask;

    public Adam6051ModbusServer(
        Adam6051RegisterMap registerMap,
        ILogger<Adam6051ModbusServer> logger,
        int port = 502)
    {
        _registerMap = registerMap ?? throw new ArgumentNullException(nameof(registerMap));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _port = port;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Create TCP listener
            _tcpListener = new TcpListener(IPAddress.Any, _port);
            _tcpListener.Start();

            // Create Modbus factory and slave network
            var factory = new ModbusFactory();
            _slaveNetwork = factory.CreateSlaveNetwork(_tcpListener);

            // Create data store for registers
            _dataStore = new SlaveDataStore();

            // Create Modbus slave that responds to Unit ID 1 (what the logger expects)  
            var slave = factory.CreateSlave(1, _dataStore);

            // Add slave to network
            _slaveNetwork.AddSlave(slave);

            _logger.LogInformation("NModbus TCP server started on port {Port} with Unit ID 1", _port);

            // Start server tasks
            _serverCts = new CancellationTokenSource();
            _serverTask = _slaveNetwork.ListenAsync(_serverCts.Token);

            // Start register update task
            Task.Run(() => UpdateRegistersLoop(_serverCts.Token));

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Modbus TCP server");
            throw;
        }
    }

    /// <summary>
    /// Continuously updates register values from the register map
    /// </summary>
    private async void UpdateRegistersLoop(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Register update loop started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_dataStore != null)
                    {
                        // Get fresh data from register map
                        var mapData = _registerMap.ReadHoldingRegisters(0, 4); // Get first 4 registers (2 counters)

                        // Update the NModbus data store with register values using WritePoints
                        _dataStore.HoldingRegisters.WritePoints(0, mapData);

                        // Log the values we're syncing
                        if (mapData.Length >= 4)
                        {
                            var counter0 = ((uint)mapData[1] << 16) | mapData[0];
                            var counter1 = ((uint)mapData[3] << 16) | mapData[2];
                            _logger.LogDebug("Synced register data - Counter0: {Counter0}, Counter1: {Counter1}", counter0, counter1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in register update cycle");
                }

                // Update every 1 second for now (less frequent for easier debugging)
                await Task.Delay(1000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Register update loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in register update loop");
        }
    }

    public async Task StopAsync()
    {
        try
        {
            // Cancel server task
            _serverCts?.Cancel();

            // Wait for server task to complete
            if (_serverTask != null)
            {
                try
                {
                    await _serverTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
            }

            // Stop TCP listener
            _tcpListener?.Stop();

            // Dispose slave network
            _slaveNetwork?.Dispose();

            _logger.LogInformation("NModbus TCP server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Modbus TCP server");
        }
    }

    public void Dispose()
    {
        try
        {
            _serverCts?.Cancel();

            if (_serverTask != null)
            {
                try
                {
                    _serverTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
            }

            _serverCts?.Dispose();
            _tcpListener?.Stop();
            _slaveNetwork?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disposal");
        }
    }
}
