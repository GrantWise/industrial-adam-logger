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
using Npgsql;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Integration;

/// <summary>
/// End-to-end integration tests validating complete data flow:
/// ADAM Simulator → Modbus TCP → Service → Processing → TimescaleDB
///
/// Prerequisites:
/// - TimescaleDB running on localhost:5433
/// - ADAM simulator project built (will be started by tests)
/// </summary>
[Collection("E2E")]
public class EndToEndTests : IAsyncLifetime
{
    private readonly E2ETestFixture _fixture;
    private readonly string _testTableName;

    public EndToEndTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
        _testTableName = $"counter_data_e2e_{Guid.NewGuid():N}";
    }

    public Task InitializeAsync()
    {
        // Clean up test table before each test
        return CleanupTestTableAsync();
    }

    public Task DisposeAsync()
    {
        // Clean up test table after each test
        return CleanupTestTableAsync();
    }

    [Fact]
    public async Task SingleSimulator_CollectsDataAndStoresInTimescaleDB()
    {
        // Arrange - Start one simulator
        var simulator = await _fixture.StartSimulatorAsync(1, 5510, 8090);

        var config = CreateLoggerConfiguration(
            deviceId: "E2E-SIM-01",
            modbusPort: 5510,
            pollIntervalMs: 1000);

        using var service = await CreateAndStartServiceAsync(config);

        // Act - Let it collect data for a few seconds
        await Task.Delay(5000);

        // Assert - Verify data in TimescaleDB
        var readings = await GetReadingsFromDatabaseAsync("E2E-SIM-01");

        readings.Should().NotBeEmpty("simulator should have produced data");
        readings.Count.Should().BeGreaterThan(3, "should have multiple readings over 5 seconds");

        // Verify data quality
        readings.Should().AllSatisfy(r =>
        {
            r.Quality.Should().Be(DataQuality.Good, "simulator data should be good quality");
            r.DeviceId.Should().Be("E2E-SIM-01");
            r.ProcessedValue.Should().BeGreaterThan(0, "counter should increment");
        });

        // Verify rate calculation exists
        var readingsWithRate = readings.Where(r => r.Rate.HasValue).ToList();
        readingsWithRate.Should().NotBeEmpty("windowed rate calculation should produce rates");

        // Cleanup
        await _fixture.StopSimulatorAsync(simulator);
    }

    [Fact]
    public async Task MultipleSimulators_CollectDataConcurrently()
    {
        // Arrange - Start 3 simulators
        var sim1 = await _fixture.StartSimulatorAsync(1, 5511, 8091);
        var sim2 = await _fixture.StartSimulatorAsync(2, 5512, 8092);
        var sim3 = await _fixture.StartSimulatorAsync(3, 5513, 8093);

        var config = new LoggerConfiguration
        {
            GlobalPollIntervalMs = 1000,
            HealthCheckIntervalMs = 5000,
            Devices = new List<DeviceConfig>
            {
                CreateDeviceConfig("E2E-SIM-01", 5511),
                CreateDeviceConfig("E2E-SIM-02", 5512),
                CreateDeviceConfig("E2E-SIM-03", 5513)
            },
            TimescaleDb = CreateTimescaleSettings()
        };

        using var service = await CreateAndStartServiceAsync(config);

        // Act - Let all simulators collect data
        await Task.Delay(6000);

        // Assert - Verify each device has data
        var readings1 = await GetReadingsFromDatabaseAsync("E2E-SIM-01");
        var readings2 = await GetReadingsFromDatabaseAsync("E2E-SIM-02");
        var readings3 = await GetReadingsFromDatabaseAsync("E2E-SIM-03");

        readings1.Should().NotBeEmpty("simulator 1 should have data");
        readings2.Should().NotBeEmpty("simulator 2 should have data");
        readings3.Should().NotBeEmpty("simulator 3 should have data");

        // Verify independence - each device tracked separately
        readings1.Should().AllSatisfy(r => r.DeviceId.Should().Be("E2E-SIM-01"));
        readings2.Should().AllSatisfy(r => r.DeviceId.Should().Be("E2E-SIM-02"));
        readings3.Should().AllSatisfy(r => r.DeviceId.Should().Be("E2E-SIM-03"));

        // Verify all have good quality
        var allReadings = readings1.Concat(readings2).Concat(readings3).ToList();
        allReadings.Should().AllSatisfy(r =>
            r.Quality.Should().Be(DataQuality.Good, "all simulator data should be good quality"));

        // Cleanup
        await _fixture.StopSimulatorAsync(sim1);
        await _fixture.StopSimulatorAsync(sim2);
        await _fixture.StopSimulatorAsync(sim3);
    }

    [Fact]
    public async Task SimulatorOffline_ReportsUnavailableQuality()
    {
        // Arrange - Configure service to connect to non-existent simulator
        var config = CreateLoggerConfiguration(
            deviceId: "E2E-OFFLINE",
            modbusPort: 5599, // No simulator on this port
            pollIntervalMs: 1000,
            timeoutMs: 500,
            maxRetries: 1);

        using var service = await CreateAndStartServiceAsync(config);

        // Act - Let it attempt to collect data
        await Task.Delay(3000);

        // Assert - Should have Unavailable readings (21 CFR Part 11 compliance)
        var readings = await GetReadingsFromDatabaseAsync("E2E-OFFLINE");

        readings.Should().NotBeEmpty("should report unavailable readings for transparency");
        readings.Should().AllSatisfy(r =>
            r.Quality.Should().Be(DataQuality.Unavailable, "offline device should report unavailable"));
    }

    [Fact]
    public async Task SimulatorRestart_ServiceReconnectsAndResumesCollection()
    {
        // Arrange - Start simulator
        var simulator = await _fixture.StartSimulatorAsync(1, 5514, 8094);

        var config = CreateLoggerConfiguration(
            deviceId: "E2E-RESTART",
            modbusPort: 5514,
            pollIntervalMs: 1000);

        using var service = await CreateAndStartServiceAsync(config);

        // Collect initial data (wait longer for simulator startup + first poll + processing + DB write)
        await Task.Delay(6000);
        var initialReadings = await GetReadingsFromDatabaseAsync("E2E-RESTART");
        initialReadings.Should().NotBeEmpty("should have initial readings");

        // Act - Stop and restart simulator
        await _fixture.StopSimulatorAsync(simulator);
        await Task.Delay(2000); // Wait for service to detect disconnection

        simulator = await _fixture.StartSimulatorAsync(1, 5514, 8094);
        await Task.Delay(4000); // Wait for reconnection and new data

        // Assert - Should have new readings after restart
        var allReadings = await GetReadingsFromDatabaseAsync("E2E-RESTART");
        allReadings.Count.Should().BeGreaterThan(initialReadings.Count, "should continue collecting after restart");

        // Should have good readings (before and after restart)
        var goodReadings = allReadings.Where(r => r.Quality == DataQuality.Good).ToList();
        goodReadings.Should().NotBeEmpty("should have good readings before and after restart");

        // May have unavailable readings during disconnect (timing-dependent, so we don't assert on it)
        // The important validation is that service reconnected and continued collecting
        var unavailableReadings = allReadings.Where(r => r.Quality == DataQuality.Unavailable).ToList();

        // Cleanup
        await _fixture.StopSimulatorAsync(simulator);
    }

    [Fact]
    public async Task CounterOverflow_WindowedRateCalculationHandlesCorrectly()
    {
        // Arrange - Start simulator with fast rate to cause overflow sooner
        var simulator = await _fixture.StartSimulatorAsync(1, 5515, 8095, baseRate: 600.0);

        var config = CreateLoggerConfiguration(
            deviceId: "E2E-OVERFLOW",
            modbusPort: 5515,
            pollIntervalMs: 500);

        using var service = await CreateAndStartServiceAsync(config);

        // Act - Collect data for longer period to potentially see overflow
        await Task.Delay(10000);

        // Assert - Rate calculations should be consistent
        var readings = await GetReadingsFromDatabaseAsync("E2E-OVERFLOW");
        var readingsWithRate = readings.Where(r => r.Rate.HasValue).ToList();

        readingsWithRate.Should().NotBeEmpty("should have rate calculations");

        // Rate should be reasonable (within expected range for 600 units/min = 10 units/sec)
        readingsWithRate.Should().AllSatisfy(r =>
        {
            r.Rate!.Value.Should().BeInRange(-20, 20, "rate should be reasonable even with overflow");
        });

        // Cleanup
        await _fixture.StopSimulatorAsync(simulator);
    }

    [Fact]
    public async Task DataProcessor_PreservesUnavailableQuality()
    {
        // Arrange - This tests the data integrity requirement that unavailable readings
        // should not be processed or have rates calculated
        var config = CreateLoggerConfiguration(
            deviceId: "E2E-QUALITY",
            modbusPort: 5598, // Non-existent port
            pollIntervalMs: 1000,
            timeoutMs: 500,
            maxRetries: 1);

        using var service = await CreateAndStartServiceAsync(config);

        // Act
        await Task.Delay(3000);

        // Assert
        var readings = await GetReadingsFromDatabaseAsync("E2E-QUALITY");

        readings.Should().NotBeEmpty();
        readings.Should().AllSatisfy(r =>
        {
            r.Quality.Should().Be(DataQuality.Unavailable);
            r.Rate.Should().BeNull("unavailable readings should not have rate calculations");
        });
    }

    // Helper Methods

    private LoggerConfiguration CreateLoggerConfiguration(
        string deviceId,
        int modbusPort,
        int pollIntervalMs = 1000,
        int timeoutMs = 3000,
        int maxRetries = 2)
    {
        return new LoggerConfiguration
        {
            GlobalPollIntervalMs = pollIntervalMs,
            HealthCheckIntervalMs = 5000,
            Devices = new List<DeviceConfig>
            {
                CreateDeviceConfig(deviceId, modbusPort, pollIntervalMs, timeoutMs, maxRetries)
            },
            TimescaleDb = CreateTimescaleSettings()
        };
    }

    private DeviceConfig CreateDeviceConfig(
        string deviceId,
        int modbusPort,
        int pollIntervalMs = 1000,
        int timeoutMs = 3000,
        int maxRetries = 2)
    {
        return new DeviceConfig
        {
            DeviceId = deviceId,
            IpAddress = "127.0.0.1",
            Port = modbusPort,
            UnitId = 1,
            TimeoutMs = timeoutMs,
            MaxRetries = maxRetries,
            PollIntervalMs = pollIntervalMs,
            KeepAlive = true,
            Channels = new List<ChannelConfig>
            {
                new ChannelConfig
                {
                    ChannelNumber = 0,
                    Name = "ProductionCounter",
                    StartRegister = 0,
                    RegisterCount = 2,
                    ScaleFactor = 1.0,
                    MaxChangeRate = 1000,
                    RateWindowSeconds = 30
                }
            }
        };
    }

    private TimescaleSettings CreateTimescaleSettings()
    {
        return new TimescaleSettings
        {
            Host = "localhost",
            Port = 5433,
            Database = "adam_counters",
            Username = "adam_user",
            Password = "adam_password",
            TableName = _testTableName,
            BatchSize = 10,
            FlushIntervalMs = 1000,
            MaxRetryAttempts = 2,
            RetryDelayMs = 500,
            EnableDeadLetterQueue = true,
            Tags = new Dictionary<string, string>
            {
                ["test"] = "e2e",
                ["table"] = _testTableName
            }
        };
    }

    private async Task<AdamLoggerService> CreateAndStartServiceAsync(LoggerConfiguration config)
    {
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        var serviceLogger = new Mock<ILogger<AdamLoggerService>>();
        var poolLogger = new Mock<ILogger<ModbusDevicePool>>();
        var storageLogger = new Mock<ILogger<TimescaleStorage>>();
        var processorLogger = new Mock<ILogger<DataProcessor>>();
        var healthLogger = new Mock<ILogger<DeviceHealthTracker>>();

        var healthTracker = new DeviceHealthTracker(healthLogger.Object);
        var pool = new ModbusDevicePool(poolLogger.Object, loggerFactory.Object, healthTracker);
        var storage = new TimescaleStorage(storageLogger.Object, config.TimescaleDb);
        var processor = new DataProcessor(processorLogger.Object, config);

        // Wrap configuration in IOptions
        var configOptions = Options.Create(config);

        var service = new AdamLoggerService(
            serviceLogger.Object,
            configOptions,
            pool,
            healthTracker,
            processor,
            storage);

        await service.StartAsync(CancellationToken.None);

        return service;
    }

    private async Task<List<DeviceReading>> GetReadingsFromDatabaseAsync(string deviceId)
    {
        var connectionString = $"Host=localhost;Port=5433;Database=adam_counters;Username=adam_user;Password=adam_password";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var sql = $@"
            SELECT device_id, channel, timestamp, raw_value, processed_value, rate, quality
            FROM {_testTableName}
            WHERE device_id = @deviceId
            ORDER BY timestamp ASC";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("deviceId", deviceId);

        var readings = new List<DeviceReading>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            // Parse quality from text (stored as "Good", "Unavailable", etc.)
            var qualityText = reader.IsDBNull(6) ? "Good" : reader.GetString(6);
            var quality = Enum.Parse<DataQuality>(qualityText);

            readings.Add(new DeviceReading
            {
                DeviceId = reader.GetString(0),
                Channel = reader.GetInt32(1),
                Timestamp = reader.GetDateTime(2),
                RawValue = reader.GetInt64(3),
                ProcessedValue = reader.GetDouble(4),
                Rate = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                Quality = quality
            });
        }

        return readings;
    }

    private async Task CleanupTestTableAsync()
    {
        try
        {
            var connectionString = $"Host=localhost;Port=5433;Database=adam_counters;Username=adam_user;Password=adam_password";

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var sql = $"DROP TABLE IF EXISTS {_testTableName} CASCADE";
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
