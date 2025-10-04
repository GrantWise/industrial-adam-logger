using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.Logger.Core.Configuration;

/// <summary>
/// Configuration for an MQTT-enabled device
/// </summary>
public class MqttDeviceConfig
{
    /// <summary>
    /// Unique identifier for this device
    /// </summary>
    [Required(ErrorMessage = "DeviceId is required")]
    [StringLength(50, ErrorMessage = "DeviceId must be 50 characters or less")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the device
    /// </summary>
    [StringLength(100, ErrorMessage = "Name must be 100 characters or less")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Device model/type for documentation
    /// </summary>
    [StringLength(50, ErrorMessage = "ModelType must be 50 characters or less")]
    public string? ModelType { get; set; }

    /// <summary>
    /// Whether this device is enabled for monitoring
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// MQTT topics to subscribe to (supports wildcards: +, #)
    /// Example: "factory/line1/sensor/+"
    /// </summary>
    [Required(ErrorMessage = "At least one topic must be configured")]
    public List<string> Topics { get; set; } = [];

    /// <summary>
    /// Expected payload format
    /// </summary>
    public PayloadFormat Format { get; set; } = PayloadFormat.Json;

    /// <summary>
    /// Data type for parsing payload values
    /// </summary>
    public MqttDataType DataType { get; set; } = MqttDataType.UInt32;

    /// <summary>
    /// Quality of Service level for this device's topics (0=AtMostOnce, 1=AtLeastOnce, 2=ExactlyOnce).
    /// If null, uses global QoS from MqttSettings.
    /// </summary>
    [Range(0, 2, ErrorMessage = "QoS must be 0, 1, or 2")]
    public int? QosLevel { get; set; } = null;

    /// <summary>
    /// JSON path to extract device ID from payload (optional)
    /// If null, uses DeviceId from config
    /// Example: "$.device.id"
    /// </summary>
    public string? DeviceIdJsonPath { get; set; }

    /// <summary>
    /// JSON path to extract channel number from payload
    /// Example: "$.channel" or "$.sensor.id"
    /// </summary>
    public string? ChannelJsonPath { get; set; } = "$.channel";

    /// <summary>
    /// JSON path to extract value from payload
    /// Example: "$.value" or "$.measurement.count"
    /// </summary>
    public string? ValueJsonPath { get; set; } = "$.value";

    /// <summary>
    /// JSON path to extract timestamp from payload (optional)
    /// If null, uses message arrival time
    /// Example: "$.timestamp"
    /// </summary>
    public string? TimestampJsonPath { get; set; }

    /// <summary>
    /// Scaling factor to apply to values (e.g., 0.1 for analog inputs)
    /// </summary>
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Measurement unit (e.g., "°C", "counts", "psi")
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Validate device configuration
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DeviceId))
            errors.Add("DeviceId is required");

        if (Topics.Count == 0)
            errors.Add($"Device {DeviceId} must have at least one topic configured");

        // Validate topic patterns
        foreach (var topic in Topics)
        {
            if (string.IsNullOrWhiteSpace(topic))
                errors.Add($"Device {DeviceId} has empty topic");

            // Basic MQTT topic validation
            if (topic.Contains("##") || topic.Contains("++"))
                errors.Add($"Device {DeviceId} has invalid topic pattern: {topic}");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

/// <summary>
/// Supported payload formats
/// </summary>
public enum PayloadFormat
{
    /// <summary>JSON format (most flexible)</summary>
    Json,

    /// <summary>Binary format (most compact)</summary>
    Binary,

    /// <summary>CSV format (comma-separated values)</summary>
    Csv
}

/// <summary>
/// Data types for MQTT payloads
/// </summary>
public enum MqttDataType
{
    /// <summary>32-bit unsigned integer (counter)</summary>
    UInt32,

    /// <summary>16-bit signed integer</summary>
    Int16,

    /// <summary>16-bit unsigned integer</summary>
    UInt16,

    /// <summary>32-bit floating point</summary>
    Float32,

    /// <summary>64-bit floating point</summary>
    Float64
}
