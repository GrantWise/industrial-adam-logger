using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Industrial.Adam.Logger.Core.Processing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Industrial.Adam.Logger.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class SimpleBenchmarks
{
    private Dictionary<string, string> _channelKeys = null!;
    private ConcurrentDictionary<string, DeviceReading> _readings = null!;
    private DataProcessor _processor = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup test data
        _channelKeys = new Dictionary<string, string>();
        _readings = new ConcurrentDictionary<string, DeviceReading>();

        for (int i = 0; i < 100; i++)
        {
            var deviceId = $"Device{i:000}";
            var channel = i % 4;
            var key = $"{deviceId}:{channel}";
            _channelKeys[key] = $"Config_{i}";

            _readings[key] = new DeviceReading
            {
                DeviceId = deviceId,
                Channel = channel,
                RawValue = i * 1000,
                ProcessedValue = i * 1000.0,
                Timestamp = DateTimeOffset.UtcNow,
                Quality = DataQuality.Good
            };
        }

        // Setup processor
        var config = new LoggerConfiguration
        {
            GlobalPollIntervalMs = 1000,
            Devices = new List<DeviceConfig>
            {
                new()
                {
                    DeviceId = "Device001",
                    IpAddress = "192.168.1.100",
                    Port = 502,
                    UnitId = 1,
                    Enabled = true,
                    Channels = new List<ChannelConfig>
                    {
                        new()
                        {
                            ChannelNumber = 0,
                            StartRegister = 0,
                            RegisterCount = 2,
                            ScaleFactor = 1.0,
                            Enabled = true
                        }
                    }
                }
            }
        };

        _processor = new DataProcessor(NullLogger<DataProcessor>.Instance, config);
    }

    [Benchmark(Baseline = true)]
    public void StringConcatenation()
    {
        for (int i = 0; i < 1000; i++)
        {
            var deviceId = $"Device{i % 100:000}";
            var channel = i % 4;
            var key = $"{deviceId}:{channel}";
            _ = _channelKeys.TryGetValue(key, out _);
        }
    }

    [Benchmark]
    public void ConcurrentDictionaryLookup()
    {
        for (int i = 0; i < 1000; i++)
        {
            var key = $"Device{i % 100:000}:{i % 4}";
            _ = _readings.TryGetValue(key, out _);
        }
    }

    [Benchmark]
    public DeviceReading ProcessReading()
    {
        var reading = new DeviceReading
        {
            DeviceId = "Device001",
            Channel = 0,
            RawValue = 12345,
            ProcessedValue = 12345.0,
            Timestamp = DateTimeOffset.UtcNow,
            Quality = DataQuality.Good
        };

        return _processor.ProcessReading(reading);
    }
}
