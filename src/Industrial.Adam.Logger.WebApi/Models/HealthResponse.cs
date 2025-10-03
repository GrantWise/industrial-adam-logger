using Industrial.Adam.Logger.Core.Models;

namespace Industrial.Adam.Logger.WebApi.Models;

/// <summary>
/// Service health status response
/// </summary>
public sealed class HealthResponse
{
    /// <summary>
    /// Overall health status (Healthy or Unhealthy)
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Timestamp when health check was performed
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Service status details
    /// </summary>
    public required ServiceInfo Service { get; init; }

    /// <summary>
    /// Device connectivity details
    /// </summary>
    public required DevicesInfo Devices { get; init; }
}

/// <summary>
/// Service component information
/// </summary>
public sealed class ServiceInfo
{
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
/// Devices connectivity information
/// </summary>
public sealed class DevicesInfo
{
    /// <summary>
    /// Total number of configured devices
    /// </summary>
    public required int Total { get; init; }

    /// <summary>
    /// Number of currently connected devices
    /// </summary>
    public required int Connected { get; init; }

    /// <summary>
    /// Health status for each device
    /// </summary>
    public required Dictionary<string, DeviceHealth> Health { get; init; }
}
