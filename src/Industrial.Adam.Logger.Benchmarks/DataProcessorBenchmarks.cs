using BenchmarkDotNet.Attributes;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Industrial.Adam.Logger.Core.Processing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Industrial.Adam.Logger.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class DataProcessorBenchmarks
{
    private DataProcessor _processor = null!;
    private List<DeviceReading> _readings = null!;
    private DeviceReading _previousReading = null!;

    [GlobalSetup]
    public void Setup()
    {
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
                            MinValue = 0,
                            MaxValue = 1000000,
                            MaxChangeRate = 1000,
                            Enabled = true
                        }
                    }
                }
            }
        };

        _processor = new DataProcessor(NullLogger<DataProcessor>.Instance, config);

        // Create test readings
        _readings = new List<DeviceReading>();
        var timestamp = DateTimeOffset.UtcNow;
        for (int i = 0; i < 100; i++)
        {
            _readings.Add(new DeviceReading
            {
                DeviceId = "Device001",
                Channel = 0,
                RawValue = (long)(i * 1000),
                ProcessedValue = i * 1000,
                Timestamp = timestamp.AddSeconds(i),
                Quality = DataQuality.Good
            });
        }

        _previousReading = new DeviceReading
        {
            DeviceId = "Device001",
            Channel = 0,
            RawValue = 999000,
            ProcessedValue = 999000,
            Timestamp = timestamp.AddSeconds(-1),
            Quality = DataQuality.Good
        };
    }

    [Benchmark]
    public void ProcessSingleReading()
    {
        _ = _processor.ProcessReading(_readings[50]);
    }

    [Benchmark]
    public void ProcessReadingWithRate()
    {
        _ = _processor.ProcessReading(_readings[50], _previousReading);
    }

    [Benchmark]
    public void ProcessBatchOfReadings()
    {
        DeviceReading? prev = null;
        foreach (var reading in _readings)
        {
            _ = _processor.ProcessReading(reading, prev);
            prev = reading;
        }
    }

    [Benchmark]
    public void ValidateReadings()
    {
        foreach (var reading in _readings)
        {
            _ = _processor.ValidateReading(reading);
        }
    }

    [Benchmark]
    public void ChannelKeyLookup()
    {
        // Test the performance of channel key generation and lookup
        for (int i = 0; i < 1000; i++)
        {
            var reading = new DeviceReading
            {
                DeviceId = $"Device{i % 10:000}",
                Channel = i % 4,
                RawValue = i * 1000,
                Timestamp = DateTimeOffset.UtcNow,
                Quality = DataQuality.Good
            };
            _ = _processor.ValidateReading(reading);
        }
    }
}
