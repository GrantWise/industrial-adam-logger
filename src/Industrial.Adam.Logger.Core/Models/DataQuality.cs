namespace Industrial.Adam.Logger.Core.Models;

/// <summary>
/// Data quality assessment for readings
/// </summary>
public enum DataQuality
{
    /// <summary>
    /// Data is valid and within expected parameters
    /// </summary>
    Good = 0,

    /// <summary>
    /// Data is questionable (e.g., high rate detected)
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Data is invalid (timeout, overflow, error)
    /// </summary>
    Bad = 2
}
