namespace Industrial.Adam.Logger.Core.Models;

/// <summary>
/// Data quality assessment for readings (21 CFR Part 11 compliant)
/// </summary>
public enum DataQuality
{
    /// <summary>
    /// Data is valid and within expected parameters
    /// </summary>
    Good = 0,

    /// <summary>
    /// Data is questionable (e.g., high rate detected, estimated value)
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Data is invalid (timeout, overflow, validation error)
    /// </summary>
    Bad = 2,

    /// <summary>
    /// Data is unavailable (device offline, communication failure, no connection)
    /// </summary>
    Unavailable = 3
}
