using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.Logger.Core.Configuration;

/// <summary>
/// MQTT broker connection settings
/// </summary>
public class MqttSettings
{
    /// <summary>
    /// MQTT broker hostname or IP address
    /// </summary>
    [Required(ErrorMessage = "MQTT broker host is required")]
    public string BrokerHost { get; set; } = "localhost";

    /// <summary>
    /// MQTT broker port (default 1883 for TCP, 8883 for TLS)
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int BrokerPort { get; set; } = 1883;

    /// <summary>
    /// Optional username for authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Optional password for authentication
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// MQTT client ID (must be unique per broker)
    /// </summary>
    [Required(ErrorMessage = "Client ID is required")]
    [StringLength(100, ErrorMessage = "Client ID must be 100 characters or less")]
    public string ClientId { get; set; } = "industrial-logger";

    /// <summary>
    /// Use TLS/SSL encryption
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Keep-alive period in seconds
    /// </summary>
    [Range(10, 3600, ErrorMessage = "Keep-alive must be between 10 and 3600 seconds")]
    public int KeepAlivePeriodSeconds { get; set; } = 60;

    /// <summary>
    /// Quality of Service level (0=AtMostOnce, 1=AtLeastOnce, 2=ExactlyOnce)
    /// </summary>
    [Range(0, 2, ErrorMessage = "QoS must be 0, 1, or 2")]
    public int QualityOfServiceLevel { get; set; } = 1; // AtLeastOnce

    /// <summary>
    /// Delay before attempting reconnection (seconds)
    /// </summary>
    [Range(1, 300, ErrorMessage = "Reconnect delay must be between 1 and 300 seconds")]
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Maximum reconnection attempts (0 = infinite)
    /// </summary>
    [Range(0, 100, ErrorMessage = "Max reconnect attempts must be between 0 and 100")]
    public int MaxReconnectAttempts { get; set; } = 0;

    /// <summary>
    /// Validate MQTT settings
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(BrokerHost))
            errors.Add("MQTT broker host is required");

        if (UseTls && BrokerPort == 1883)
            errors.Add("TLS enabled but using default non-TLS port 1883. Consider port 8883.");

        if (!string.IsNullOrWhiteSpace(Username) && string.IsNullOrWhiteSpace(Password))
            errors.Add("Username provided but password is missing");

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
