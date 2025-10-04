using FluentAssertions;
using Industrial.Adam.Logger.Core.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Mqtt;

public class MqttHealthMonitorTests
{
    [Fact]
    public void RecordMessageReceived_ValidTopic_IncrementsCounters()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();

        // Act
        monitor.RecordMessageReceived("test/topic");

        // Assert
        var health = monitor.GetHealthStatus(true, 1);
        health.MessagesReceived.Should().Be(1);
        health.TopicStatistics.Should().ContainKey("test/topic");
        health.TopicStatistics["test/topic"].MessagesReceived.Should().Be(1);
    }

    [Fact]
    public void RecordMessageReceived_NullTopic_ThrowsArgumentException()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();

        // Act
        var act = () => monitor.RecordMessageReceived(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordMessageReceived_EmptyTopic_ThrowsArgumentException()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();

        // Act
        var act = () => monitor.RecordMessageReceived(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordMessageProcessed_ValidTopic_IncrementsCounter()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();
        monitor.RecordMessageReceived("test/topic");

        // Act
        monitor.RecordMessageProcessed("test/topic");

        // Assert
        var health = monitor.GetHealthStatus(true, 1);
        health.MessagesProcessed.Should().Be(1);
        health.TopicStatistics["test/topic"].MessagesProcessed.Should().Be(1);
    }

    [Fact]
    public void RecordMessageFailed_ValidTopic_IncrementsCounter()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();
        monitor.RecordMessageReceived("test/topic");

        // Act
        monitor.RecordMessageFailed("test/topic");

        // Assert
        var health = monitor.GetHealthStatus(true, 1);
        health.MessagesFailed.Should().Be(1);
        health.TopicStatistics["test/topic"].MessagesFailed.Should().Be(1);
    }

    [Fact]
    public void RecordMessageReceived_MultipleTopics_TracksAll()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();
        monitor.RecordMessageReceived("topic1");
        monitor.RecordMessageReceived("topic2");
        monitor.RecordMessageReceived("topic3");

        // Assert
        var health = monitor.GetHealthStatus(true, 1);
        health.TopicStatistics.Should().HaveCount(3);
        health.TopicStatistics.Should().ContainKey("topic1");
        health.TopicStatistics.Should().ContainKey("topic2");
        health.TopicStatistics.Should().ContainKey("topic3");
        health.MessagesReceived.Should().Be(3);
    }

    [Fact]
    public void GetHealthStatus_ReturnsCorrectData()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();
        monitor.RecordMessageReceived("test/topic");
        monitor.RecordMessageProcessed("test/topic");

        // Act
        var health = monitor.GetHealthStatus(isConnected: true, configuredDevices: 5);

        // Assert
        health.IsConnected.Should().BeTrue();
        health.ConfiguredDevices.Should().Be(5);
        health.MessagesReceived.Should().Be(1);
        health.MessagesProcessed.Should().Be(1);
        health.MessagesFailed.Should().Be(0);
        health.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetTopicStatistics_ReturnsAllTopics()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();
        monitor.RecordMessageReceived("topic1");
        monitor.RecordMessageReceived("topic2");
        monitor.RecordMessageProcessed("topic1");

        // Act
        var stats = monitor.GetTopicStatistics();

        // Assert
        stats.Should().HaveCount(2);
        stats["topic1"].MessagesReceived.Should().Be(1);
        stats["topic1"].MessagesProcessed.Should().Be(1);
        stats["topic2"].MessagesReceived.Should().Be(1);
        stats["topic2"].MessagesProcessed.Should().Be(0);
    }

    [Fact]
    public void Reset_ClearsAllStatistics()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();
        monitor.RecordMessageReceived("test/topic");
        monitor.RecordMessageProcessed("test/topic");

        // Act
        monitor.Reset();

        // Assert
        var health = monitor.GetHealthStatus(true, 1);
        health.MessagesReceived.Should().Be(0);
        health.MessagesProcessed.Should().Be(0);
        health.MessagesFailed.Should().Be(0);
        health.TopicStatistics.Should().BeEmpty();
    }

    [Fact]
    public void LastMessageTime_IsRecorded()
    {
        // Arrange
        var monitor = new MqttHealthMonitor();
        var beforeTime = DateTimeOffset.UtcNow;

        // Act
        monitor.RecordMessageReceived("test/topic");

        // Assert
        var stats = monitor.GetTopicStatistics();
        stats["test/topic"].LastMessageTime.Should().NotBeNull();
        stats["test/topic"].LastMessageTime.Should().BeOnOrAfter(beforeTime);
    }
}
