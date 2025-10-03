namespace Industrial.Adam.Logger.Core.Models;

/// <summary>
/// Device health and connection status
/// </summary>
public sealed record DeviceHealth
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Current connection status
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Last successful read timestamp
    /// </summary>
    public DateTimeOffset? LastSuccessfulRead { get; init; }

    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>
    /// Last error message
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Total read attempts
    /// </summary>
    public long TotalReads { get; init; }

    /// <summary>
    /// Successful read count
    /// </summary>
    public long SuccessfulReads { get; init; }

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => TotalReads > 0 ? (double)SuccessfulReads / TotalReads * 100 : 0;

    /// <summary>
    /// Device is considered offline after max consecutive failures
    /// </summary>
    public bool IsOffline => ConsecutiveFailures >= Constants.MaxConsecutiveFailures;
}
