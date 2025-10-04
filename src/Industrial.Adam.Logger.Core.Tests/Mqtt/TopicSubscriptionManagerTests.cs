using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Mqtt;

public class TopicSubscriptionManagerTests
{
    private readonly TopicSubscriptionManager _manager;

    public TopicSubscriptionManagerTests()
    {
        _manager = new TopicSubscriptionManager(NullLogger<TopicSubscriptionManager>.Instance);
    }

    [Fact]
    public void RegisterDevices_ValidDevices_RegistersSuccessfully()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["test/topic1"] },
            new() { DeviceId = "DEV2", Enabled = true, Topics = ["test/topic2"] }
        };

        // Act
        _manager.RegisterDevices(devices);

        // Assert
        var result1 = _manager.FindDeviceForTopic("test/topic1");
        var result2 = _manager.FindDeviceForTopic("test/topic2");
        result1.Should().NotBeNull();
        result1!.DeviceId.Should().Be("DEV1");
        result2.Should().NotBeNull();
        result2!.DeviceId.Should().Be("DEV2");
    }

    [Fact]
    public void RegisterDevices_DisabledDevice_DoesNotRegister()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = false, Topics = ["test/topic1"] }
        };

        // Act
        _manager.RegisterDevices(devices);

        // Assert
        var result = _manager.FindDeviceForTopic("test/topic1");
        result.Should().BeNull();
    }

    [Fact]
    public void RegisterDevices_NullDevices_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _manager.RegisterDevices(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterDevices_EmptyTopic_SkipsTopic()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["", "test/valid"] }
        };

        // Act
        _manager.RegisterDevices(devices);

        // Assert - empty topic should be skipped, valid topic should work
        var result = _manager.FindDeviceForTopic("test/valid");
        result.Should().NotBeNull();
    }

    [Fact]
    public void FindDeviceForTopic_ExactMatch_ReturnsDevice()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["test/exact"] }
        };
        _manager.RegisterDevices(devices);

        // Act
        var result = _manager.FindDeviceForTopic("test/exact");

        // Assert
        result.Should().NotBeNull();
        result!.DeviceId.Should().Be("DEV1");
    }

    [Fact]
    public void FindDeviceForTopic_WildcardSingleLevel_MatchesCorrectly()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["test/+/data"] }
        };
        _manager.RegisterDevices(devices);

        // Act
        var result1 = _manager.FindDeviceForTopic("test/sensor1/data");
        var result2 = _manager.FindDeviceForTopic("test/sensor2/data");
        var result3 = _manager.FindDeviceForTopic("test/sensor1/other");

        // Assert
        result1.Should().NotBeNull();
        result1!.DeviceId.Should().Be("DEV1");
        result2.Should().NotBeNull();
        result2!.DeviceId.Should().Be("DEV1");
        result3.Should().BeNull(); // Doesn't match pattern
    }

    [Fact]
    public void FindDeviceForTopic_WildcardMultiLevel_MatchesCorrectly()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["test/#"] }
        };
        _manager.RegisterDevices(devices);

        // Act
        var result1 = _manager.FindDeviceForTopic("test/sensor1");
        var result2 = _manager.FindDeviceForTopic("test/sensor1/data/value");
        var result3 = _manager.FindDeviceForTopic("other/topic");

        // Assert
        result1.Should().NotBeNull();
        result1!.DeviceId.Should().Be("DEV1");
        result2.Should().NotBeNull();
        result2!.DeviceId.Should().Be("DEV1");
        result3.Should().BeNull(); // Doesn't match pattern
    }

    [Fact]
    public void FindDeviceForTopic_NullTopic_ThrowsArgumentException()
    {
        // Act
        var act = () => _manager.FindDeviceForTopic(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FindDeviceForTopic_EmptyTopic_ThrowsArgumentException()
    {
        // Act
        var act = () => _manager.FindDeviceForTopic(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FindDeviceForTopic_NoMatch_ReturnsNull()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["test/topic"] }
        };
        _manager.RegisterDevices(devices);

        // Act
        var result = _manager.FindDeviceForTopic("other/topic");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void BuildTopicFilters_CreatesFiltersWithCorrectQoS()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["test/topic1"], QosLevel = 2 },
            new() { DeviceId = "DEV2", Enabled = true, Topics = ["test/topic2"], QosLevel = null }
        };

        // Act
        var filters = _manager.BuildTopicFilters(devices, globalQos: 1);

        // Assert
        filters.Should().HaveCount(2);
        filters[0].Topic.Should().Be("test/topic1");
        filters[0].QualityOfServiceLevel.Should().Be(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce);
        filters[1].Topic.Should().Be("test/topic2");
        filters[1].QualityOfServiceLevel.Should().Be(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
    }

    [Fact]
    public void BuildTopicFilters_SharedTopic_UsesHigherQoS()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["shared/topic"], QosLevel = 0 },
            new() { DeviceId = "DEV2", Enabled = true, Topics = ["shared/topic"], QosLevel = 2 }
        };

        // Act
        var filters = _manager.BuildTopicFilters(devices, globalQos: 1);

        // Assert
        filters.Should().HaveCount(1);
        filters[0].Topic.Should().Be("shared/topic");
        filters[0].QualityOfServiceLevel.Should().Be(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce);
    }

    [Fact]
    public void GetAllTopics_ReturnsUniqueTopics()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["topic1", "topic2"] },
            new() { DeviceId = "DEV2", Enabled = true, Topics = ["topic2", "topic3"] }
        };

        // Act
        var topics = _manager.GetAllTopics(devices);

        // Assert
        topics.Should().HaveCount(3);
        topics.Should().Contain("topic1");
        topics.Should().Contain("topic2");
        topics.Should().Contain("topic3");
    }

    [Fact]
    public async Task RegisterDevices_ThreadSafe_ConcurrentAccess()
    {
        // Arrange
        var devices = new List<MqttDeviceConfig>
        {
            new() { DeviceId = "DEV1", Enabled = true, Topics = ["test/topic"] }
        };

        // Act - concurrent registration and lookup
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            _manager.RegisterDevices(devices);
            var result = _manager.FindDeviceForTopic("test/topic");
            return result != null;
        }));

        var results = await Task.WhenAll(tasks);

        // Assert - all operations should succeed
        results.Should().AllBeEquivalentTo(true);
    }
}
