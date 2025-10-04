using System.Globalization;
using System.Text;
using System.Text.Json;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Core.Mqtt;

/// <summary>
/// Processes MQTT messages and converts them to DeviceReading instances.
/// Supports JSON, Binary, and CSV payload formats with configurable field extraction.
/// </summary>
public sealed class MqttMessageProcessor
{
    private readonly ILogger<MqttMessageProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MqttMessageProcessor"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public MqttMessageProcessor(ILogger<MqttMessageProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes an MQTT message payload and extracts a device reading.
    /// </summary>
    /// <param name="deviceConfig">Device configuration specifying payload format and extraction rules.</param>
    /// <param name="topic">MQTT topic the message was received on.</param>
    /// <param name="payload">Raw message payload bytes.</param>
    /// <returns>DeviceReading if successfully parsed, null otherwise.</returns>
    public DeviceReading? ProcessMessage(MqttDeviceConfig deviceConfig, string topic, ArraySegment<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(deviceConfig);
        ArgumentNullException.ThrowIfNull(topic);

        try
        {
            return deviceConfig.Format switch
            {
                PayloadFormat.Json => ProcessJsonPayload(deviceConfig, topic, payload),
                PayloadFormat.Binary => ProcessBinaryPayload(deviceConfig, topic, payload),
                PayloadFormat.Csv => ProcessCsvPayload(deviceConfig, topic, payload),
                _ => throw new NotSupportedException($"Payload format {deviceConfig.Format} is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process MQTT message for device {DeviceId} on topic {Topic}",
                deviceConfig.DeviceId, topic);
            return null;
        }
    }

    private DeviceReading? ProcessJsonPayload(MqttDeviceConfig config, string topic, ArraySegment<byte> payload)
    {
        var json = Encoding.UTF8.GetString(payload);
        _logger.LogDebug("Processing JSON payload: {Json}", json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Extract channel number (default to 0 for single-value MQTT devices)
        if (!TryExtractJsonValue<int>(root, config.ChannelJsonPath ?? "$.channel", out var channelNumber))
        {
            channelNumber = 0;
            _logger.LogDebug("No channel in payload for device {DeviceId}, defaulting to channel 0", config.DeviceId);
        }

        // Extract value
        if (!TryExtractJsonValue<double>(root, config.ValueJsonPath ?? "$.value", out var rawValue))
        {
            _logger.LogWarning("Could not extract value from JSON using path {Path}", config.ValueJsonPath);
            return null;
        }

        // Apply scale factor
        var scaledValue = rawValue * config.ScaleFactor;

        // Extract or use current timestamp
        var timestamp = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(config.TimestampJsonPath))
        {
            if (TryExtractJsonValue<string>(root, config.TimestampJsonPath, out var timestampValue) &&
                DateTimeOffset.TryParse(timestampValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTimestamp))
            {
                timestamp = parsedTimestamp.ToUniversalTime();
            }
        }

        return CreateReading(config, channelNumber, rawValue, scaledValue, timestamp);
    }

    private DeviceReading? ProcessBinaryPayload(MqttDeviceConfig config, string topic, ArraySegment<byte> payload)
    {
        _logger.LogDebug("Processing binary payload ({Length} bytes)", payload.Count);

        try
        {
            var span = payload.AsSpan();
            int channelNumber = 0;
            int valueOffset = 0;

            // Check if payload includes channel byte (old format: [channel][value])
            // or just value (new format: [value] with channel defaulting to 0)
            var expectedSizeWithChannel = GetExpectedBinarySize(config.DataType, includeChannelByte: true);
            var expectedSizeNoChannel = GetExpectedBinarySize(config.DataType, includeChannelByte: false);

            if (payload.Count == expectedSizeWithChannel)
            {
                // Format: [1 byte channel][N bytes value]
                channelNumber = span[0];
                valueOffset = 1;
            }
            else if (payload.Count == expectedSizeNoChannel)
            {
                // Format: [N bytes value] - channel defaults to 0
                channelNumber = 0;
                valueOffset = 0;
                _logger.LogDebug("Binary payload has no channel byte, defaulting to channel 0 for device {DeviceId}", config.DeviceId);
            }
            else
            {
                _logger.LogWarning("Binary payload has unexpected length: {Length} bytes (expected {WithChannel} or {NoChannel})",
                    payload.Count, expectedSizeWithChannel, expectedSizeNoChannel);
                return null;
            }

            // Extract value based on data type
            double value = config.DataType switch
            {
                MqttDataType.UInt32 => BitConverter.ToUInt32(span.Slice(valueOffset, 4)),
                MqttDataType.Int16 => BitConverter.ToInt16(span.Slice(valueOffset, 2)),
                MqttDataType.UInt16 => BitConverter.ToUInt16(span.Slice(valueOffset, 2)),
                MqttDataType.Float32 => BitConverter.ToSingle(span.Slice(valueOffset, 4)),
                MqttDataType.Float64 => BitConverter.ToDouble(span.Slice(valueOffset, 8)),
                _ => throw new NotSupportedException($"Data type {config.DataType} not supported for binary format")
            };

            // Apply scale factor
            var scaledValue = value * config.ScaleFactor;

            return CreateReading(config, channelNumber, value, scaledValue, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse binary payload for device {DeviceId}", config.DeviceId);
            return null;
        }
    }

    private static int GetExpectedBinarySize(MqttDataType dataType, bool includeChannelByte)
    {
        var channelSize = includeChannelByte ? 1 : 0;
        var valueSize = dataType switch
        {
            MqttDataType.Int16 => 2,
            MqttDataType.UInt16 => 2,
            MqttDataType.UInt32 => 4,
            MqttDataType.Float32 => 4,
            MqttDataType.Float64 => 8,
            _ => throw new NotSupportedException($"Data type {dataType} not supported")
        };
        return channelSize + valueSize;
    }

    private DeviceReading? ProcessCsvPayload(MqttDeviceConfig config, string topic, ArraySegment<byte> payload)
    {
        var csv = Encoding.UTF8.GetString(payload).Trim();
        _logger.LogDebug("Processing CSV payload: {Csv}", csv);

        var fields = csv.Split(',');
        if (fields.Length < 1)
        {
            _logger.LogWarning("CSV payload is empty");
            return null;
        }

        try
        {
            int channelNumber = 0;
            double value;
            int timestampFieldIndex;

            // Support two CSV formats:
            // 1. Single value: "123.45" or "123.45,2025-10-04T10:00:00Z"
            // 2. Channel,value: "0,123.45" or "0,123.45,2025-10-04T10:00:00Z"

            if (fields.Length == 1 || !int.TryParse(fields[0], out channelNumber))
            {
                // Format 1: Just value (no channel) - defaults to channel 0
                channelNumber = 0;
                if (!double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    _logger.LogWarning("Could not parse value from CSV: {Field}", fields[0]);
                    return null;
                }
                timestampFieldIndex = 1;
                _logger.LogDebug("CSV has no channel field, defaulting to channel 0 for device {DeviceId}", config.DeviceId);
            }
            else
            {
                // Format 2: Channel,value
                if (fields.Length < 2)
                {
                    _logger.LogWarning("CSV with channel number requires value field");
                    return null;
                }

                if (!double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    _logger.LogWarning("Could not parse value from CSV: {Field}", fields[1]);
                    return null;
                }
                timestampFieldIndex = 2;
            }

            // Apply scale factor
            var scaledValue = value * config.ScaleFactor;

            // Optional timestamp field
            var timestamp = DateTimeOffset.UtcNow;
            if (fields.Length > timestampFieldIndex &&
                DateTimeOffset.TryParse(fields[timestampFieldIndex], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTimestamp))
            {
                timestamp = parsedTimestamp.ToUniversalTime();
            }

            return CreateReading(config, channelNumber, value, scaledValue, timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse CSV payload for device {DeviceId}", config.DeviceId);
            return null;
        }
    }

    private DeviceReading CreateReading(MqttDeviceConfig config, int channelNumber, double rawValue, double processedValue, DateTimeOffset timestamp)
    {
        return new DeviceReading
        {
            DeviceId = config.DeviceId,
            Channel = channelNumber,
            RawValue = (long)rawValue, // Convert to long for RawValue
            ProcessedValue = processedValue,
            Timestamp = timestamp,
            Quality = DataQuality.Good, // MQTT data is considered Good quality when successfully parsed
            Unit = config.Unit ?? "counts"
        };
    }

    private bool TryExtractJsonValue<T>(JsonElement root, string jsonPath, out T value)
    {
        value = default!;

        // Simple JSON path extraction supporting basic dot notation
        // Full JsonPath support would require additional library (e.g., Json.NET)

        var pathSegments = jsonPath.TrimStart('$').Trim('.').Split('.');
        var current = root;

        foreach (var segment in pathSegments)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!current.TryGetProperty(segment, out current))
            {
                return false;
            }
        }

        // Convert JSON element to target type
        try
        {
            if (typeof(T) == typeof(int))
            {
                value = (T)(object)current.GetInt32();
                return true;
            }
            if (typeof(T) == typeof(double))
            {
                value = (T)(object)current.GetDouble();
                return true;
            }
            if (typeof(T) == typeof(string))
            {
                var strValue = current.GetString();
                if (strValue != null)
                {
                    value = (T)(object)strValue;
                    return true;
                }
                return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
