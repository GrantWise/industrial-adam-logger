using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Devices;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Devices;

public class ModbusDevicePoolTests : IDisposable
{
    private readonly Mock<ILogger<ModbusDevicePool>> _loggerMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly DeviceHealthTracker _healthTracker;
    private readonly ModbusDevicePool _pool;

    public ModbusDevicePoolTests()
    {
        _loggerMock = new Mock<ILogger<ModbusDevicePool>>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();

        // Setup logger factory to return mocked loggers
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        var healthTrackerLogger = new Mock<ILogger<DeviceHealthTracker>>();
        _healthTracker = new DeviceHealthTracker(healthTrackerLogger.Object);

        _pool = new ModbusDevicePool(_loggerMock.Object, _loggerFactoryMock.Object, _healthTracker);
    }

    [Fact]
    public async Task AddDeviceAsync_WithValidConfig_AddsDevice()
    {
        // Arrange
        var config = CreateTestDeviceConfig("TEST001");

        // Act
        var result = await _pool.AddDeviceAsync(config);

        // Assert
        result.Should().BeTrue();
        _pool.DeviceCount.Should().Be(1);
        _pool.ActiveDeviceIds.Should().Contain("TEST001");
    }

    [Fact]
    public async Task AddDeviceAsync_WithDuplicateDevice_ReturnsFalse()
    {
        // Arrange
        var config = CreateTestDeviceConfig("TEST001");
        await _pool.AddDeviceAsync(config);

        // Act
        var result = await _pool.AddDeviceAsync(config);

        // Assert
        result.Should().BeFalse();
        _pool.DeviceCount.Should().Be(1);

        // Verify warning log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("already exists")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AddDeviceAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _pool.AddDeviceAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("config");
    }

    [Fact]
    public async Task RemoveDeviceAsync_WithExistingDevice_RemovesDevice()
    {
        // Arrange
        var config = CreateTestDeviceConfig("TEST001");
        await _pool.AddDeviceAsync(config);

        // Act
        var result = await _pool.RemoveDeviceAsync("TEST001");

        // Assert
        result.Should().BeTrue();
        _pool.DeviceCount.Should().Be(0);
        _pool.ActiveDeviceIds.Should().NotContain("TEST001");
    }

    [Fact]
    public async Task RemoveDeviceAsync_WithNonExistentDevice_ReturnsFalse()
    {
        // Act
        var result = await _pool.RemoveDeviceAsync("UNKNOWN");

        // Assert
        result.Should().BeFalse();

        // Verify warning log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RestartDeviceAsync_WithExistingDevice_ReturnsTrue()
    {
        // Arrange
        var config = CreateTestDeviceConfig("TEST001");
        await _pool.AddDeviceAsync(config);

        // Act
        var result = await _pool.RestartDeviceAsync("TEST001");

        // Assert
        result.Should().BeTrue();
        _pool.DeviceCount.Should().Be(1); // Device should still be in pool

        // Verify info log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Restarting device")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RestartDeviceAsync_WithNonExistentDevice_ReturnsFalse()
    {
        // Act
        var result = await _pool.RestartDeviceAsync("UNKNOWN");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsDeviceConnected_WithExistingDevice_ReturnsConnectionStatus()
    {
        // Arrange
        var config = CreateTestDeviceConfig("TEST001");
        await _pool.AddDeviceAsync(config);

        // Act
        var isConnected = _pool.IsDeviceConnected("TEST001");

        // Assert
        isConnected.Should().BeFalse(); // Not connected since we can't actually connect in test
    }

    [Fact]
    public void IsDeviceConnected_WithNonExistentDevice_ReturnsFalse()
    {
        // Act
        var isConnected = _pool.IsDeviceConnected("UNKNOWN");

        // Assert
        isConnected.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllDeviceHealth_ReturnsHealthForAllDevices()
    {
        // Arrange
        await _pool.AddDeviceAsync(CreateTestDeviceConfig("TEST001"));
        await _pool.AddDeviceAsync(CreateTestDeviceConfig("TEST002"));

        // Act
        var health = _pool.GetAllDeviceHealth();

        // Assert
        health.Should().BeEmpty(); // No health data yet since devices haven't polled
    }

    [Fact]
    public async Task StopAllAsync_StopsAllDevicePolling()
    {
        // Arrange
        await _pool.AddDeviceAsync(CreateTestDeviceConfig("TEST001"));
        await _pool.AddDeviceAsync(CreateTestDeviceConfig("TEST002"));

        // Act
        await _pool.StopAllAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Stopping all device polling")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReadingReceived_Event_FiredWhenDevicePolled()
    {
        // Arrange
        var config = CreateTestDeviceConfig("TEST001", pollIntervalMs: 100);
        DeviceReading? receivedReading = null;
        _pool.ReadingReceived += reading => receivedReading = reading;

        // Act
        await _pool.AddDeviceAsync(config);

        // Wait a bit for polling (won't actually connect in test)
        await Task.Delay(200);

        // Assert
        // With data integrity improvements, we now receive Unavailable readings when connection fails
        receivedReading.Should().NotBeNull("unavailable readings are now reported for transparency");
        receivedReading!.Quality.Should().Be(DataQuality.Unavailable, "device connection failed");
        receivedReading.DeviceId.Should().Be("TEST001");
    }

    [Fact]
    public async Task Dispose_DisposesAllResources()
    {
        // Arrange
        await _pool.AddDeviceAsync(CreateTestDeviceConfig("TEST001"));

        // Act
        _pool.Dispose();

        // Assert - Should not throw
        var act = () => _pool.Dispose(); // Double dispose
        act.Should().NotThrow();

        // Should throw ObjectDisposedException on operations after dispose
        var addAct = async () => await _pool.AddDeviceAsync(CreateTestDeviceConfig("TEST002"));
        await addAct.Should().ThrowAsync<ObjectDisposedException>();
    }

    private DeviceConfig CreateTestDeviceConfig(string deviceId, int pollIntervalMs = 1000)
    {
        return new DeviceConfig
        {
            DeviceId = deviceId,
            IpAddress = "127.0.0.1",
            Port = 5020,
            UnitId = 1,
            TimeoutMs = 1000,
            MaxRetries = 3,
            PollIntervalMs = pollIntervalMs,
            KeepAlive = true,
            Channels = new List<ChannelConfig>
            {
                new ChannelConfig
                {
                    ChannelNumber = 0,
                    Name = "Test Channel",
                    StartRegister = 0,
                    RegisterCount = 2,
                    ScaleFactor = 1.0
                }
            }
        };
    }

    public void Dispose()
    {
        _pool?.Dispose();
    }
}
