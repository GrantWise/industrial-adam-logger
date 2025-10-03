using System.Diagnostics;
using System.Net.Http;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Integration;

/// <summary>
/// Test fixture for managing ADAM simulator lifecycle during E2E tests.
/// Handles starting/stopping simulator processes and verifying they're ready.
/// </summary>
public class E2ETestFixture : IAsyncLifetime
{
    private readonly string _simulatorProjectPath;
    private readonly List<SimulatorProcess> _runningSimulators = new();
    private readonly HttpClient _httpClient;

    public E2ETestFixture()
    {
        // Navigate to simulator project from test project
        var testDir = Directory.GetCurrentDirectory();
        var solutionRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        _simulatorProjectPath = Path.Combine(solutionRoot, "src", "Industrial.Adam.Logger.Simulator");

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public Task InitializeAsync()
    {
        // Verify simulator project exists
        if (!Directory.Exists(_simulatorProjectPath))
        {
            throw new InvalidOperationException(
                $"Simulator project not found at: {_simulatorProjectPath}");
        }

        // Ensure simulator is built
        var csprojPath = Path.Combine(_simulatorProjectPath, "Industrial.Adam.Logger.Simulator.csproj");
        if (!File.Exists(csprojPath))
        {
            throw new InvalidOperationException(
                $"Simulator project file not found at: {csprojPath}");
        }

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Stop all running simulators
        foreach (var simulator in _runningSimulators.ToList())
        {
            await StopSimulatorAsync(simulator);
        }

        _httpClient.Dispose();
    }

    /// <summary>
    /// Start an ADAM simulator instance
    /// </summary>
    /// <param name="instanceNumber">Simulator instance number (for identification)</param>
    /// <param name="modbusPort">Modbus TCP port</param>
    /// <param name="apiPort">REST API port</param>
    /// <param name="baseRate">Production rate in units/minute</param>
    /// <returns>SimulatorProcess handle for later cleanup</returns>
    public async Task<SimulatorProcess> StartSimulatorAsync(
        int instanceNumber,
        int modbusPort,
        int apiPort,
        double baseRate = 120.0)
    {
        var deviceId = $"SIM-E2E-{instanceNumber:D2}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --configuration Release",
            WorkingDirectory = _simulatorProjectPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // Set environment variables for configuration
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://localhost:{apiPort}";
        startInfo.Environment["SimulatorSettings__DeviceId"] = deviceId;
        startInfo.Environment["SimulatorSettings__DeviceName"] = $"E2E Test Simulator {instanceNumber}";
        startInfo.Environment["SimulatorSettings__ModbusPort"] = modbusPort.ToString();
        startInfo.Environment["SimulatorSettings__ApiPort"] = apiPort.ToString();
        startInfo.Environment["ProductionSettings__BaseRate"] = baseRate.ToString();
        startInfo.Environment["ProductionSettings__RateVariation"] = "0.1";

        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start simulator process");
        }

        var simulator = new SimulatorProcess
        {
            Process = process,
            InstanceNumber = instanceNumber,
            ModbusPort = modbusPort,
            ApiPort = apiPort,
            DeviceId = deviceId
        };

        // Wait for simulator to be ready
        var ready = await WaitForSimulatorReadyAsync(apiPort, timeout: TimeSpan.FromSeconds(30));
        if (!ready)
        {
            process.Kill();
            throw new InvalidOperationException(
                $"Simulator {instanceNumber} failed to start within timeout. Port: {apiPort}");
        }

        _runningSimulators.Add(simulator);
        return simulator;
    }

    /// <summary>
    /// Stop a running simulator instance
    /// </summary>
    public async Task StopSimulatorAsync(SimulatorProcess simulator)
    {
        if (simulator.Process != null && !simulator.Process.HasExited)
        {
            try
            {
                // Try graceful shutdown first via API
                try
                {
                    await _httpClient.PostAsync(
                        $"http://localhost:{simulator.ApiPort}/api/simulator/shutdown",
                        null);
                    await Task.Delay(1000);
                }
                catch
                {
                    // If API call fails, force kill
                }

                // Force kill if still running
                if (!simulator.Process.HasExited)
                {
                    simulator.Process.Kill();
                    await simulator.Process.WaitForExitAsync();
                }
            }
            catch (Exception)
            {
                // Best effort - simulator may already be stopped
            }

            simulator.Process.Dispose();
        }

        _runningSimulators.Remove(simulator);
    }

    /// <summary>
    /// Wait for simulator to be ready by polling health endpoint
    /// </summary>
    private async Task<bool> WaitForSimulatorReadyAsync(int apiPort, TimeSpan timeout)
    {
        var healthUrl = $"http://localhost:{apiPort}/api/simulator/health";
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var response = await _httpClient.GetAsync(healthUrl);
                if (response.IsSuccessStatusCode)
                {
                    // Extra delay to ensure Modbus server is also ready
                    await Task.Delay(500);
                    return true;
                }
            }
            catch
            {
                // Expected during startup
            }

            await Task.Delay(500);
        }

        return false;
    }
}

/// <summary>
/// Represents a running simulator process
/// </summary>
public class SimulatorProcess
{
    public Process? Process { get; set; }
    public int InstanceNumber { get; set; }
    public int ModbusPort { get; set; }
    public int ApiPort { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}

/// <summary>
/// xUnit collection definition for E2E tests.
/// All E2E tests share the same fixture instance.
/// </summary>
[CollectionDefinition("E2E")]
public class E2ETestCollection : ICollectionFixture<E2ETestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
