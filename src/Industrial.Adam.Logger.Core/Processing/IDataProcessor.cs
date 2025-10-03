using Industrial.Adam.Logger.Core.Models;

namespace Industrial.Adam.Logger.Core.Processing;

/// <summary>
/// Processes raw device readings with industrial-specific logic
/// </summary>
public interface IDataProcessor
{
    /// <summary>
    /// Process a raw device reading
    /// </summary>
    /// <param name="reading">Raw reading from device</param>
    /// <param name="previousReading">Previous reading for the same channel (if available)</param>
    /// <returns>Processed reading with calculated values</returns>
    public DeviceReading ProcessReading(DeviceReading reading, DeviceReading? previousReading = null);

    /// <summary>
    /// Validate a processed reading
    /// </summary>
    /// <param name="reading">Reading to validate</param>
    /// <returns>True if reading is valid</returns>
    public bool ValidateReading(DeviceReading reading);

    /// <summary>
    /// Get windowed rate statistics for a specific channel
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="channel">Channel number</param>
    /// <returns>Rate calculation statistics</returns>
    public WindowedRateStatistics? GetRateStatistics(string deviceId, int channel);
}
