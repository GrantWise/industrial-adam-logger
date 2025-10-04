using Industrial.Adam.Logger.Core.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace Industrial.Adam.Logger.Core.Mqtt;

/// <summary>
/// Manages MQTT topic subscriptions and routes incoming messages to device configurations.
/// Provides optimized O(1) topic lookup and handles wildcard matching.
/// </summary>
public sealed class TopicSubscriptionManager
{
    private readonly ILogger<TopicSubscriptionManager> _logger;
    private readonly Dictionary<string, MqttDeviceConfig> _exactTopicLookup;
    private readonly List<(string pattern, MqttDeviceConfig device)> _wildcardTopics;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicSubscriptionManager"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public TopicSubscriptionManager(ILogger<TopicSubscriptionManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exactTopicLookup = new Dictionary<string, MqttDeviceConfig>();
        _wildcardTopics = new List<(string, MqttDeviceConfig)>();
    }

    /// <summary>
    /// Registers device configurations and builds topic lookup structures.
    /// </summary>
    /// <param name="devices">List of MQTT device configurations.</param>
    public void RegisterDevices(IEnumerable<MqttDeviceConfig> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        _exactTopicLookup.Clear();
        _wildcardTopics.Clear();

        foreach (var device in devices.Where(d => d.Enabled))
        {
            foreach (var topic in device.Topics)
            {
                if (IsWildcardTopic(topic))
                {
                    // Wildcard topics need pattern matching
                    _wildcardTopics.Add((topic, device));
                    _logger.LogDebug("Registered wildcard topic {Topic} for device {DeviceId}",
                        topic, device.DeviceId);
                }
                else
                {
                    // Exact topics go into O(1) lookup dictionary
                    if (_exactTopicLookup.TryAdd(topic, device))
                    {
                        _logger.LogDebug("Registered exact topic {Topic} for device {DeviceId}",
                            topic, device.DeviceId);
                    }
                    else
                    {
                        _logger.LogWarning("Duplicate topic {Topic} configured for multiple devices. " +
                            "Using first registered device {DeviceId}",
                            topic, _exactTopicLookup[topic].DeviceId);
                    }
                }
            }
        }

        _logger.LogInformation("Registered {ExactCount} exact topics and {WildcardCount} wildcard patterns",
            _exactTopicLookup.Count, _wildcardTopics.Count);
    }

    /// <summary>
    /// Finds the device configuration for a given topic.
    /// </summary>
    /// <param name="topic">The MQTT topic to match.</param>
    /// <returns>Device configuration if matched, null otherwise.</returns>
    public MqttDeviceConfig? FindDeviceForTopic(string topic)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);

        // Try exact match first (O(1))
        if (_exactTopicLookup.TryGetValue(topic, out var device))
        {
            return device;
        }

        // Fall back to wildcard matching (O(n) where n = wildcard patterns)
        foreach (var (pattern, wildcardDevice) in _wildcardTopics)
        {
            if (MqttTopicFilterComparer.Compare(topic, pattern) == MqttTopicFilterCompareResult.IsMatch)
            {
                return wildcardDevice;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds topic filter list for subscription with per-device QoS support.
    /// </summary>
    /// <param name="devices">List of MQTT device configurations.</param>
    /// <param name="globalQos">Global QoS level from MqttSettings.</param>
    /// <returns>List of topic filters ready for subscription.</returns>
    public List<MqttTopicFilter> BuildTopicFilters(IEnumerable<MqttDeviceConfig> devices, int globalQos)
    {
        ArgumentNullException.ThrowIfNull(devices);

        var enabledDevices = devices.Where(d => d.Enabled).ToList();

        // Get unique topics with their associated devices
        var topicDeviceMap = new Dictionary<string, (MqttDeviceConfig device, int qos)>();

        foreach (var device in enabledDevices)
        {
            // Determine QoS for this device
            var deviceQos = device.QosLevel ?? globalQos;

            foreach (var topic in device.Topics)
            {
                if (!topicDeviceMap.ContainsKey(topic))
                {
                    topicDeviceMap[topic] = (device, deviceQos);
                }
                else
                {
                    // If topic is shared between devices, use higher QoS
                    var existing = topicDeviceMap[topic];
                    if (deviceQos > existing.qos)
                    {
                        topicDeviceMap[topic] = (device, deviceQos);
                        _logger.LogDebug("Topic {Topic} shared by multiple devices, using higher QoS {Qos}",
                            topic, deviceQos);
                    }
                }
            }
        }

        // Build MqttTopicFilter list
        var topicFilters = new List<MqttTopicFilter>();

        foreach (var (topic, (device, qos)) in topicDeviceMap)
        {
            var mqttQos = qos switch
            {
                0 => MqttQualityOfServiceLevel.AtMostOnce,
                1 => MqttQualityOfServiceLevel.AtLeastOnce,
                2 => MqttQualityOfServiceLevel.ExactlyOnce,
                _ => MqttQualityOfServiceLevel.AtLeastOnce
            };

            var filter = new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(mqttQos)
                .Build();

            topicFilters.Add(filter);

            _logger.LogDebug("Created topic filter: {Topic} with QoS {Qos} for device {DeviceId}",
                topic, mqttQos, device.DeviceId);
        }

        _logger.LogInformation("Built {Count} topic filters for subscription", topicFilters.Count);

        return topicFilters;
    }

    /// <summary>
    /// Gets all unique topics from registered devices.
    /// </summary>
    /// <param name="devices">List of MQTT device configurations.</param>
    /// <returns>List of unique topics.</returns>
    public List<string> GetAllTopics(IEnumerable<MqttDeviceConfig> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        return devices
            .Where(d => d.Enabled)
            .SelectMany(d => d.Topics)
            .Distinct()
            .ToList();
    }

    private static bool IsWildcardTopic(string topic)
    {
        return topic.Contains('+') || topic.Contains('#');
    }
}
