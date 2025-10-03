using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Configuration;

public class DeviceConfigTests
{
    [Fact]
    public void Validate_WithInvalidIpAddress_ReturnsInvalid()
    {
        // Arrange
        var config = new DeviceConfig
        {
            DeviceId = "TEST001",
            IpAddress = "invalid..ip..address",
            Channels = new List<ChannelConfig> { CreateValidChannel() }
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid IP address"));
    }

    [Fact]
    public void Validate_WithNoChannels_ReturnsInvalid()
    {
        // Arrange
        var config = new DeviceConfig
        {
            DeviceId = "TEST001",
            IpAddress = "192.168.1.100",
            Channels = new List<ChannelConfig>()
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least one channel"));
    }

    [Fact]
    public void Validate_WithDuplicateChannels_ReturnsInvalid()
    {
        // Arrange
        var config = new DeviceConfig
        {
            DeviceId = "TEST001",
            IpAddress = "192.168.1.100",
            Channels = new List<ChannelConfig>
            {
                CreateValidChannel(0),
                CreateValidChannel(0)
            }
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("duplicate channel number: 0"));
    }

    [Fact]
    public void Validate_WithValidConfig_ReturnsValid()
    {
        // Arrange
        var config = new DeviceConfig
        {
            DeviceId = "TEST001",
            IpAddress = "192.168.1.100",
            Port = 502,
            UnitId = 1,
            PollIntervalMs = 1000,
            TimeoutMs = 3000,
            MaxRetries = 3,
            Channels = new List<ChannelConfig>
            {
                CreateValidChannel(0),
                CreateValidChannel(1)
            }
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private static ChannelConfig CreateValidChannel(int channelNumber = 0)
    {
        return new ChannelConfig
        {
            ChannelNumber = channelNumber,
            Name = $"Channel {channelNumber}",
            StartRegister = (ushort)(channelNumber * 2),
            RegisterCount = 2,
            Enabled = true
        };
    }
}
