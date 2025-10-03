using System.Collections.Concurrent;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Core.Processing;

/// <summary>
/// Calculates production rates using configurable time windows for smooth, reliable rate calculations
/// Replaces simple point-to-point calculations with windowed approach that handles brief stoppages
/// and provides more stable rate values for industrial applications
/// </summary>
public sealed class WindowedRateCalculator : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, CircularBuffer<TimestampedReading>> _channelBuffers;
    private readonly object _cleanupLock = new();
    private readonly Timer _cleanupTimer;
    private volatile bool _disposed;

    // Buffer configuration
    private const int MaxBufferSize = 200; // ~17 minutes at 5-second polling
    private const int CleanupIntervalMinutes = 5;
    private const int MaxBufferAgeMinutes = 20; // Clean buffers older than this

    // Counter limits for overflow detection
    private const long Counter16BitMax = 65535;
    private const long Counter32BitMax = 4294967295;

    /// <summary>
    /// Initialize the windowed rate calculator
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public WindowedRateCalculator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _channelBuffers = new ConcurrentDictionary<string, CircularBuffer<TimestampedReading>>();

        // Setup periodic cleanup of old buffers
        _cleanupTimer = new Timer(PerformCleanup, null,
            TimeSpan.FromMinutes(CleanupIntervalMinutes),
            TimeSpan.FromMinutes(CleanupIntervalMinutes));

        _logger.LogDebug("WindowedRateCalculator initialized with {MaxBufferSize} reading capacity per channel", MaxBufferSize);
    }

    /// <summary>
    /// Calculate windowed rate for a device reading
    /// </summary>
    /// <param name="current">Current device reading</param>
    /// <param name="channelConfig">Channel configuration including window settings</param>
    /// <returns>Calculated rate in units per second, or 0.0 if insufficient data</returns>
    public double CalculateWindowedRate(DeviceReading current, ChannelConfig channelConfig)
    {
        if (_disposed)
            return 0.0;

        var channelKey = GetChannelKey(current.DeviceId, current.Channel);

        // Get or create buffer for this channel
        var buffer = _channelBuffers.GetOrAdd(channelKey, _ => new CircularBuffer<TimestampedReading>(MaxBufferSize));

        // Add current reading to buffer
        var timestampedReading = TimestampedReading.FromDeviceReading(current);
        buffer.Add(timestampedReading);

        // Calculate windowed rate
        return CalculateRateFromWindow(buffer, timestampedReading, channelConfig);
    }

    /// <summary>
    /// Get statistics for a channel's rate calculation buffer
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="channel">Channel number</param>
    /// <returns>Buffer statistics or null if channel not found</returns>
    public WindowedRateStatistics? GetChannelStatistics(string deviceId, int channel)
    {
        var channelKey = GetChannelKey(deviceId, channel);
        if (!_channelBuffers.TryGetValue(channelKey, out var buffer))
            return null;

        return new WindowedRateStatistics
        {
            ChannelKey = channelKey,
            BufferCount = buffer.Count,
            BufferCapacity = buffer.Capacity,
            HasData = !buffer.IsEmpty,
            OldestTimestamp = buffer.IsEmpty ? null : buffer.PeekOldest().Timestamp,
            NewestTimestamp = buffer.IsEmpty ? null : buffer.PeekNewest().Timestamp
        };
    }

    private double CalculateRateFromWindow(
        CircularBuffer<TimestampedReading> buffer,
        TimestampedReading current,
        ChannelConfig channelConfig)
    {
        if (buffer.Count < 2)
        {
            // Not enough data for any rate calculation
            return 0.0;
        }

        var windowSeconds = channelConfig.RateWindowSeconds;
        var cutoffTime = current.Timestamp.AddSeconds(-windowSeconds);

        // Find the best reference reading within the window
        var referenceReading = FindBestReferenceReading(buffer, cutoffTime, current);
        if (referenceReading == null)
        {
            // Fallback to shortest available window if configured window is too large
            var minWindowSeconds = Math.Min(windowSeconds, 10); // Minimum 10 seconds
            var fallbackCutoffTime = current.Timestamp.AddSeconds(-minWindowSeconds);
            referenceReading = FindBestReferenceReading(buffer, fallbackCutoffTime, current);

            if (referenceReading == null)
            {
                // Still no suitable reference, return 0
                return 0.0;
            }
        }

        return CalculateRateWithOverflowDetection(referenceReading, current, channelConfig);
    }

    private TimestampedReading? FindBestReferenceReading(
        CircularBuffer<TimestampedReading> buffer,
        DateTimeOffset cutoffTime,
        TimestampedReading current)
    {
        // Get readings within the time window, newest first
        var windowReadings = buffer.GetItemsWithinWindow(cutoffTime, r => r.Timestamp);

        if (windowReadings.Count < 2)
        {
            return null; // Need at least current + one reference
        }

        // Find the oldest reading in the window (best for rate calculation)
        // Skip the current reading (first in list) and get the oldest available
        return windowReadings.LastOrDefault();
    }

    private double CalculateRateWithOverflowDetection(
        TimestampedReading reference,
        TimestampedReading current,
        ChannelConfig channelConfig)
    {
        var timeDiff = (current.Timestamp - reference.Timestamp).TotalSeconds;
        if (timeDiff <= 0)
        {
            _logger.LogWarning(
                "Invalid time difference for windowed rate calculation: {TimeDiff}s for {DeviceId} channel {Channel}",
                timeDiff, current.DeviceId, current.Channel);
            return 0.0;
        }

        // Handle counter overflow
        long valueDiff = current.RawValue - reference.RawValue;

        // Determine maximum counter value based on register count
        var maxValue = channelConfig.RegisterCount switch
        {
            1 => Counter16BitMax,
            2 => Counter32BitMax,
            _ => Counter32BitMax
        };

        // Detect overflow: large negative difference indicates counter wrapped around
        if (valueDiff < 0 && Math.Abs(valueDiff) > (maxValue / 2))
        {
            // Counter wrapped around
            valueDiff = (maxValue + 1) + valueDiff;

            _logger.LogDebug(
                "Counter overflow detected in windowed calculation for {DeviceId} channel {Channel}: " +
                "ref={Reference} ({RefTime}), curr={Current} ({CurrTime}), adjusted diff={Diff}, window={Window}s",
                current.DeviceId, current.Channel,
                reference.RawValue, reference.Timestamp,
                current.RawValue, current.Timestamp,
                valueDiff, timeDiff);
        }

        // Calculate rate (units per second) with scaling
        var rate = (valueDiff / timeDiff) * channelConfig.ScaleFactor;

        // Apply rate limits if configured
        if (channelConfig.MaxChangeRate.HasValue && Math.Abs(rate) > channelConfig.MaxChangeRate.Value)
        {
            _logger.LogWarning(
                "Windowed rate {Rate:F2} exceeds max change rate {MaxRate} for {DeviceId} channel {Channel} " +
                "(window: {Window}s, time diff: {TimeDiff:F1}s)",
                rate, channelConfig.MaxChangeRate.Value, current.DeviceId, current.Channel,
                channelConfig.RateWindowSeconds, timeDiff);

            // Could return 0 or clamp to max rate - returning calculated rate with warning for now
        }

        _logger.LogTrace(
            "Windowed rate calculated for {DeviceId} channel {Channel}: {Rate:F3} units/sec " +
            "(window: {Window}s, actual: {ActualWindow:F1}s, value change: {ValueChange})",
            current.DeviceId, current.Channel, rate,
            channelConfig.RateWindowSeconds, timeDiff, valueDiff);

        return rate;
    }

    private void PerformCleanup(object? state)
    {
        if (_disposed)
            return;

        lock (_cleanupLock)
        {
            try
            {
                var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-MaxBufferAgeMinutes);
                var channelsToRemove = new List<string>();

                foreach (var kvp in _channelBuffers)
                {
                    var channelKey = kvp.Key;
                    var buffer = kvp.Value;

                    if (buffer.IsEmpty)
                    {
                        channelsToRemove.Add(channelKey);
                        continue;
                    }

                    // Check if newest reading is too old
                    try
                    {
                        var newestReading = buffer.PeekNewest();
                        if (newestReading.Timestamp < cutoffTime)
                        {
                            channelsToRemove.Add(channelKey);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Buffer became empty during check
                        channelsToRemove.Add(channelKey);
                    }
                }

                // Remove old channel buffers
                foreach (var channelKey in channelsToRemove)
                {
                    if (_channelBuffers.TryRemove(channelKey, out _))
                    {
                        _logger.LogDebug("Cleaned up inactive channel buffer: {ChannelKey}", channelKey);
                    }
                }

                if (channelsToRemove.Count > 0)
                {
                    _logger.LogDebug("Windowed rate calculator cleanup: removed {Count} inactive channel buffers",
                        channelsToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during windowed rate calculator cleanup");
            }
        }
    }

    private static string GetChannelKey(string deviceId, int channel)
    {
        // Use string.Create for better performance in .NET 9
        return string.Create(deviceId.Length + 1 + channel.ToString().Length,
            (deviceId, channel),
            (span, state) =>
            {
                state.deviceId.AsSpan().CopyTo(span);
                span[state.deviceId.Length] = ':';
                state.channel.TryFormat(span[(state.deviceId.Length + 1)..], out _);
            });
    }

    /// <summary>
    /// Dispose resources and cleanup timers
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _cleanupTimer?.Dispose();

        lock (_cleanupLock)
        {
            // Clear all buffers
            foreach (var buffer in _channelBuffers.Values)
            {
                buffer.Clear();
            }
            _channelBuffers.Clear();
        }

        _logger.LogDebug("WindowedRateCalculator disposed");
    }
}

/// <summary>
/// Statistics about a channel's windowed rate calculation buffer
/// </summary>
public sealed record WindowedRateStatistics
{
    /// <summary>
    /// Channel identifier (DeviceId:Channel)
    /// </summary>
    public required string ChannelKey { get; init; }

    /// <summary>
    /// Current number of readings in buffer
    /// </summary>
    public required int BufferCount { get; init; }

    /// <summary>
    /// Maximum buffer capacity
    /// </summary>
    public required int BufferCapacity { get; init; }

    /// <summary>
    /// Whether buffer has any data
    /// </summary>
    public required bool HasData { get; init; }

    /// <summary>
    /// Timestamp of oldest reading in buffer
    /// </summary>
    public DateTimeOffset? OldestTimestamp { get; init; }

    /// <summary>
    /// Timestamp of newest reading in buffer
    /// </summary>
    public DateTimeOffset? NewestTimestamp { get; init; }

    /// <summary>
    /// Available time window in seconds
    /// </summary>
    public double AvailableWindowSeconds =>
        OldestTimestamp.HasValue && NewestTimestamp.HasValue
            ? (NewestTimestamp.Value - OldestTimestamp.Value).TotalSeconds
            : 0.0;
}
