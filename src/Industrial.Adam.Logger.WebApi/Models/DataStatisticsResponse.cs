namespace Industrial.Adam.Logger.WebApi.Models;

/// <summary>
/// Data collection statistics and metrics
/// </summary>
public sealed class DataStatisticsResponse
{
    /// <summary>
    /// Overall system summary
    /// </summary>
    public required StatisticsSummary Summary { get; init; }

    /// <summary>
    /// Per-device statistics
    /// </summary>
    public required List<DeviceStatistics> DeviceStatistics { get; init; }
}

/// <summary>
/// Overall statistics summary
/// </summary>
public sealed class StatisticsSummary
{
    /// <summary>
    /// Whether the logger service is running
    /// </summary>
    public required bool ServiceRunning { get; init; }

    /// <summary>
    /// Service uptime duration
    /// </summary>
    public required TimeSpan ServiceUptime { get; init; }

    /// <summary>
    /// Total number of configured devices
    /// </summary>
    public required int TotalDevices { get; init; }

    /// <summary>
    /// Number of currently connected devices
    /// </summary>
    public required int ConnectedDevices { get; init; }

    /// <summary>
    /// Total number of active readings
    /// </summary>
    public required int TotalReadings { get; init; }

    /// <summary>
    /// Timestamp of most recent data update
    /// </summary>
    public DateTimeOffset? LastDataUpdate { get; init; }
}

/// <summary>
/// Statistics for a specific device
/// </summary>
public sealed class DeviceStatistics
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Number of active channels
    /// </summary>
    public required int ChannelCount { get; init; }

    /// <summary>
    /// Timestamp of last update from this device
    /// </summary>
    public required DateTimeOffset LastUpdate { get; init; }

    /// <summary>
    /// Average rate across all channels (units per second)
    /// </summary>
    public required double AverageRate { get; init; }

    /// <summary>
    /// Distribution of data quality across readings
    /// </summary>
    public required Dictionary<string, int> QualityDistribution { get; init; }
}
