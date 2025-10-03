using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Devices;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Integration;

/// <summary>
/// Integration tests for ModbusDevicePool focusing on pool management behavior
/// </summary>
public class DevicePoolIntegrationTests : IDisposable
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly DeviceHealthTracker _healthTracker;
    private readonly ModbusDevicePool _pool;

    public DevicePoolIntegrationTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();

        // Setup logger factory to return mocked loggers
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        var poolLoggerMock = new Mock<ILogger<ModbusDevicePool>>();
        var healthLoggerMock = new Mock<ILogger<DeviceHealthTracker>>();

        _healthTracker = new DeviceHealthTracker(healthLoggerMock.Object);
        _pool = new ModbusDevicePool(poolLoggerMock.Object, _loggerFactoryMock.Object, _healthTracker);
    }

    [Fact]
    public async Task DevicePool_AddMultipleDevices_ManagesThemConcurrently()
    {
        // Arrange
        var configs = new[]
        {
            CreateConfig("DEVICE1", 5021),
            CreateConfig("DEVICE2", 5022),
            CreateConfig("DEVICE3", 5023)
        };

        // Act
        var results = await Task.WhenAll(configs.Select(c => _pool.AddDeviceAsync(c)));

        // Assert
        results.Should().AllBeEquivalentTo(true);
        _pool.DeviceCount.Should().Be(3);
        _pool.ActiveDeviceIds.Should().BeEquivalentTo(new[] { "DEVICE1", "DEVICE2", "DEVICE3" });
    }

    [Fact]
    public async Task DevicePool_RemoveDeviceWhilePolling_StopsPollingGracefully()
    {
        // Arrange
        var config = CreateConfig("REMOVE001", 5024, pollIntervalMs: 50);
        await _pool.AddDeviceAsync(config);

        // Let it poll for a bit
        await Task.Delay(200);

        // Act
        var removeResult = await _pool.RemoveDeviceAsync("REMOVE001");

        // Assert
        removeResult.Should().BeTrue();
        _pool.DeviceCount.Should().Be(0);
        _pool.IsDeviceConnected("REMOVE001").Should().BeFalse();

        // Verify health data was reset
        var health = _healthTracker.GetDeviceHealth("REMOVE001");
        health.TotalReads.Should().Be(0);
    }

    [Fact]
    public async Task DevicePool_WithFailingConnections_RecordsHealthMetrics()
    {
        // Arrange - Device with invalid address will fail to connect
        var config = new DeviceConfig
        {
            DeviceId = "FAIL001",
            IpAddress = "192.168.255.255", // Likely unreachable
            Port = 502,
            UnitId = 1,
            TimeoutMs = 100, // Short timeout for faster test
            MaxRetries = 1,
            PollIntervalMs = 100,
            KeepAlive = false,
            Channels = new List<ChannelConfig>
            {
                new ChannelConfig
                {
                    ChannelNumber = 0,
                    Name = "Counter",
                    StartRegister = 0,
                    RegisterCount = 2,
                    ScaleFactor = 1.0
                }
            }
        };

        // Act
        await _pool.AddDeviceAsync(config);

        // Wait for a few poll attempts
        await Task.Delay(500);

        // Assert
        var health = _healthTracker.GetDeviceHealth("FAIL001");
        health.ConsecutiveFailures.Should().BeGreaterThan(0);
        health.LastError.Should().NotBeNullOrEmpty();
        health.SuccessfulReads.Should().Be(0);
    }

    [Fact]
    public async Task DevicePool_RestartDevice_ContinuesPolling()
    {
        // Arrange
        var config = CreateConfig("RESTART001", 5025, pollIntervalMs: 100);
        await _pool.AddDeviceAsync(config);

        // Wait for initial polling
        await Task.Delay(200);

        // Act
        var restartResult = await _pool.RestartDeviceAsync("RESTART001");

        // Wait for polling to resume
        await Task.Delay(200);

        // Assert
        restartResult.Should().BeTrue();
        _pool.DeviceCount.Should().Be(1);
        _pool.ActiveDeviceIds.Should().Contain("RESTART001");
    }

    [Fact]
    public async Task DevicePool_StopAll_StopsAllDevicesGracefully()
    {
        // Arrange
        await _pool.AddDeviceAsync(CreateConfig("STOP001", 5026));
        await _pool.AddDeviceAsync(CreateConfig("STOP002", 5027));
        await _pool.AddDeviceAsync(CreateConfig("STOP003", 5028));

        // Act
        await _pool.StopAllAsync();

        // Wait a bit to ensure stopping completes
        await Task.Delay(100);

        // Assert
        _pool.DeviceCount.Should().Be(3); // Devices still in pool but stopped
        _pool.IsDeviceConnected("STOP001").Should().BeFalse();
        _pool.IsDeviceConnected("STOP002").Should().BeFalse();
        _pool.IsDeviceConnected("STOP003").Should().BeFalse();
    }

    [Fact]
    public async Task DevicePool_ConcurrentOperations_HandledSafely()
    {
        // Arrange
        var tasks = new List<Task<bool>>();

        // Act - Perform multiple operations concurrently
        for (int i = 0; i < 10; i++)
        {
            var deviceId = $"CONCURRENT{i:D3}";
            tasks.Add(_pool.AddDeviceAsync(CreateConfig(deviceId, 5030 + i)));
        }

        var results = await Task.WhenAll(tasks);

        // Also try some removals concurrently
        var removeTasks = new List<Task<bool>>
        {
            _pool.RemoveDeviceAsync("CONCURRENT002"),
            _pool.RemoveDeviceAsync("CONCURRENT005"),
            _pool.RemoveDeviceAsync("CONCURRENT008")
        };

        var removeResults = await Task.WhenAll(removeTasks);

        // Assert
        results.Should().AllBeEquivalentTo(true);
        removeResults.Should().AllBeEquivalentTo(true);
        _pool.DeviceCount.Should().Be(7); // 10 added - 3 removed
    }

    [Fact]
    public async Task DevicePool_Dispose_CleansUpAllResources()
    {
        // Arrange
        await _pool.AddDeviceAsync(CreateConfig("DISPOSE001", 5040));
        await _pool.AddDeviceAsync(CreateConfig("DISPOSE002", 5041));

        // Act
        _pool.Dispose();

        // Assert - Operations after dispose should throw
        var act = async () => await _pool.AddDeviceAsync(CreateConfig("AFTERDISPOSE", 5042));
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    private DeviceConfig CreateConfig(string deviceId, int port, int pollIntervalMs = 1000)
    {
        return new DeviceConfig
        {
            DeviceId = deviceId,
            IpAddress = "127.0.0.1",
            Port = port,
            UnitId = 1,
            TimeoutMs = 1000,
            MaxRetries = 2,
            PollIntervalMs = pollIntervalMs,
            KeepAlive = false,
            Channels = new List<ChannelConfig>
            {
                new ChannelConfig
                {
                    ChannelNumber = 0,
                    Name = "Counter",
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
