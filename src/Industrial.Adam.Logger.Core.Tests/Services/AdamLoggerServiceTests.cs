using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Devices;
using Industrial.Adam.Logger.Core.Models;
using Industrial.Adam.Logger.Core.Processing;
using Industrial.Adam.Logger.Core.Services;
using Industrial.Adam.Logger.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Services;

public class AdamLoggerServiceTests : IDisposable
{
    private readonly Mock<ILogger<AdamLoggerService>> _loggerMock;
    private readonly Mock<IOptions<LoggerConfiguration>> _configMock;
    private readonly ModbusDevicePool _devicePool;
    private readonly DeviceHealthTracker _healthTracker;
    private readonly Mock<IDataProcessor> _dataProcessorMock;
    private readonly Mock<ITimescaleStorage> _timescaleStorageMock;
    private readonly LoggerConfiguration _testConfig;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private AdamLoggerService? _service;

    public AdamLoggerServiceTests()
    {
        _loggerMock = new Mock<ILogger<AdamLoggerService>>();
        _configMock = new Mock<IOptions<LoggerConfiguration>>();

        // Create real instances instead of mocks for sealed classes
        var healthTrackerLogger = new Mock<ILogger<DeviceHealthTracker>>();
        _healthTracker = new DeviceHealthTracker(healthTrackerLogger.Object);

        var devicePoolLogger = new Mock<ILogger<ModbusDevicePool>>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(() => new Mock<ILogger>().Object);

        _devicePool = new ModbusDevicePool(
            devicePoolLogger.Object,
            _loggerFactoryMock.Object,
            _healthTracker);

        _dataProcessorMock = new Mock<IDataProcessor>();
        _timescaleStorageMock = new Mock<ITimescaleStorage>();

        _testConfig = new LoggerConfiguration
        {
            Devices = new List<DeviceConfig>
            {
                new DeviceConfig
                {
                    DeviceId = "TEST001",
                    Name = "Test Device",
                    IpAddress = "192.168.1.10",
                    Port = 502,
                    UnitId = 1,
                    Enabled = true,
                    Channels = new List<ChannelConfig>
                    {
                        new ChannelConfig
                        {
                            ChannelNumber = 0,
                            Name = "Test Channel",
                            StartRegister = 0,
                            RegisterCount = 2,
                            Enabled = true
                        }
                    }
                }
            },
            TimescaleDb = new TimescaleSettings
            {
                Host = "localhost",
                Port = 5432,
                Database = "adam_counters",
                Username = "adam_user",
                Password = "adam_password",
                TableName = "counter_data_test"
            }
        };

        _configMock.Setup(x => x.Value).Returns(_testConfig);
    }

    [Fact]
    public void Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Act
        _service = CreateService();

