using Industrial.Adam.Logger.Core.Models;

namespace Industrial.Adam.Logger.WebApi.Models;

/// <summary>
/// Detailed health status response including all system components
/// </summary>
public sealed class DetailedHealthResponse
{
    /// <summary>
    /// Overall health status (Healthy, Degraded, or Unhealthy)
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Timestamp when health check was performed
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Health status of all system components
    /// </summary>
    public required ComponentsHealth Components { get; init; }
}

/// <summary>
/// Health status for all system components
/// </summary>
public sealed class ComponentsHealth
{
    /// <summary>
    /// Service component health
    /// </summary>
    public required ComponentStatus Service { get; init; }

    /// <summary>
    /// Database component health
    /// </summary>
    public required DatabaseStatus Database { get; init; }

    /// <summary>
    /// Devices component health
    /// </summary>
    public required DeviceComponentStatus Devices { get; init; }
}

/// <summary>
/// Component health status
/// </summary>
public sealed class ComponentStatus
{
    /// <summary>
    /// Component status (Healthy or Unhealthy)
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Whether the service is currently running
    /// </summary>
    public required bool IsRunning { get; init; }

    /// <summary>
    /// Service start time
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Service uptime duration
    /// </summary>
    public required TimeSpan Uptime { get; init; }
}

/// <summary>
/// Database health status
/// </summary>
public sealed class DatabaseStatus
{
    /// <summary>
    /// Database status (Healthy or Unhealthy)
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Whether database connection is active
    /// </summary>
    public required bool Connected { get; init; }
}

/// <summary>
/// Device pool health status
/// </summary>
public sealed class DeviceComponentStatus
{
    /// <summary>
    /// Device pool status (Healthy, Degraded, or Unhealthy)
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Total number of configured devices
    /// </summary>
    public required int Total { get; init; }

    /// <summary>
    /// Number of currently connected devices
    /// </summary>
    public required int Connected { get; init; }

    /// <summary>
    /// Health details for each device
    /// </summary>
    public required Dictionary<string, DeviceHealth> Details { get; init; }
}
