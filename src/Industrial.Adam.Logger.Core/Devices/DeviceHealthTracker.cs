using System.Collections.Concurrent;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Core.Devices;

/// <summary>
/// Tracks health and statistics for connected devices
/// </summary>
public sealed class DeviceHealthTracker
{
    private readonly ConcurrentDictionary<string, DeviceHealthData> _healthData = new();
    private readonly ILogger<DeviceHealthTracker> _logger;

    /// <summary>
    /// Initialize the device health tracker
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public DeviceHealthTracker(ILogger<DeviceHealthTracker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Record a successful read operation
    /// </summary>
    public void RecordSuccess(string deviceId, TimeSpan duration)
    {
        var data = _healthData.AddOrUpdate(deviceId,
            _ =>
            {
                var newData = new DeviceHealthData
                {
                    LastSuccessfulRead = DateTimeOffset.UtcNow,
                    SuccessfulReads = 1,
                    TotalReads = 1,
                    ConsecutiveFailures = 0
                };
                newData.AddDuration(duration);
                return newData;
            },
            (_, existing) =>
            {
                existing.LastSuccessfulRead = DateTimeOffset.UtcNow;
                existing.SuccessfulReads++;
                existing.TotalReads++;
                existing.ConsecutiveFailures = 0;
                existing.AddDuration(duration);
                return existing;
            });

        _logger.LogDebug(
            "Device {DeviceId}: Read successful in {Duration}ms, success rate: {Rate:F1}%",
            deviceId, duration.TotalMilliseconds, data.SuccessRate);
    }

    /// <summary>
    /// Record a failed read operation
    /// </summary>
    public void RecordFailure(string deviceId, string error)
    {
        var data = _healthData.AddOrUpdate(deviceId,
            _ => new DeviceHealthData
            {
                LastError = error,
                LastErrorTime = DateTimeOffset.UtcNow,
                TotalReads = 1,
                ConsecutiveFailures = 1
            },
            (_, existing) =>
            {
                existing.LastError = error;
                existing.LastErrorTime = DateTimeOffset.UtcNow;
                existing.TotalReads++;
                existing.ConsecutiveFailures++;
                return existing;
            });

        if (data.ConsecutiveFailures == Constants.MaxConsecutiveFailures)
        {
            _logger.LogWarning(
                "Device {DeviceId} marked as offline after {Failures} consecutive failures",
                deviceId, data.ConsecutiveFailures);
        }
    }

    /// <summary>
    /// Get current health status for a device
    /// </summary>
    public DeviceHealth GetDeviceHealth(string deviceId)
    {
        if (!_healthData.TryGetValue(deviceId, out var data))
        {
            return new DeviceHealth
            {
                DeviceId = deviceId,
                IsConnected = false,
                ConsecutiveFailures = 0,
                TotalReads = 0,
                SuccessfulReads = 0
            };
        }

        return new DeviceHealth
        {
            DeviceId = deviceId,
            IsConnected = data.ConsecutiveFailures < Constants.MaxConsecutiveFailures,
            LastSuccessfulRead = data.LastSuccessfulRead,
            ConsecutiveFailures = data.ConsecutiveFailures,
            LastError = data.LastError,
            TotalReads = data.TotalReads,
            SuccessfulReads = data.SuccessfulReads
        };
    }

    /// <summary>
    /// Get health status for all devices
    /// </summary>
    public Dictionary<string, DeviceHealth> GetAllDeviceHealth()
    {
        return _healthData.ToDictionary(
            kvp => kvp.Key,
            kvp => GetDeviceHealth(kvp.Key));
    }

    /// <summary>
    /// Get average response time for a device
    /// </summary>
    public double? GetAverageResponseTime(string deviceId)
    {
        if (!_healthData.TryGetValue(deviceId, out var data) || data.RecentDurations.Count == 0)
            return null;

        lock (data.DurationLock)
        {
            return data.RecentDurations.Average(d => d.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Reset health data for a device
    /// </summary>
    public void ResetDeviceHealth(string deviceId)
    {
        _healthData.TryRemove(deviceId, out _);
        _logger.LogInformation("Reset health data for device {DeviceId}", deviceId);
    }

    /// <summary>
    /// Internal health data storage
    /// </summary>
    private class DeviceHealthData
    {
        private const int MaxDurationHistory = 100;

        public DateTimeOffset? LastSuccessfulRead { get; set; }
        public DateTimeOffset? LastErrorTime { get; set; }
        public string? LastError { get; set; }
        public long TotalReads { get; set; }
        public long SuccessfulReads { get; set; }
        public int ConsecutiveFailures { get; set; }
        public List<TimeSpan> RecentDurations { get; } = new();
        public object DurationLock { get; } = new();

        public double SuccessRate => TotalReads > 0 ? (double)SuccessfulReads / TotalReads * 100 : 0;

        public void AddDuration(TimeSpan duration)
        {
            lock (DurationLock)
            {
                RecentDurations.Add(duration);
                if (RecentDurations.Count > MaxDurationHistory)
                {
                    RecentDurations.RemoveAt(0);
                }
            }
        }
    }
}