        // Assert
        _service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AdamLoggerService(
            null!,
            _configMock.Object,
            _devicePool,
            _healthTracker,
            _dataProcessorMock.Object,
            _timescaleStorageMock.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task StartAsync_WithValidConfiguration_StartsSuccessfully()
    {
        // Arrange
        _service = CreateService();
        _timescaleStorageMock.Setup(x => x.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Since we're using a real ModbusDevicePool, we need to mock the IModbusDeviceConnection
        // that it will create. For this test, we'll check the device count after adding

        // Act
        await _service.StartAsync(CancellationToken.None);

        // Assert
        _timescaleStorageMock.Verify(x => x.TestConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Check that device was added to pool (we can't verify the method call since it's not mocked)
        _devicePool.DeviceCount.Should().Be(1);

        // Verify info logs
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Starting ADAM Logger Service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WithInvalidConfiguration_ThrowsException()
    {
        // Arrange
        _testConfig.Devices.Clear(); // Invalid - no devices
        _service = CreateService();

        // Act & Assert
        var act = async () => await _service.StartAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid configuration*");
    }

    [Fact]
    public async Task StartAsync_WithInfluxDbConnectionFailure_ThrowsException()
    {
        // Arrange
        _service = CreateService();
        _timescaleStorageMock.Setup(x => x.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var act = async () => await _service.StartAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to connect to TimescaleDB*");
    }

    [Fact]
    public async Task StopAsync_AfterStart_StopsGracefully()
    {
        // Arrange
        _service = CreateService();
        _timescaleStorageMock.Setup(x => x.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _timescaleStorageMock.Setup(x => x.ForceFlushAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.StartAsync(CancellationToken.None);

        // Act
        await _service.StopAsync(CancellationToken.None);

        // Assert
        // Since we're using a real device pool, we check its state after stop
        _devicePool.DeviceCount.Should().Be(1); // Devices remain registered but stopped
        _timescaleStorageMock.Verify(x => x.ForceFlushAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Verify info logs
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Stopping ADAM Logger Service")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetStatus_ReturnsCurrentStatus()
    {
        // Arrange
        _service = CreateService();

        // Update health tracker with test data (ensure device stays connected by ending with successes)
        for (int i = 0; i < 100; i++)
        {
            if (i < 90 || i >= 95) // 95 successful (90 + last 5), 5 failed (middle failures)
                _healthTracker.RecordSuccess("TEST001", TimeSpan.FromMilliseconds(10));
            else
                _healthTracker.RecordFailure("TEST001", "Test failure");
        }

        // Act
        var status = _service.GetStatus();

        // Assert
        status.Should().NotBeNull();
        status.TotalDevices.Should().Be(0); // No devices added yet in this test
        status.ConnectedDevices.Should().Be(1); // But health tracker has one device
        status.DeviceHealth.Should().HaveCount(1);
        status.DeviceHealth["TEST001"].SuccessfulReads.Should().Be(95);
    }

    [Fact]
    public async Task AddDeviceAsync_WithValidConfig_AddsDevice()
    {
        // Arrange
        _service = CreateService();
        var newDevice = new DeviceConfig
        {
            DeviceId = "TEST002",
            Name = "New Device",
            IpAddress = "192.168.1.11",
            Port = 502,
            UnitId = 2,
            Channels = new List<ChannelConfig>
            {
                new ChannelConfig
                {
                    ChannelNumber = 0,
                    Name = "Test Channel",
                    StartRegister = 0,
                    RegisterCount = 2,
                    Enabled = true
                }
            }
        };

        var initialCount = _devicePool.DeviceCount;

        // Act
        var result = await _service.AddDeviceAsync(newDevice);

        // Assert
        result.Should().BeTrue();
        _devicePool.DeviceCount.Should().Be(initialCount + 1);
    }

    [Fact]
    public async Task RemoveDeviceAsync_WithExistingDevice_RemovesDevice()
    {
        // Arrange
        _service = CreateService();
        _timescaleStorageMock.Setup(x => x.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Start service to add initial device
        await _service.StartAsync(CancellationToken.None);
        var initialCount = _devicePool.DeviceCount;

        // Act
        var result = await _service.RemoveDeviceAsync("TEST001");

        // Assert
        result.Should().BeTrue();
        _devicePool.DeviceCount.Should().Be(initialCount - 1);
    }

    [Fact]
    public async Task RestartDeviceAsync_WithExistingDevice_RestartsDevice()
    {
        // Arrange
        _service = CreateService();
        _timescaleStorageMock.Setup(x => x.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Start service to add initial device
        await _service.StartAsync(CancellationToken.None);

        // Act
        var result = await _service.RestartDeviceAsync("TEST001");

        // Assert
        result.Should().BeTrue();
        _devicePool.DeviceCount.Should().Be(1); // Device count should remain the same
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        _service = CreateService();

        // Act
        _service.Dispose();

        // Assert - Should not throw
        var act = () => _service.Dispose(); // Double dispose
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_AfterDispose_OperationsThrowObjectDisposedException()
    {
        // Arrange
        _service = CreateService();
        _service.Dispose();

        // Act & Assert
        var act1 = async () => await _service.AddDeviceAsync(new DeviceConfig());
        await act1.Should().ThrowAsync<ObjectDisposedException>();

        var act2 = async () => await _service.RemoveDeviceAsync("TEST001");
        await act2.Should().ThrowAsync<ObjectDisposedException>();

        var act3 = async () => await _service.RestartDeviceAsync("TEST001");
        await act3.Should().ThrowAsync<ObjectDisposedException>();
    }

    private AdamLoggerService CreateService()
    {
        return new AdamLoggerService(
            _loggerMock.Object,
            _configMock.Object,
            _devicePool,
            _healthTracker,
            _dataProcessorMock.Object,
            _timescaleStorageMock.Object);
    }

    public void Dispose()
    {
        _service?.Dispose();
        _devicePool?.Dispose();
    }
}
