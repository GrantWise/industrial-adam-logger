using BenchmarkDotNet.Attributes;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Devices;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Industrial.Adam.Logger.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ModbusDevicePoolBenchmarks
{
    private ModbusDevicePool _pool = null!;
    private DeviceHealthTracker _healthTracker = null!;
    private List<DeviceConfig> _deviceConfigs = null!;

    [GlobalSetup]
    public void Setup()
    {
        var loggerFactory = new NullLoggerFactory();
        _healthTracker = new DeviceHealthTracker(loggerFactory.CreateLogger<DeviceHealthTracker>());
        _pool = new ModbusDevicePool(
            loggerFactory.CreateLogger<ModbusDevicePool>(),
            loggerFactory,
            _healthTracker);

        // Create test device configurations
        _deviceConfigs = new List<DeviceConfig>();
        for (int i = 0; i < 10; i++)
        {
            _deviceConfigs.Add(new DeviceConfig
            {
                DeviceId = $"Device{i:000}",
                IpAddress = $"192.168.1.{100 + i}",
                Port = 502,
                UnitId = 1,
                Enabled = true,
                PollIntervalMs = 1000,
                TimeoutMs = 3000,
                Channels = new List<ChannelConfig>
                {
                    new() { ChannelNumber = 0, StartRegister = 0, RegisterCount = 2, Enabled = true },
                    new() { ChannelNumber = 1, StartRegister = 2, RegisterCount = 2, Enabled = true }
                }
            });
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pool?.Dispose();
    }

    [Benchmark]
    public async Task AddAndRemoveDevices()
    {
        // Add all devices
        foreach (var config in _deviceConfigs)
        {
            await _pool.AddDeviceAsync(config);
        }

        // Check connection status
        foreach (var config in _deviceConfigs)
        {
            _ = _pool.IsDeviceConnected(config.DeviceId);
        }

        // Get health data
        _ = _pool.GetAllDeviceHealth();

        // Remove all devices
        foreach (var config in _deviceConfigs)
        {
            await _pool.RemoveDeviceAsync(config.DeviceId);
        }
    }

    [Benchmark]
    public void DeviceLookupPerformance()
    {
        // Simulate frequent device lookups
        for (int i = 0; i < 1000; i++)
        {
            var deviceId = $"Device{i % 10:000}";
            _ = _pool.IsDeviceConnected(deviceId);
        }
    }

    [Benchmark]
    public Dictionary<string, DeviceHealth> GetHealthData()
    {
        return _pool.GetAllDeviceHealth();
    }
}
