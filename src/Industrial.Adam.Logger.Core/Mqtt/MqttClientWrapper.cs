using System.Net;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;

namespace Industrial.Adam.Logger.Core.Mqtt;

/// <summary>
/// Production implementation of IMqttClientWrapper using MQTTnet v4 ManagedClient.
/// Provides auto-reconnect, event handling, and lifecycle management.
/// </summary>
public sealed class MqttClientWrapper : IMqttClientWrapper
{
    private readonly IManagedMqttClient _managedClient;
    private readonly ILogger<MqttClientWrapper> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MqttClientWrapper"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public MqttClientWrapper(ILogger<MqttClientWrapper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Use MqttFactory as per v4 API
        var factory = new MqttFactory();
        _managedClient = factory.CreateManagedMqttClient();

        // Wire up internal events to public events
        _managedClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        _managedClient.ConnectedAsync += OnConnectedAsync;
        _managedClient.DisconnectedAsync += OnDisconnectedAsync;
    }

    /// <inheritdoc />
    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

    /// <inheritdoc />
    public event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync;

    /// <inheritdoc />
    public event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync;

    /// <inheritdoc />
    public bool IsConnected => _managedClient.IsConnected;

    /// <inheritdoc />
    public async Task StartAsync(ManagedMqttClientOptions options, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Starting MQTT managed client with broker {BrokerHost}:{BrokerPort}",
            GetBrokerHost(options), GetBrokerPort(options));

        try
        {
            await _managedClient.StartAsync(options).ConfigureAwait(false);
            _logger.LogInformation("MQTT managed client started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT managed client");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Stopping MQTT managed client");

        try
        {
            await _managedClient.StopAsync().ConfigureAwait(false);
            _logger.LogInformation("MQTT managed client stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MQTT managed client");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(IEnumerable<MqttTopicFilter> topicFilters, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var filters = topicFilters.ToList();
        _logger.LogInformation("Subscribing to {Count} MQTT topics: {Topics}",
            filters.Count, string.Join(", ", filters.Select(f => f.Topic)));

        try
        {
            await _managedClient.SubscribeAsync(filters).ConfigureAwait(false);
            _logger.LogInformation("Successfully subscribed to MQTT topics");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to MQTT topics");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(IEnumerable<string> topics, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var topicList = topics.ToList();
        _logger.LogInformation("Unsubscribing from {Count} MQTT topics: {Topics}",
            topicList.Count, string.Join(", ", topicList));

        try
        {
            await _managedClient.UnsubscribeAsync(topicList).ConfigureAwait(false);
            _logger.LogInformation("Successfully unsubscribed from MQTT topics");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from MQTT topics");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(MqttApplicationMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebug("Publishing message to topic {Topic}", message.Topic);

        try
        {
            await _managedClient.EnqueueAsync(message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to topic {Topic}", message.Topic);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing MQTT client wrapper");

        try
        {
            // Unsubscribe from events to prevent memory leaks
            _managedClient.ApplicationMessageReceivedAsync -= OnApplicationMessageReceivedAsync;
            _managedClient.ConnectedAsync -= OnConnectedAsync;
            _managedClient.DisconnectedAsync -= OnDisconnectedAsync;

            // Stop the client if still running
            if (_managedClient.IsStarted)
            {
                await _managedClient.StopAsync().ConfigureAwait(false);
            }

            // Dispose the managed client
            _managedClient.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MQTT client disposal");
        }
        finally
        {
            _disposed = true;
        }
    }

    private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            if (ApplicationMessageReceivedAsync != null)
            {
                await ApplicationMessageReceivedAsync(e).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ApplicationMessageReceivedAsync handler for topic {Topic}",
                e.ApplicationMessage.Topic);
        }
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        try
        {
            _logger.LogInformation("MQTT client connected to broker");

            if (ConnectedAsync != null)
            {
                await ConnectedAsync(e).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ConnectedAsync handler");
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        try
        {
            _logger.LogWarning("MQTT client disconnected from broker. Reason: {Reason}", e.Reason);

            if (DisconnectedAsync != null)
            {
                await DisconnectedAsync(e).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DisconnectedAsync handler");
        }
    }

    private static string GetBrokerHost(ManagedMqttClientOptions options)
    {
        // Extract broker host from client options for logging
        if (options.ClientOptions is MqttClientOptions clientOptions)
        {
            if (clientOptions.ChannelOptions is MqttClientTcpOptions tcpOptions)
            {
                // Use RemoteEndpoint as Server/Port are obsolete in v4
                if (tcpOptions.RemoteEndpoint is DnsEndPoint dnsEndPoint)
                {
                    return dnsEndPoint.Host;
                }
                if (tcpOptions.RemoteEndpoint is IPEndPoint ipEndPoint)
                {
                    return ipEndPoint.Address.ToString();
                }
            }
        }
        return "unknown";
    }

    private static int GetBrokerPort(ManagedMqttClientOptions options)
    {
        // Extract broker port from client options for logging
        if (options.ClientOptions is MqttClientOptions clientOptions)
        {
            if (clientOptions.ChannelOptions is MqttClientTcpOptions tcpOptions)
            {
                // Use RemoteEndpoint as Server/Port are obsolete in v4
                if (tcpOptions.RemoteEndpoint is DnsEndPoint dnsEndPoint)
                {
                    return dnsEndPoint.Port;
                }
                if (tcpOptions.RemoteEndpoint is IPEndPoint ipEndPoint)
                {
                    return ipEndPoint.Port;
                }
            }
        }
        return 1883;
    }
}
