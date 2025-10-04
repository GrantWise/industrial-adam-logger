using System.Collections.Concurrent;

namespace Industrial.Adam.Logger.Core.Mqtt;

/// <summary>
/// Monitors MQTT connection and message processing health with per-topic statistics.
/// Provides thread-safe statistics tracking for MQTT service observability.
/// </summary>
public sealed class MqttHealthMonitor
{
    private readonly DateTimeOffset _serviceStartTime;
    private long _messagesReceived;
    private long _messagesProcessed;
    private long _messagesFailed;
    private readonly ConcurrentDictionary<string, TopicStatistics> _topicStats;

    /// <summary>
    /// Initializes a new instance of the <see cref="MqttHealthMonitor"/> class.
    /// </summary>
    public MqttHealthMonitor()
    {
        _serviceStartTime = DateTimeOffset.UtcNow;
        _topicStats = new ConcurrentDictionary<string, TopicStatistics>();
    }

    /// <summary>
    /// Records a message received event.
    /// </summary>
    /// <param name="topic">The MQTT topic the message was received on.</param>
    public void RecordMessageReceived(string topic)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);

        Interlocked.Increment(ref _messagesReceived);

        var stats = _topicStats.GetOrAdd(topic, _ => new TopicStatistics());
        Interlocked.Increment(ref stats.MessagesReceived);
        stats.LastMessageTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records a message successfully processed event.
    /// </summary>
    /// <param name="topic">The MQTT topic the message was received on.</param>
    public void RecordMessageProcessed(string topic)
    {
        Interlocked.Increment(ref _messagesProcessed);

        if (_topicStats.TryGetValue(topic, out var stats))
        {
            Interlocked.Increment(ref stats.MessagesProcessed);
        }
    }

    /// <summary>
    /// Records a message processing failure event.
    /// </summary>
    /// <param name="topic">The MQTT topic the message was received on.</param>
    public void RecordMessageFailed(string topic)
    {
        Interlocked.Increment(ref _messagesFailed);

        if (_topicStats.TryGetValue(topic, out var stats))
        {
            Interlocked.Increment(ref stats.MessagesFailed);
        }
    }

    /// <summary>
    /// Gets overall service health status.
    /// </summary>
    /// <param name="isConnected">Whether MQTT client is currently connected.</param>
    /// <param name="configuredDevices">Number of enabled devices configured.</param>
    /// <returns>Health status information.</returns>
    public MqttServiceHealth GetHealthStatus(bool isConnected, int configuredDevices)
    {
        return new MqttServiceHealth
        {
            IsConnected = isConnected,
            MessagesReceived = Interlocked.Read(ref _messagesReceived),
            MessagesProcessed = Interlocked.Read(ref _messagesProcessed),
            MessagesFailed = Interlocked.Read(ref _messagesFailed),
            Uptime = DateTimeOffset.UtcNow - _serviceStartTime,
            ConfiguredDevices = configuredDevices,
            TopicStatistics = GetTopicStatistics()
        };
    }

    /// <summary>
    /// Gets per-topic statistics.
    /// </summary>
    /// <returns>Dictionary of topic to statistics.</returns>
    public Dictionary<string, TopicStats> GetTopicStatistics()
    {
        var result = new Dictionary<string, TopicStats>();

        foreach (var (topic, stats) in _topicStats)
        {
            result[topic] = new TopicStats
            {
                MessagesReceived = Interlocked.Read(ref stats.MessagesReceived),
                MessagesProcessed = Interlocked.Read(ref stats.MessagesProcessed),
                MessagesFailed = Interlocked.Read(ref stats.MessagesFailed),
                LastMessageTime = stats.LastMessageTime
            };
        }

        return result;
    }

    /// <summary>
    /// Resets all statistics (for testing or service restart).
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _messagesReceived, 0);
        Interlocked.Exchange(ref _messagesProcessed, 0);
        Interlocked.Exchange(ref _messagesFailed, 0);
        _topicStats.Clear();
    }
}

/// <summary>
/// Internal mutable statistics for per-topic tracking.
/// </summary>
internal sealed class TopicStatistics
{
    public long MessagesReceived;
    public long MessagesProcessed;
    public long MessagesFailed;
    public DateTimeOffset LastMessageTime;
}

/// <summary>
/// MQTT service health status information.
/// </summary>
public sealed record MqttServiceHealth
{
    /// <summary>
    /// Whether MQTT client is currently connected to broker.
    /// </summary>
    public required bool IsConnected { get; init; }

    /// <summary>
    /// Total messages received from broker.
    /// </summary>
    public required long MessagesReceived { get; init; }

    /// <summary>
    /// Messages successfully processed and stored.
    /// </summary>
    public required long MessagesProcessed { get; init; }

    /// <summary>
    /// Messages that failed processing or storage.
    /// </summary>
    public required long MessagesFailed { get; init; }

    /// <summary>
    /// Service uptime duration.
    /// </summary>
    public required TimeSpan Uptime { get; init; }

    /// <summary>
    /// Number of enabled MQTT devices configured.
    /// </summary>
    public required int ConfiguredDevices { get; init; }

    /// <summary>
    /// Per-topic statistics.
    /// </summary>
    public Dictionary<string, TopicStats> TopicStatistics { get; init; } = [];
}

/// <summary>
/// Statistics for a specific MQTT topic.
/// </summary>
public sealed record TopicStats
{
    /// <summary>
    /// Messages received on this topic.
    /// </summary>
    public required long MessagesReceived { get; init; }

    /// <summary>
    /// Messages successfully processed from this topic.
    /// </summary>
    public required long MessagesProcessed { get; init; }

    /// <summary>
    /// Messages that failed processing from this topic.
    /// </summary>
    public required long MessagesFailed { get; init; }

    /// <summary>
    /// Timestamp of last message received on this topic.
    /// </summary>
    public DateTimeOffset? LastMessageTime { get; init; }
}
