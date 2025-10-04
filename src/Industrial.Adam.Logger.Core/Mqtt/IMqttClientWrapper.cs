using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;

namespace Industrial.Adam.Logger.Core.Mqtt;

/// <summary>
/// Abstraction over MQTTnet managed client for testability and lifecycle management.
/// Wraps MQTTnet v4 API with correct event signatures and factory patterns.
/// </summary>
public interface IMqttClientWrapper : IAsyncDisposable
{
    /// <summary>
    /// Event raised when a message is received from the broker.
    /// Signature: Func&lt;MqttApplicationMessageReceivedEventArgs, Task&gt;
    /// </summary>
    event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

    /// <summary>
    /// Event raised when the client connects to the broker.
    /// Signature: Func&lt;MqttClientConnectedEventArgs, Task&gt;
    /// </summary>
    event Func<MqttClientConnectedEventArgs, Task> ConnectedAsync;

    /// <summary>
    /// Event raised when the client disconnects from the broker.
    /// Signature: Func&lt;MqttClientDisconnectedEventArgs, Task&gt;
    /// </summary>
    event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync;

    /// <summary>
    /// Gets whether the client is currently connected to the broker.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Starts the managed MQTT client with auto-reconnect.
    /// </summary>
    /// <param name="options">Managed client options including broker settings and reconnect policy.</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
    Task StartAsync(ManagedMqttClientOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the managed MQTT client and disconnects from the broker.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to one or more MQTT topics.
    /// </summary>
    /// <param name="topicFilters">Topic filters to subscribe to (supports wildcards + and #).</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
    Task SubscribeAsync(IEnumerable<MqttTopicFilter> topicFilters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from one or more MQTT topics.
    /// </summary>
    /// <param name="topics">Topics to unsubscribe from.</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
    Task UnsubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to the broker.
    /// </summary>
    /// <param name="message">Application message to publish.</param>
    /// <param name="cancellationToken">Cancellation token to stop the operation.</param>
    Task PublishAsync(MqttApplicationMessage message, CancellationToken cancellationToken = default);
}
