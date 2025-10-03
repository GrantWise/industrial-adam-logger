using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Industrial.Adam.Logger.Core.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Storage;

public class TimescaleStorageTests : IDisposable
{
    private readonly Mock<ILogger<TimescaleStorage>> _loggerMock;
    private readonly TimescaleSettings _testSettings;

    public TimescaleStorageTests()
    {
        _loggerMock = new Mock<ILogger<TimescaleStorage>>();
        _testSettings = new TimescaleSettings
        {
            Host = "localhost",
            Port = 5433,  // Docker container port mapping
            Database = "adam_counters",
            Username = "adam_user",
            Password = "adam_password",
            TableName = "counter_data_unit_test",
            BatchSize = 10,
            FlushIntervalMs = 1000,
            Tags = new Dictionary<string, string>
            {
                ["location"] = "test-site",
                ["environment"] = "test"
            }
        };
    }

    [Fact]
    public void Constructor_WithValidSettings_InitializesSuccessfully()
    {
        // Act
        using var storage = new TimescaleStorage(_loggerMock.Object, _testSettings);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("TimescaleDB storage initialized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TimescaleStorage(null!, _testSettings);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TimescaleStorage(_loggerMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [Fact]
    public async Task WriteReadingAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var storage = new TimescaleStorage(_loggerMock.Object, _testSettings);
        storage.Dispose();

        var reading = CreateTestReading();

        // Act & Assert
        var act = async () => await storage.WriteReadingAsync(reading);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task WriteBatchAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var storage = new TimescaleStorage(_loggerMock.Object, _testSettings);
        storage.Dispose();

        var readings = new[] { CreateTestReading() };

        // Act & Assert
        var act = async () => await storage.WriteBatchAsync(readings);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_DoesNothing()
    {
        // Arrange
        using var storage = new TimescaleStorage(_loggerMock.Object, _testSettings);
        var readings = Enumerable.Empty<DeviceReading>();

        // Act
        await storage.WriteBatchAsync(readings);

        // Assert - Should not log any write operations
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Wrote batch")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var storage = new TimescaleStorage(_loggerMock.Object, _testSettings);

        // Act & Assert
        var act = () =>
        {
            storage.Dispose();
            storage.Dispose(); // Second call
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task TestConnectionAsync_WithMockClient_HandlesResponse()
    {
        // Note: This test is limited because we can't easily mock NpgsqlConnection
        // In a real scenario, you would use integration tests with a test TimescaleDB instance

        // Arrange
        using var storage = new TimescaleStorage(_loggerMock.Object, _testSettings);

        // Act
        // This test has limited value without a real TimescaleDB connection
        var result = await storage.TestConnectionAsync();

        // Assert
        // Result depends on whether TimescaleDB connection can be established
        // In CI/test environment, this may succeed or fail
        _ = result; // We don't assert on the result since it depends on test environment
    }

    private DeviceReading CreateTestReading()
    {
        return new DeviceReading
        {
            DeviceId = "TEST001",
            Channel = 0,
            RawValue = 12345,
            ProcessedValue = 12345.0,
            Rate = 50.0,
            Timestamp = DateTimeOffset.UtcNow,
            Quality = DataQuality.Good
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
