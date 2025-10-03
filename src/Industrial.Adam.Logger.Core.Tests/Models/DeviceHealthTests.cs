using FluentAssertions;
using Industrial.Adam.Logger.Core.Models;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Models;

public class DeviceHealthTests
{
    [Fact]
    public void SuccessRate_WithNoReads_ReturnsZero()
    {
        // Arrange
        var health = new DeviceHealth
        {
            DeviceId = "TEST001",
            TotalReads = 0,
            SuccessfulReads = 0
        };

        // Act
        var rate = health.SuccessRate;

        // Assert
        rate.Should().Be(0);
    }

    [Fact]
    public void SuccessRate_WithReads_CalculatesCorrectly()
    {
        // Arrange
        var health = new DeviceHealth
        {
            DeviceId = "TEST001",
            TotalReads = 1000,
            SuccessfulReads = 950
        };

        // Act
        var rate = health.SuccessRate;

        // Assert
        rate.Should().Be(95.0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    public void IsOffline_BasedOnConsecutiveFailures(int failures, bool expectedOffline)
    {
        // Arrange
        var health = new DeviceHealth
        {
            DeviceId = "TEST001",
            ConsecutiveFailures = failures
        };

        // Act
        var isOffline = health.IsOffline;

        // Assert
        isOffline.Should().Be(expectedOffline);
    }
}
