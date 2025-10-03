using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Configuration;

public class ChannelConfigTests
{
    [Fact]
    public void Validate_WithInvalidScaleFactor_ReturnsInvalid()
    {
        // Arrange
        var config = new ChannelConfig
        {
            ChannelNumber = 0,
            Name = "Test Channel",
            StartRegister = 0,
            RegisterCount = 2,
            ScaleFactor = 0 // Invalid
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("ScaleFactor must be greater than 0");
    }

    [Fact]
    public void Validate_WithInvalidMinMaxValues_ReturnsInvalid()
    {
        // Arrange
        var config = new ChannelConfig
        {
            ChannelNumber = 0,
            Name = "Test Channel",
            StartRegister = 0,
            RegisterCount = 2,
            MinValue = 100,
            MaxValue = 50 // Invalid: less than MinValue
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("MinValue must be less than MaxValue");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void Validate_RegisterCount_ValidatesCorrectly(int registerCount, bool expectedValid)
    {
        // Arrange
        var config = new ChannelConfig
        {
            ChannelNumber = 0,
            Name = "Test Channel",
            StartRegister = 0,
            RegisterCount = registerCount
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void Validate_WithValidConfig_ReturnsValid()
    {
        // Arrange
        var config = new ChannelConfig
        {
            ChannelNumber = 0,
            Name = "Production Counter",
            StartRegister = 40001,
            RegisterCount = 2,
            Enabled = true,
            ScaleFactor = 1.0,
            Offset = 0.0,
            Unit = "parts",
            MinValue = 0,
            MaxValue = 10000,
            HighRateThreshold = 100,
            Tags = new Dictionary<string, string>
            {
                ["line"] = "Line1",
                ["product"] = "WidgetA"
            }
        };

        // Act
        var result = config.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
