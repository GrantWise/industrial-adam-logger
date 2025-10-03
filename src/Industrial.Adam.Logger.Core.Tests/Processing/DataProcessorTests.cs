using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Industrial.Adam.Logger.Core.Processing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Processing;

public class DataProcessorTests
{
    private readonly Mock<ILogger<DataProcessor>> _loggerMock;
    private readonly LoggerConfiguration _testConfig;
    private readonly DataProcessor _processor;

    public DataProcessorTests()
    {
        _loggerMock = new Mock<ILogger<DataProcessor>>();
        _testConfig = new LoggerConfiguration
        {
            Devices = new List<DeviceConfig>
            {
                new DeviceConfig
                {
                    DeviceId = "TEST001",
                    IpAddress = "192.168.1.10",
                    Port = 502,
                    UnitId = 1,
                    Channels = new List<ChannelConfig>
                    {
                        new ChannelConfig
                        {
                            ChannelNumber = 0,
                            Name = "Counter 1",
                            StartRegister = 0,
                            RegisterCount = 2,
                            ScaleFactor = 1.0,
                            MinValue = 0,
                            MaxValue = 1000000,
                            MaxChangeRate = 1000
                        },
                        new ChannelConfig
                        {
                            ChannelNumber = 1,
                            Name = "Counter 2",
                            StartRegister = 2,
                            RegisterCount = 1, // 16-bit counter
                            ScaleFactor = 0.1
                        }
                    }
                }
            }
        };

        // Use simple rate calculation for tests (not windowed)
        _processor = new DataProcessor(_loggerMock.Object, _testConfig, useWindowedCalculation: false);
    }

    [Fact]
    public void ProcessReading_WithValidReading_ReturnsProcessedReading()
    {
        // Arrange
        var reading = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 12345,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = _processor.ProcessReading(reading);

        // Assert
        result.ProcessedValue.Should().Be(12345.0); // ScaleFactor = 1.0
        result.Quality.Should().Be(DataQuality.Good);
        result.Rate.Should().BeNull(); // No previous reading
    }

    [Fact]
    public void ProcessReading_WithScaleFactor_AppliesScaling()
    {
        // Arrange
        var reading = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 1, // Channel with scale factor 0.1
            RawValue = 500,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = _processor.ProcessReading(reading);

        // Assert
        result.ProcessedValue.Should().Be(50.0); // 500 * 0.1
        result.Quality.Should().Be(DataQuality.Good);
    }

    [Fact]
    public void ProcessReading_WithPreviousReading_CalculatesRate()
    {
        // Arrange
        var previous = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 1000,
            Timestamp = DateTimeOffset.UtcNow.AddSeconds(-10)
        };

        var current = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 1500,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = _processor.ProcessReading(current, previous);

        // Assert
        result.Rate.Should().NotBeNull();
        result.Rate.Should().BeApproximately(50.0, 0.1); // (1500-1000)/10 = 50 units/second with timing precision tolerance
        result.Quality.Should().Be(DataQuality.Good);
    }

    [Fact]
    public void ProcessReading_With32BitCounterOverflow_HandlesCorrectly()
    {
        // Arrange
        var previous = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0, // 32-bit counter (2 registers)
            RawValue = 4294967290, // Near max
            Timestamp = DateTimeOffset.UtcNow.AddSeconds(-5)
        };

        var current = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 10, // Wrapped around
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = _processor.ProcessReading(current, previous);

        // Assert
        result.Rate.Should().NotBeNull();
        // Overflow: ((2^32) - 4294967290 + 10) / 5 = (6 + 10) / 5 = 16 / 5 = 3.2
        result.Rate.Should().BeApproximately(3.2, 0.1);
    }

    [Fact]
    public void ProcessReading_With16BitCounterOverflow_HandlesCorrectly()
    {
        // Arrange
        var previous = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 1, // 16-bit counter (1 register)
            RawValue = 65530,
            Timestamp = DateTimeOffset.UtcNow.AddSeconds(-2)
        };

        var current = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 1,
            RawValue = 5, // Wrapped around
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = _processor.ProcessReading(current, previous);

        // Assert
        result.Rate.Should().NotBeNull();
        // Overflow: (65536 - 65530 + 5) / 2 = 11 / 2 = 5.5
        // With scale factor 0.1: 5.5 * 0.1 = 0.55
        result.Rate.Should().BeApproximately(0.55, 0.01); // This one is for scale factor precision, keep tight tolerance
    }

    [Fact]
    public void ProcessReading_ExceedsMaxValue_SetsQualityBad()
    {
        // Arrange
        var reading = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 2000000, // Exceeds max of 1000000
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = _processor.ProcessReading(reading);

        // Assert
        result.Quality.Should().Be(DataQuality.Bad);

        // Verify warning log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("above maximum")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ProcessReading_BelowMinValue_SetsQualityBad()
    {
        // Arrange
        var reading = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 0,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Modify reading to have negative processed value
        reading = reading with { ProcessedValue = -10 };

        // Act
        var result = _processor.ValidateReading(reading);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ProcessReading_ExceedsMaxChangeRate_SetsQualityUncertain()
    {
        // Arrange
        var previous = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 1000,
            Timestamp = DateTimeOffset.UtcNow.AddSeconds(-1)
        };

        var current = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 3000, // Change of 2000 in 1 second (exceeds max rate of 1000)
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = _processor.ProcessReading(current, previous);

        // Assert
        result.Quality.Should().Be(DataQuality.Degraded);
        result.Rate.Should().BeApproximately(2000, 1.0); // Still calculated with reasonable tolerance for timing precision

        // Verify warning log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("exceeds max change rate")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ProcessReading_WithUnknownChannel_ReturnsOriginalReading()
    {
        // Arrange
        var reading = new DeviceReading
        {
            DeviceId = "UNKNOWN",
            Channel = 99,
            RawValue = 12345,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = _processor.ProcessReading(reading);

        // Assert
        result.Should().BeEquivalentTo(reading);

        // Verify warning log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("No configuration found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ValidateReading_WithValidReading_ReturnsTrue()
    {
        // Arrange
        var reading = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 50000,
            ProcessedValue = 50000,
            Timestamp = DateTimeOffset.UtcNow,
            Quality = DataQuality.Good
        };

        // Act
        var result = _processor.ValidateReading(reading);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateReading_WithInvalidReading_ReturnsFalse()
    {
        // Arrange
        var reading = new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 2000000,
            ProcessedValue = 2000000, // Exceeds max
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        var result = _processor.ValidateReading(reading);

        // Assert
        result.Should().BeFalse();
    }
}
