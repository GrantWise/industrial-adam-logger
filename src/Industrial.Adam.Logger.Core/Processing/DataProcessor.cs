using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Microsoft.Extensions.Logging;

namespace Industrial.Adam.Logger.Core.Processing;

/// <summary>
/// Processes device readings with counter overflow detection and windowed rate calculations
/// </summary>
public sealed class DataProcessor : IDataProcessor, IDisposable
{
    private readonly ILogger<DataProcessor> _logger;
    private readonly Dictionary<string, ChannelConfig> _channelConfigs;
    private readonly WindowedRateCalculator? _rateCalculator;
    private readonly bool _useWindowedCalculation;
    private bool _disposed;

    // Counter limits for overflow detection
    private const long Counter16BitMax = 65535;
    private const long Counter32BitMax = 4294967295;

    /// <summary>
    /// Initialize the data processor with windowed rate calculation
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">Logger configuration</param>
    /// <param name="useWindowedCalculation">Use windowed rate calculation (false for testing)</param>
    public DataProcessor(ILogger<DataProcessor> logger, LoggerConfiguration configuration, bool useWindowedCalculation = true)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        _useWindowedCalculation = useWindowedCalculation;

        // Initialize windowed rate calculator only if needed
        if (_useWindowedCalculation)
        {
            _rateCalculator = new WindowedRateCalculator(_logger);
        }

        // Build channel config lookup
        _channelConfigs = new Dictionary<string, ChannelConfig>();
        foreach (var device in configuration.Devices)
        {
            foreach (var channel in device.Channels)
            {
                var key = GetChannelKey(device.DeviceId, channel.ChannelNumber);
                _channelConfigs[key] = channel;
            }
        }
    }

    /// <summary>
    /// Process a raw device reading with overflow detection and rate calculation
    /// </summary>
    public DeviceReading ProcessReading(DeviceReading reading, DeviceReading? previousReading = null)
    {
        var channelKey = GetChannelKey(reading.DeviceId, reading.Channel);
        if (!_channelConfigs.TryGetValue(channelKey, out var channelConfig))
        {
            _logger.LogWarning(
                "No configuration found for device {DeviceId} channel {Channel}",
                reading.DeviceId, reading.Channel);
            return reading;
        }

        // Create a new reading with processed values
        var processed = reading with
        {
            ProcessedValue = reading.RawValue * channelConfig.ScaleFactor,
            Quality = DataQuality.Good
        };

        // Calculate rate using appropriate method
        double? calculatedRate = null;
        if (_useWindowedCalculation && _rateCalculator != null)
        {
            // Use windowed rate calculation for production
            var windowedRate = _rateCalculator.CalculateWindowedRate(processed, channelConfig);
            calculatedRate = windowedRate;

            // Log rate calculation details at trace level
            _logger.LogTrace(
                "Calculated windowed rate for {DeviceId} channel {Channel}: {Rate:F3} units/sec (window: {Window}s)",
                processed.DeviceId, processed.Channel, windowedRate, channelConfig.RateWindowSeconds);
        }
        else if (previousReading != null)
        {
            // Use simple point-to-point calculation for tests
            calculatedRate = CalculateSimpleRate(reading, previousReading, channelConfig);
        }

        processed = processed with { Rate = calculatedRate };

        // Validate the processed reading and check rate limits
        if (!ValidateProcessedReading(processed, channelConfig))
        {
            // Only set to Bad if not already Degraded
            if (processed.Quality != DataQuality.Degraded)
            {
                processed = processed with { Quality = DataQuality.Bad };
            }
        }

        // Check rate limits for quality degradation
        if (calculatedRate.HasValue && channelConfig.MaxChangeRate.HasValue)
        {
            if (Math.Abs(calculatedRate.Value) > channelConfig.MaxChangeRate.Value)
            {
                processed = processed with { Quality = DataQuality.Degraded };
                _logger.LogWarning(
                    "Rate {Rate:F2} exceeds max change rate {MaxRate} for {DeviceId} channel {Channel}",
                    calculatedRate.Value, channelConfig.MaxChangeRate.Value,
                    processed.DeviceId, processed.Channel);
            }
        }

        return processed;
    }

    /// <summary>
    /// Calculate simple point-to-point rate for testing
    /// </summary>
    private double CalculateSimpleRate(DeviceReading current, DeviceReading previous, ChannelConfig channelConfig)
    {
        var timeDiff = (current.Timestamp - previous.Timestamp).TotalSeconds;
        if (timeDiff <= 0)
        {
            return 0.0;
        }

        // Handle counter overflow
        long valueDiff = current.RawValue - previous.RawValue;

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
        }

        // Calculate rate (units per second) with scaling
        return (valueDiff / timeDiff) * channelConfig.ScaleFactor;
    }

    /// <summary>
    /// Get windowed rate statistics for a specific channel
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="channel">Channel number</param>
    /// <returns>Rate calculation statistics</returns>
    public WindowedRateStatistics? GetRateStatistics(string deviceId, int channel)
    {
        return _rateCalculator?.GetChannelStatistics(deviceId, channel);
    }

    /// <summary>
    /// Validate a processed reading against channel limits
    /// </summary>
    public bool ValidateReading(DeviceReading reading)
    {
        var channelKey = GetChannelKey(reading.DeviceId, reading.Channel);
        if (!_channelConfigs.TryGetValue(channelKey, out var channelConfig))
        {
            return false;
        }

        return ValidateProcessedReading(reading, channelConfig);
    }


    private bool ValidateProcessedReading(DeviceReading reading, ChannelConfig channelConfig)
    {
        // Check min/max limits if configured
        if (channelConfig.MinValue.HasValue && reading.ProcessedValue < channelConfig.MinValue.Value)
        {
            _logger.LogWarning(
                "Reading {Value} below minimum {Min} for {DeviceId} channel {Channel}",
                reading.ProcessedValue, channelConfig.MinValue.Value,
                reading.DeviceId, reading.Channel);
            return false;
        }

        if (channelConfig.MaxValue.HasValue && reading.ProcessedValue > channelConfig.MaxValue.Value)
        {
            _logger.LogWarning(
                "Reading {Value} above maximum {Max} for {DeviceId} channel {Channel}",
                reading.ProcessedValue, channelConfig.MaxValue.Value,
                reading.DeviceId, reading.Channel);
            return false;
        }

        // Check rate limits if available
        if (reading.Rate.HasValue && channelConfig.MaxChangeRate.HasValue)
        {
            if (Math.Abs(reading.Rate.Value) > channelConfig.MaxChangeRate.Value)
            {
                return false;
            }
        }

        return true;
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
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _rateCalculator?.Dispose();

        _logger.LogDebug("DataProcessor disposed");
    }
}
