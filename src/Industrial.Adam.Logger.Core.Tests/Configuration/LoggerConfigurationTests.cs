using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Configuration;

public class LoggerConfigurationTests
{
    [Fact]
    public void Validate_WithNoDevices_ReturnsInvalid()
    {
        // Arrange
        var config = new LoggerConfiguration
        {
            Devices = new List<DeviceConfig>()
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("At least one device must be configured");
    }

    [Fact]
    public void Validate_WithDuplicateDeviceIds_ReturnsInvalid()
    {
        // Arrange
        var config = new LoggerConfiguration
        {
            Devices = new List<DeviceConfig>
            {
                CreateValidDevice("ADAM001"),
                CreateValidDevice("ADAM001")
            },
            TimescaleDb = CreateValidTimescaleSettings()
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Duplicate device ID: ADAM001");
    }

    [Fact]
    public void Validate_WithValidConfiguration_ReturnsValid()
    {
        // Arrange
        var config = new LoggerConfiguration
        {
            Devices = new List<DeviceConfig>
            {
                CreateValidDevice("ADAM001"),
                CreateValidDevice("ADAM002")
            },
            TimescaleDb = CreateValidTimescaleSettings(),
            GlobalPollIntervalMs = 1000,
            HealthCheckIntervalMs = 30000
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private static DeviceConfig CreateValidDevice(string deviceId)
    {
        return new DeviceConfig
        {
            DeviceId = deviceId,
            IpAddress = "192.168.1.100",
            Port = 502,
            UnitId = 1,
            Channels = new List<ChannelConfig>
            {
                new ChannelConfig
                {
                    ChannelNumber = 0,
                    Name = "Counter 1",
                    StartRegister = 0,
                    RegisterCount = 2
                }
            }
        };
    }

    private static TimescaleSettings CreateValidTimescaleSettings()
    {
        return new TimescaleSettings
        {
            Host = "localhost",
            Port = 5432,
            Database = "adam_counters",
            Username = "adam_user",
            Password = "adam_password",
            TableName = "counter_data_config_test"
        };
    }
}
