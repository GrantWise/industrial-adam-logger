using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Devices;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Devices;

public class ModbusDeviceConnectionTests : IDisposable
{
    private readonly Mock<ILogger<ModbusDeviceConnection>> _loggerMock;
    private readonly DeviceConfig _testConfig;
    private ModbusDeviceConnection? _connection;

    public ModbusDeviceConnectionTests()
    {
        _loggerMock = new Mock<ILogger<ModbusDeviceConnection>>();
        _testConfig = new DeviceConfig
        {
            DeviceId = "TEST001",
            IpAddress = "127.0.0.1", // Localhost for testing
            Port = 5020, // Non-standard port to avoid conflicts
            UnitId = 1,
            TimeoutMs = 1000,
            MaxRetries = 3,
            PollIntervalMs = 1000,
            KeepAlive = true,
            Channels = new List<ChannelConfig>
            {
                new ChannelConfig
                {
                    ChannelNumber = 0,
                    Name = "Test Channel",
                    StartRegister = 0,
                    RegisterCount = 2
                }
            }
        };
    }

    [Fact]
    public void Constructor_WithValidConfig_InitializesProperties()
    {
        // Act
        _connection = new ModbusDeviceConnection(_testConfig, _loggerMock.Object);

        // Assert
        _connection.DeviceId.Should().Be("TEST001");
        _connection.IsConnected.Should().BeFalse();
        _connection.Configuration.Should().Be(_testConfig);
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModbusDeviceConnection(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("config");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModbusDeviceConnection(_testConfig, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task ConnectAsync_ToNonExistentDevice_ReturnsFalse()
    {
        // Arrange
        _connection = new ModbusDeviceConnection(_testConfig, _loggerMock.Object);

        // Act
        var result = await _connection.ConnectAsync();

        // Assert
        result.Should().BeFalse();
        _connection.IsConnected.Should().BeFalse();

        // Verify logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to connect")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_WithConnectionThrottling_RespectseCooldown()
    {
        // Arrange
        _connection = new ModbusDeviceConnection(_testConfig, _loggerMock.Object);

        // Act - First attempt
        var result1 = await _connection.ConnectAsync();

        // Act - Immediate second attempt (should be throttled)
        var result2 = await _connection.ConnectAsync();

        // Assert
        result1.Should().BeFalse(); // Failed connection
        result2.Should().BeFalse(); // Throttled

        // Verify only one connection attempt was made
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to connect")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once); // Only one attempt due to throttling
    }

    [Fact]
    public async Task ReadRegistersAsync_WhenNotConnected_AttemptsConnection()
    {
        // Arrange
        _connection = new ModbusDeviceConnection(_testConfig, _loggerMock.Object);

        // Act
        var result = await _connection.ReadRegistersAsync(0, 2);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenNotConnected_ReturnsFalse()
    {
        // Arrange
        _connection = new ModbusDeviceConnection(_testConfig, _loggerMock.Object);

        // Act
        var result = await _connection.TestConnectionAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_DisconnectsCleanly()
    {
        // Arrange
        _connection = new ModbusDeviceConnection(_testConfig, _loggerMock.Object);

        // Act
        await _connection.DisconnectAsync();

        // Assert
        _connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        _connection = new ModbusDeviceConnection(_testConfig, _loggerMock.Object);

        // Act
        _connection.Dispose();

        // Assert - Should not throw
        var act = () => _connection.Dispose(); // Double dispose
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
