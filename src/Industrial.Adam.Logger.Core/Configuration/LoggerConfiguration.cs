using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.Logger.Core.Configuration;

/// <summary>
/// Main configuration for the ADAM logger service
/// </summary>
public class LoggerConfiguration
{
    /// <summary>
    /// List of ADAM devices to monitor
    /// </summary>
    [Required(ErrorMessage = "At least one device must be configured")]
    public List<DeviceConfig> Devices { get; set; } = [];

    /// <summary>
    /// Default polling interval in milliseconds (can be overridden per device)
    /// </summary>
    [Range(100, 300000, ErrorMessage = "GlobalPollIntervalMs must be between 100ms and 5 minutes")]
    public int GlobalPollIntervalMs { get; set; } = Constants.DefaultPollIntervalMs;

    /// <summary>
    /// Health check interval in milliseconds
    /// </summary>
    [Range(5000, 300000, ErrorMessage = "HealthCheckIntervalMs must be between 5 seconds and 5 minutes")]
    public int HealthCheckIntervalMs { get; set; } = Constants.DefaultHealthCheckIntervalMs;

    /// <summary>
    /// TimescaleDB connection settings
    /// </summary>
    [Required(ErrorMessage = "TimescaleDB configuration is required")]
    public TimescaleSettings TimescaleDb { get; set; } = new();

    /// <summary>
    /// Enable demo mode (uses mock device manager)
    /// </summary>
    public bool DemoMode { get; set; } = false;

    /// <summary>
    /// Validate the configuration
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (Devices.Count == 0)
        {
            errors.Add("At least one device must be configured");
        }

        // Check for duplicate device IDs
        var duplicateIds = Devices.GroupBy(d => d.DeviceId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var id in duplicateIds)
        {
            errors.Add($"Duplicate device ID: {id}");
        }

        // Validate each device
        foreach (var device in Devices)
        {
            var deviceErrors = device.Validate();
            if (!deviceErrors.IsValid)
            {
                errors.AddRange(deviceErrors.Errors);
            }
        }

        // Validate TimescaleDB settings
        var timescaleErrors = TimescaleDb.Validate();
        if (!timescaleErrors.IsValid)
        {
            errors.AddRange(timescaleErrors.Errors);
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

/// <summary>
/// Validation result for configuration
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the configuration is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<string> Errors { get; set; } = [];
}
