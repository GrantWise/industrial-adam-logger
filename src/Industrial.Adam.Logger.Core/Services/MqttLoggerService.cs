using System.Threading.Channels;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Models;
using Industrial.Adam.Logger.Core.Mqtt;
using Industrial.Adam.Logger.Core.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;

namespace Industrial.Adam.Logger.Core.Services;

/// <summary>
/// Background service that manages MQTT client connections, processes incoming messages,
/// and persists data to TimescaleDB via storage layer.
/// </summary>
public sealed class MqttLoggerService : BackgroundService
{
    private readonly IMqttClientWrapper _mqttClient;
    private readonly MqttConnectionFactory _connectionFactory;
    private readonly TopicSubscriptionManager _subscriptionManager;
    private readonly MqttMessageProcessor _messageProcessor;
    private readonly MqttHealthMonitor _healthMonitor;
    private readonly ITimescaleStorage _storage;
    private readonly DeadLetterQueue _deadLetterQueue;
    private readonly LoggerConfiguration _config;
    private readonly ILogger<MqttLoggerService> _logger;

    // Channel for batching readings before storage
    private readonly Channel<DeviceReading> _readingChannel;
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="MqttLoggerService"/> class.
    /// </summary>
    /// <param name="mqttClient">MQTT client wrapper for broker communication.</param>
    /// <param name="connectionFactory">Factory for creating MQTT connection options.</param>
    /// <param name="subscriptionManager">Manager for topic subscriptions and routing.</param>
    /// <param name="messageProcessor">Message processor for payload parsing.</param>
    /// <param name="healthMonitor">Health and statistics monitor.</param>
    /// <param name="storage">TimescaleDB storage interface.</param>
    /// <param name="deadLetterQueue">Dead letter queue for failed storage operations.</param>
    /// <param name="config">Logger configuration.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public MqttLoggerService(
        IMqttClientWrapper mqttClient,
        MqttConnectionFactory connectionFactory,
        TopicSubscriptionManager subscriptionManager,
        MqttMessageProcessor messageProcessor,
        MqttHealthMonitor healthMonitor,
        ITimescaleStorage storage,
        DeadLetterQueue deadLetterQueue,
        IOptions<LoggerConfiguration> config,
        ILogger<MqttLoggerService> logger)
    {
        _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _subscriptionManager = subscriptionManager ?? throw new ArgumentNullException(nameof(subscriptionManager));
        _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _deadLetterQueue = deadLetterQueue ?? throw new ArgumentNullException(nameof(deadLetterQueue));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure batching (use TimescaleDB batch settings)
        _batchSize = _config.TimescaleDb?.BatchSize ?? 100;
        _batchTimeout = TimeSpan.FromMilliseconds(_config.TimescaleDb?.BatchTimeoutMs ?? 5000);

        // Create bounded channel with backpressure handling
        _readingChannel = Channel.CreateBounded<DeviceReading>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Executes the MQTT logger service.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to signal service shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_config.Mqtt == null || _config.MqttDevices.Count == 0)
        {
            _logger.LogInformation("MQTT not configured, service will remain idle");
            return;
        }

        _logger.LogInformation("Starting MQTT logger service with {DeviceCount} devices", _config.MqttDevices.Count);

        try
        {
            // Register devices with subscription manager for topic routing
            _subscriptionManager.RegisterDevices(_config.MqttDevices);

            // Wire up event handlers
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

            // Build managed client options using factory
            var managedOptions = _connectionFactory.CreateManagedClientOptions(_config.Mqtt);

            // Start MQTT client
            await _mqttClient.StartAsync(managedOptions, stoppingToken).ConfigureAwait(false);

            // Subscribe to configured topics
            await SubscribeToTopicsAsync(stoppingToken).ConfigureAwait(false);

            // Start batch processing task
            await ProcessReadingsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MQTT logger service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MQTT logger service");
            throw;
        }
        finally
        {
            await ShutdownAsync().ConfigureAwait(false);
        }
    }

    private async Task SubscribeToTopicsAsync(CancellationToken cancellationToken)
    {
        // Build topic filters with per-device QoS support
        var topicFilters = _subscriptionManager.BuildTopicFilters(
            _config.MqttDevices,
            _config.Mqtt!.QualityOfServiceLevel);

        if (topicFilters.Count == 0)
        {
            _logger.LogWarning("No topics configured for subscription");
            return;
        }

        await _mqttClient.SubscribeAsync(topicFilters, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Subscribed to {TopicCount} topics", topicFilters.Count);
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        _healthMonitor.RecordMessageReceived(topic);

        try
        {
            var payload = e.ApplicationMessage.PayloadSegment;

            // Find matching device configuration using subscription manager
            var deviceConfig = _subscriptionManager.FindDeviceForTopic(topic);
            if (deviceConfig == null)
            {
                _logger.LogDebug("No device configured for topic {Topic}", topic);
                return;
            }

            // Process message
            var reading = _messageProcessor.ProcessMessage(deviceConfig, topic, payload);
            if (reading == null)
            {
                _healthMonitor.RecordMessageFailed(topic);
                _logger.LogWarning("Failed to process message from topic {Topic}", topic);
                return;
            }

            // Enqueue for batching
            await _readingChannel.Writer.WriteAsync(reading).ConfigureAwait(false);
            _healthMonitor.RecordMessageProcessed(topic);
        }
        catch (Exception ex)
        {
            _healthMonitor.RecordMessageFailed(topic);
            _logger.LogError(ex, "Error processing MQTT message from topic {Topic}", topic);
        }
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        _logger.LogInformation("MQTT client connected to broker");
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning("MQTT client disconnected: {Reason}. Will auto-reconnect", e.Reason);
        return Task.CompletedTask;
    }

    private async Task ProcessReadingsAsync(CancellationToken stoppingToken)
    {
        var batch = new List<DeviceReading>(_batchSize);

        await foreach (var reading in _readingChannel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            batch.Add(reading);

            // Flush batch when size reached or on timeout
            if (batch.Count >= _batchSize)
            {
                await FlushBatchAsync(batch, stoppingToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Flush remaining on shutdown
        if (batch.Count > 0)
        {
            await FlushBatchAsync(batch, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task FlushBatchAsync(List<DeviceReading> batch, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.WriteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Flushed {Count} readings to TimescaleDB", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush batch of {Count} readings, sending to DLQ", batch.Count);

            // Send to dead letter queue for retry
            _deadLetterQueue.AddFailedBatch(batch, ex);
        }
    }

    private async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down MQTT logger service");

        try
        {
            // Complete channel writing
            _readingChannel.Writer.Complete();

            // Stop MQTT client
            await _mqttClient.StopAsync().ConfigureAwait(false);

            // Log final statistics from health monitor
            var health = GetHealthStatus();
            _logger.LogInformation(
                "MQTT service stopped. Uptime: {Uptime}, Messages: {Received} received, {Processed} processed, {Failed} failed",
                health.Uptime, health.MessagesReceived, health.MessagesProcessed, health.MessagesFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MQTT service shutdown");
        }
    }

    /// <summary>
    /// Gets service health status with statistics.
    /// </summary>
    /// <returns>Health information.</returns>
    public MqttServiceHealth GetHealthStatus()
    {
        return _healthMonitor.GetHealthStatus(
            _mqttClient.IsConnected,
            _config.MqttDevices.Count(d => d.Enabled));
    }
}
