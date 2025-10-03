namespace Industrial.Adam.Logger.Core.Models;

/// <summary>
/// Represents a data reading from an ADAM device channel
/// </summary>
public sealed record DeviceReading
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Channel number (0-based)
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// Raw counter value from Modbus register
    /// </summary>
    public required long RawValue { get; init; }

    /// <summary>
    /// Timestamp when data was acquired
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Processed value after scaling/calibration
    /// </summary>
    public double ProcessedValue { get; init; }

    /// <summary>
    /// Rate of change (units per second)
    /// </summary>
    public double? Rate { get; init; }

    /// <summary>
    /// Data quality assessment
    /// </summary>
    public DataQuality Quality { get; init; } = DataQuality.Good;

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public string Unit { get; init; } = "counts";

    /// <summary>
    /// Additional tags for InfluxDB
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = [];
}
