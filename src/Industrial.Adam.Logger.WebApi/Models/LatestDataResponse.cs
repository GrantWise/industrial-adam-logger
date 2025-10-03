using Industrial.Adam.Logger.Core.Models;

namespace Industrial.Adam.Logger.WebApi.Models;

/// <summary>
/// Latest readings from all devices
/// </summary>
public sealed class LatestDataResponse
{
    /// <summary>
    /// Total number of readings returned
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Timestamp of most recent reading
    /// </summary>
    public DateTimeOffset? LastUpdated { get; init; }

    /// <summary>
    /// Device readings ordered by device and channel
    /// </summary>
    public required List<DeviceReading> Readings { get; init; }
}

/// <summary>
/// Latest readings for a specific device
/// </summary>
public sealed class DeviceLatestDataResponse
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Number of readings for this device
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Timestamp of most recent reading
    /// </summary>
    public required DateTimeOffset LastUpdated { get; init; }

    /// <summary>
    /// Device readings ordered by channel
    /// </summary>
    public required List<DeviceReading> Readings { get; init; }
}
