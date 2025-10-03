using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Industrial.Adam.Logger.Core.Configuration;

/// <summary>
/// Configuration for an individual ADAM device
/// </summary>
public class DeviceConfig
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
    /// ADAM model type for documentation (e.g., "ADAM-6051", "ADAM-6017")
    /// </summary>
    [StringLength(50, ErrorMessage = "ModelType must be 50 characters or less")]
    public string? ModelType { get; set; }

    /// <summary>
    /// Whether this device is enabled for monitoring
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// IP address of the ADAM device
    /// </summary>
    [Required(ErrorMessage = "IP Address is required")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Modbus TCP port (default 502)
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = Constants.DefaultModbusPort;

    /// <summary>
    /// Modbus unit ID (slave address)
    /// </summary>
    [Range(1, 255, ErrorMessage = "UnitId must be between 1 and 255")]
    public byte UnitId { get; set; } = 1;

    /// <summary>
    /// Device-specific polling interval in milliseconds (overrides global)
    /// </summary>
    [Range(100, 300000, ErrorMessage = "PollIntervalMs must be between 100ms and 5 minutes")]
    public int PollIntervalMs { get; set; } = Constants.DefaultPollIntervalMs;

    /// <summary>
    /// Communication timeout in milliseconds
    /// </summary>
    [Range(500, 30000, ErrorMessage = "TimeoutMs must be between 500ms and 30 seconds")]
    public int TimeoutMs { get; set; } = Constants.DefaultTimeoutMs;

    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetries must be between 0 and 10")]
    public int MaxRetries { get; set; } = Constants.DefaultMaxRetries;

    /// <summary>
    /// List of channels to monitor on this device
    /// </summary>
    [Required(ErrorMessage = "At least one channel must be configured")]
    public List<ChannelConfig> Channels { get; set; } = [];

    /// <summary>
    /// Enable TCP keep-alive for connection monitoring
    /// </summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>
    /// TCP receive buffer size
    /// </summary>
    public int ReceiveBufferSize { get; set; } = Constants.DefaultReceiveBufferSize;

    /// <summary>
    /// TCP send buffer size
    /// </summary>
    public int SendBufferSize { get; set; } = Constants.DefaultSendBufferSize;

    /// <summary>
    /// Validates if the given string is a valid IP address or resolvable hostname
    /// </summary>
    private static bool IsValidIpAddressOrHostname(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        // First try to parse as IP address
        if (IPAddress.TryParse(address, out _))
            return true;

        // Check if it's a valid hostname format
        if (!IsValidHostnameFormat(address))
            return false;

        // For hostnames, we accept them as valid without DNS resolution
        // The actual connection will fail later if the hostname can't be resolved
        return true;
    }

    /// <summary>
    /// Validates hostname format according to RFC standards
    /// </summary>
    private static bool IsValidHostnameFormat(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname) || hostname.Length > 253)
            return false;

        // Hostname can contain letters, digits, hyphens, and dots
        // Must not start or end with hyphen or dot
        if (hostname.StartsWith('-') || hostname.EndsWith('-') ||
            hostname.StartsWith('.') || hostname.EndsWith('.'))
            return false;

        // Split by dots and validate each label
        var labels = hostname.Split('.');
        foreach (var label in labels)
        {
            if (string.IsNullOrEmpty(label) || label.Length > 63)
                return false;

            if (label.StartsWith('-') || label.EndsWith('-'))
                return false;

            // Each label should contain only alphanumeric characters and hyphens
            if (!label.All(c => char.IsLetterOrDigit(c) || c == '-'))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Validate device configuration
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        // Validate IP address or hostname
        if (!IsValidIpAddressOrHostname(IpAddress))
        {
            errors.Add($"Invalid IP address or hostname for device {DeviceId}: '{IpAddress}'. " +
                      "Use a valid IP address (e.g., '192.168.1.100'), 'localhost', or hostname (e.g., 'adam-device-01')");
        }

        // Validate channels
        if (Channels.Count == 0)
        {
            errors.Add($"Device {DeviceId} must have at least one channel configured");
        }

        // Check for duplicate channel numbers
        var duplicateChannels = Channels.GroupBy(c => c.ChannelNumber)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var channel in duplicateChannels)
        {
            errors.Add($"Device {DeviceId} has duplicate channel number: {channel}");
        }

        // Validate each channel
        foreach (var channel in Channels)
        {
            var channelErrors = channel.Validate();
            if (!channelErrors.IsValid)
            {
                errors.AddRange(channelErrors.Errors.Select(e => $"Device {DeviceId}, Channel {channel.ChannelNumber}: {e}"));
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
