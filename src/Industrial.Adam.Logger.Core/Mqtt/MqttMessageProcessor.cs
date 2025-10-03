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

        // Extract channel number
        if (!TryExtractJsonValue<int>(root, config.ChannelJsonPath ?? "$.channel", out var channelNumber))
        {
            _logger.LogWarning("Could not extract channel number from JSON using path {Path}", config.ChannelJsonPath);
            return null;
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

        if (payload.Count < 4)
        {
            _logger.LogWarning("Binary payload too small: {Length} bytes (minimum 4)", payload.Count);
            return null;
        }

        try
        {
            var span = payload.AsSpan();

            // Extract channel number (first byte)
            var channelNumber = span[0];

            // Extract value based on data type
            double value = config.DataType switch
            {
                MqttDataType.UInt32 => BitConverter.ToUInt32(span[1..5]),
                MqttDataType.Int16 => BitConverter.ToInt16(span[1..3]),
                MqttDataType.UInt16 => BitConverter.ToUInt16(span[1..3]),
                MqttDataType.Float32 => BitConverter.ToSingle(span[1..5]),
                MqttDataType.Float64 => BitConverter.ToDouble(span[1..9]),
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

    private DeviceReading? ProcessCsvPayload(MqttDeviceConfig config, string topic, ArraySegment<byte> payload)
    {
        var csv = Encoding.UTF8.GetString(payload).Trim();
        _logger.LogDebug("Processing CSV payload: {Csv}", csv);

        var fields = csv.Split(',');
        if (fields.Length < 2)
        {
            _logger.LogWarning("CSV payload has insufficient fields: {Count}", fields.Length);
            return null;
        }

        try
        {
            // First field is channel number, second is value
            if (!int.TryParse(fields[0], out var channelNumber))
            {
                _logger.LogWarning("Could not parse channel number from CSV: {Field}", fields[0]);
                return null;
            }

            if (!double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                _logger.LogWarning("Could not parse value from CSV: {Field}", fields[1]);
                return null;
            }

            // Apply scale factor
            var scaledValue = value * config.ScaleFactor;

            // Optional timestamp in third field
            var timestamp = DateTimeOffset.UtcNow;
            if (fields.Length > 2 && DateTimeOffset.TryParse(fields[2], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTimestamp))
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
