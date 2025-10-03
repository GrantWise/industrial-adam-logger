using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.Logger.Core.Configuration;

/// <summary>
/// Modbus register type for reading data
/// </summary>
public enum ModbusRegisterType
{
    /// <summary>
    /// Holding Register (Function Code 03) - Used for counters and digital I/O
    /// </summary>
    HoldingRegister = 0,

    /// <summary>
    /// Input Register (Function Code 04) - Used for analog inputs
    /// </summary>
    InputRegister = 1
}

/// <summary>
/// Data type stored in Modbus registers
/// </summary>
public enum ChannelDataType
{
    /// <summary>
    /// 32-bit unsigned counter (2 registers, little-endian)
    /// Used for digital counters (ADAM-6051, 6052, etc.)
    /// </summary>
    UInt32Counter = 0,

    /// <summary>
    /// Signed 16-bit integer (-32768 to 32767)
    /// </summary>
    Int16 = 1,

    /// <summary>
    /// Unsigned 16-bit integer (0 to 65535)
    /// </summary>
    UInt16 = 2,

    /// <summary>
    /// IEEE 754 32-bit float (2 registers, big-endian)
    /// Used for analog modules (ADAM-6017, 6015, etc.)
    /// </summary>
    Float32 = 3,

    /// <summary>
    /// Signed 32-bit integer (2 registers, little-endian)
    /// </summary>
    Int32 = 4
}

/// <summary>
/// Configuration for a device channel
/// </summary>
public class ChannelConfig
{
    /// <summary>
    /// Channel number on the device (0-based)
    /// </summary>
    [Range(0, 255, ErrorMessage = "ChannelNumber must be between 0 and 255")]
    public int ChannelNumber { get; set; }

    /// <summary>
    /// Human-readable name for this channel
    /// </summary>
    [Required(ErrorMessage = "Channel name is required")]
    [StringLength(100, ErrorMessage = "Channel name must be 100 characters or less")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Starting Modbus register address
    /// </summary>
    [Range(0, 65535, ErrorMessage = "StartRegister must be between 0 and 65535")]
    public ushort StartRegister { get; set; }

    /// <summary>
    /// Number of registers to read (2 for 32-bit counter)
    /// </summary>
    [Range(1, 4, ErrorMessage = "RegisterCount must be between 1 and 4")]
    public int RegisterCount { get; set; } = Constants.CounterRegisterCount;

    /// <summary>
    /// Whether this channel is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Modbus register type to read (default: HoldingRegister for counters)
    /// </summary>
    public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

    /// <summary>
    /// Data type interpretation (default: UInt32Counter for digital counters)
    /// </summary>
    public ChannelDataType DataType { get; set; } = ChannelDataType.UInt32Counter;

    /// <summary>
    /// Scaling factor to apply to raw value
    /// </summary>
    [Range(0.0001, 1000000, ErrorMessage = "ScaleFactor must be between 0.0001 and 1,000,000")]
    public double ScaleFactor { get; set; } = 1.0;

    /// <summary>
    /// Offset to apply after scaling
    /// </summary>
    public double Offset { get; set; } = 0.0;

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public string Unit { get; set; } = "counts";

    /// <summary>
    /// Minimum valid value (for validation)
    /// </summary>
    public double? MinValue { get; set; }

    /// <summary>
    /// Maximum valid value (for validation)
    /// </summary>
    public double? MaxValue { get; set; }

    /// <summary>
    /// High rate threshold for anomaly detection
    /// </summary>
    public double? HighRateThreshold { get; set; }

    /// <summary>
    /// Maximum allowed change rate (units per second)
    /// </summary>
    public double? MaxChangeRate { get; set; }

    /// <summary>
    /// Rate calculation window in seconds (default: 60s for operational monitoring)
    /// - Real-time monitoring (alarms, HMI): 30 seconds
    /// - Operational monitoring (dashboards): 60 seconds (default)  
    /// - Production planning (KPIs, reports): 180 seconds
    /// - Process optimization (trend analysis): 300-900 seconds
    /// </summary>
    [Range(10, 1800, ErrorMessage = "RateWindowSeconds must be between 10 seconds and 30 minutes")]
    public int RateWindowSeconds { get; set; } = Constants.DefaultRateWindowSeconds;

    /// <summary>
    /// Additional tags for this channel
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = [];

    /// <summary>
    /// Validate channel configuration
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (RegisterCount < 1 || RegisterCount > 4)
        {
            errors.Add("RegisterCount must be between 1 and 4");
        }

        if (MinValue.HasValue && MaxValue.HasValue && MinValue.Value >= MaxValue.Value)
        {
            errors.Add("MinValue must be less than MaxValue");
        }

        if (ScaleFactor <= 0)
        {
            errors.Add("ScaleFactor must be greater than 0");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
