using FluentAssertions;
using Industrial.Adam.Logger.Core.Devices;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Devices;

public class DeviceHealthTrackerTests
{
    private readonly Mock<ILogger<DeviceHealthTracker>> _loggerMock;
    private readonly DeviceHealthTracker _tracker;

    public DeviceHealthTrackerTests()
    {
        _loggerMock = new Mock<ILogger<DeviceHealthTracker>>();
        _tracker = new DeviceHealthTracker(_loggerMock.Object);
    }

    [Fact]
    public void RecordSuccess_UpdatesHealthData()
    {
        // Arrange
        const string DeviceId = "TEST001";
        var duration = TimeSpan.FromMilliseconds(50);

        // Act
        _tracker.RecordSuccess(DeviceId, duration);
        var health = _tracker.GetDeviceHealth(DeviceId);

        // Assert
        health.DeviceId.Should().Be(DeviceId);
        health.IsConnected.Should().BeTrue();
        health.LastSuccessfulRead.Should().NotBeNull();
        health.ConsecutiveFailures.Should().Be(0);
        health.TotalReads.Should().Be(1);
        health.SuccessfulReads.Should().Be(1);
        health.SuccessRate.Should().Be(100.0);
    }

    [Fact]
    public void RecordFailure_UpdatesHealthData()
    {
        // Arrange
        const string DeviceId = "TEST001";
        const string Error = "Connection timeout";

        // Act
        _tracker.RecordFailure(DeviceId, Error);
        var health = _tracker.GetDeviceHealth(DeviceId);

        // Assert
        health.DeviceId.Should().Be(DeviceId);
        health.IsConnected.Should().BeTrue(); // Still connected until max failures
        health.ConsecutiveFailures.Should().Be(1);
        health.LastError.Should().Be(Error);
        health.TotalReads.Should().Be(1);
        health.SuccessfulReads.Should().Be(0);
        health.SuccessRate.Should().Be(0.0);
    }

    [Fact]
    public void RecordFailure_AfterMaxFailures_MarksOffline()
    {
        // Arrange
        const string DeviceId = "TEST001";
        const int MaxFailures = 5; // From Constants.MaxConsecutiveFailures

        // Act
        for (int i = 0; i < MaxFailures; i++)
        {
            _tracker.RecordFailure(DeviceId, $"Error {i}");
        }
        var health = _tracker.GetDeviceHealth(DeviceId);

        // Assert
        health.IsConnected.Should().BeFalse();
        health.IsOffline.Should().BeTrue();
        health.ConsecutiveFailures.Should().Be(MaxFailures);

        // Verify warning log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("marked as offline")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordSuccess_AfterFailures_ResetsConsecutiveFailures()
    {
        // Arrange
        const string DeviceId = "TEST001";

        // Act
        _tracker.RecordFailure(DeviceId, "Error 1");
        _tracker.RecordFailure(DeviceId, "Error 2");
        _tracker.RecordSuccess(DeviceId, TimeSpan.FromMilliseconds(50));
        var health = _tracker.GetDeviceHealth(DeviceId);

        // Assert
        health.IsConnected.Should().BeTrue();
        health.ConsecutiveFailures.Should().Be(0);
        health.TotalReads.Should().Be(3);
        health.SuccessfulReads.Should().Be(1);
        health.SuccessRate.Should().BeApproximately(33.33, 0.01);
    }

    [Fact]
    public void GetDeviceHealth_ForUnknownDevice_ReturnsDefaultHealth()
    {
        // Act
        var health = _tracker.GetDeviceHealth("UNKNOWN");

        // Assert
        health.DeviceId.Should().Be("UNKNOWN");
        health.IsConnected.Should().BeFalse();
        health.ConsecutiveFailures.Should().Be(0);
        health.TotalReads.Should().Be(0);
        health.SuccessfulReads.Should().Be(0);
    }

    [Fact]
    public void GetAllDeviceHealth_ReturnsAllDevices()
    {
        // Arrange
        _tracker.RecordSuccess("DEVICE1", TimeSpan.FromMilliseconds(50));
        _tracker.RecordFailure("DEVICE2", "Error");
        _tracker.RecordSuccess("DEVICE3", TimeSpan.FromMilliseconds(75));

        // Act
        var allHealth = _tracker.GetAllDeviceHealth();

        // Assert
        allHealth.Should().HaveCount(3);
        allHealth.Should().ContainKeys("DEVICE1", "DEVICE2", "DEVICE3");
        allHealth["DEVICE1"].IsConnected.Should().BeTrue();
        allHealth["DEVICE2"].ConsecutiveFailures.Should().Be(1);
        allHealth["DEVICE3"].IsConnected.Should().BeTrue();
    }

    [Fact]
    public void GetAverageResponseTime_WithData_ReturnsAverage()
    {
        // Arrange
        const string DeviceId = "TEST001";
        _tracker.RecordSuccess(DeviceId, TimeSpan.FromMilliseconds(50));
        _tracker.RecordSuccess(DeviceId, TimeSpan.FromMilliseconds(100));
        _tracker.RecordSuccess(DeviceId, TimeSpan.FromMilliseconds(75));

        // Act
        var avgTime = _tracker.GetAverageResponseTime(DeviceId);

        // Assert
        avgTime.Should().NotBeNull();
        avgTime.Should().BeApproximately(75.0, 0.01); // (50 + 100 + 75) / 3 = 75
    }

    [Fact]
    public void ResetDeviceHealth_ClearsData()
    {
        // Arrange
        const string DeviceId = "TEST001";
        _tracker.RecordSuccess(DeviceId, TimeSpan.FromMilliseconds(50));
        _tracker.RecordFailure(DeviceId, "Error");

        // Act
        _tracker.ResetDeviceHealth(DeviceId);
        var health = _tracker.GetDeviceHealth(DeviceId);

        // Assert
        health.TotalReads.Should().Be(0);
        health.SuccessfulReads.Should().Be(0);
        health.ConsecutiveFailures.Should().Be(0);
        health.LastError.Should().BeNull();
    }
}
