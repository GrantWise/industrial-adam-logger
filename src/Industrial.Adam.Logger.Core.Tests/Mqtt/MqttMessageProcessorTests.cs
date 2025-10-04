using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Industrial.Adam.Logger.Core.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Mqtt;

public class MqttMessageProcessorTests
{
    private readonly MqttMessageProcessor _processor;

    public MqttMessageProcessorTests()
    {
        _processor = new MqttMessageProcessor(NullLogger<MqttMessageProcessor>.Instance);
    }

    [Fact]
    public void ProcessMessage_JsonPayload_ValidMessage_ReturnsReading()
    {
        // Arrange
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST001",
            Format = PayloadFormat.Json,
            DataType = MqttDataType.UInt32,
            DeviceIdJsonPath = "$.device_id",
            ChannelJsonPath = "$.channel",
            ValueJsonPath = "$.value"
        };

        var json = "{\"device_id\":\"TEST001\",\"channel\":0,\"value\":12345}";
        var payload = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().NotBeNull();
        result!.DeviceId.Should().Be("TEST001");
        result.Channel.Should().Be(0);
        result.RawValue.Should().Be(12345);
        result.Quality.Should().Be(DataQuality.Good);
    }

    [Fact]
    public void ProcessMessage_JsonPayload_MissingValue_ReturnsNull()
    {
        // Arrange
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST001",
            Format = PayloadFormat.Json,
            ValueJsonPath = "$.value"
        };

        var json = "{\"channel\":0}"; // Missing value
        var payload = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ProcessMessage_JsonPayload_InvalidJson_ReturnsNull()
    {
        // Arrange
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST001",
            Format = PayloadFormat.Json
        };

        var json = "{invalid json}";
        var payload = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ProcessMessage_BinaryPayload_ValidUInt32_ReturnsReading()
    {
        // Arrange
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST002",
            Format = PayloadFormat.Binary,
            DataType = MqttDataType.UInt32
        };

        // Binary format: [channel byte][value bytes]
        var channelByte = new byte[] { 0 };
        var valueBytes = BitConverter.GetBytes(54321u);
        var payload = new ArraySegment<byte>(channelByte.Concat(valueBytes).ToArray());

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().NotBeNull();
        result!.DeviceId.Should().Be("TEST002");
        result.Channel.Should().Be(0);
        result.RawValue.Should().Be(54321);
        result.Quality.Should().Be(DataQuality.Good);
    }

    [Fact]
    public void ProcessMessage_BinaryPayload_InvalidLength_ReturnsNull()
    {
        // Arrange
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST002",
            Format = PayloadFormat.Binary,
            DataType = MqttDataType.UInt32
        };

        var payload = new ArraySegment<byte>(new byte[] { 1, 2 }); // Too short for UInt32

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ProcessMessage_AppliesScaleFactor()
    {
        // Arrange
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST003",
            Format = PayloadFormat.Json,
            ChannelJsonPath = "$.channel",
            ValueJsonPath = "$.value",
            ScaleFactor = 0.1
        };

        var json = "{\"channel\":0,\"value\":1000}";
        var payload = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().NotBeNull();
        result!.ProcessedValue.Should().Be(100); // 1000 * 0.1
    }

    [Fact]
    public void ProcessMessage_NullDeviceConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var payload = new ArraySegment<byte>(new byte[] { 1, 2, 3, 4 });

        // Act
        var act = () => _processor.ProcessMessage(null!, "test/topic", payload);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProcessMessage_EmptyPayload_ReturnsNull()
    {
        // Arrange
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST004",
            Format = PayloadFormat.Json
        };

        var payload = new ArraySegment<byte>(Array.Empty<byte>());

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ProcessMessage_JsonPayload_NoChannel_DefaultsToZero()
    {
        // Arrange - JSON with no channel field
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST005",
            Format = PayloadFormat.Json,
            ValueJsonPath = "$.temperature",
            ChannelJsonPath = "$.channel"  // Path specified but field missing in payload
        };

        var json = "{\"temperature\":25.5}";  // No channel field
        var payload = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().NotBeNull();
        result!.Channel.Should().Be(0);  // Defaults to 0
        result.ProcessedValue.Should().Be(25.5);
    }

    [Fact]
    public void ProcessMessage_BinaryPayload_NoChannelByte_DefaultsToZero()
    {
        // Arrange - Binary payload without channel byte (just 4 bytes for UInt32)
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST006",
            Format = PayloadFormat.Binary,
            DataType = MqttDataType.UInt32
        };

        // Binary format: just value bytes (no channel byte)
        var valueBytes = BitConverter.GetBytes(12345u);
        var payload = new ArraySegment<byte>(valueBytes);

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().NotBeNull();
        result!.Channel.Should().Be(0);  // Defaults to 0
        result.RawValue.Should().Be(12345);
    }

    [Fact]
    public void ProcessMessage_CsvPayload_SingleValue_DefaultsToZero()
    {
        // Arrange - CSV with just value (no channel)
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST007",
            Format = PayloadFormat.Csv
        };

        var csv = "123.45";  // Just value, no channel
        var payload = new ArraySegment<byte>(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().NotBeNull();
        result!.Channel.Should().Be(0);  // Defaults to 0
        result.ProcessedValue.Should().Be(123.45);
    }

    [Fact]
    public void ProcessMessage_CsvPayload_ValueWithTimestamp_DefaultsToZero()
    {
        // Arrange - CSV with value and timestamp (no channel)
        var deviceConfig = new MqttDeviceConfig
        {
            DeviceId = "TEST008",
            Format = PayloadFormat.Csv
        };

        var csv = "99.99,2025-10-04T10:00:00Z";  // Value,timestamp (no channel)
        var payload = new ArraySegment<byte>(Encoding.UTF8.GetBytes(csv));

        // Act
        var result = _processor.ProcessMessage(deviceConfig, "test/topic", payload);

        // Assert
        result.Should().NotBeNull();
        result!.Channel.Should().Be(0);  // Defaults to 0
        result.ProcessedValue.Should().Be(99.99);
        result.Timestamp.Year.Should().Be(2025);
    }
}
