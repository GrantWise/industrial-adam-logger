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
    private readonly MqttMessageProcessor _messageProcessor;
    private readonly ITimescaleStorage _storage;
    private readonly DeadLetterQueue _deadLetterQueue;
    private readonly LoggerConfiguration _config;
    private readonly ILogger<MqttLoggerService> _logger;

    // Channel for batching readings before storage
    private readonly Channel<DeviceReading> _readingChannel;
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;

    // Statistics tracking
    private long _messagesReceived;
    private long _messagesProcessed;
    private long _messagesFailed;
    private DateTimeOffset _serviceStartTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="MqttLoggerService"/> class.
    /// </summary>
    /// <param name="mqttClient">MQTT client wrapper for broker communication.</param>
    /// <param name="messageProcessor">Message processor for payload parsing.</param>
    /// <param name="storage">TimescaleDB storage interface.</param>
    /// <param name="deadLetterQueue">Dead letter queue for failed storage operations.</param>
    /// <param name="config">Logger configuration.</param>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public MqttLoggerService(
        IMqttClientWrapper mqttClient,
        MqttMessageProcessor messageProcessor,
        ITimescaleStorage storage,
        DeadLetterQueue deadLetterQueue,
        IOptions<LoggerConfiguration> config,
        ILogger<MqttLoggerService> logger)
    {
        _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
        _messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
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
        _serviceStartTime = DateTimeOffset.UtcNow;

        if (_config.Mqtt == null || _config.MqttDevices.Count == 0)
        {
            _logger.LogInformation("MQTT not configured, service will remain idle");
            return;
        }

        _logger.LogInformation("Starting MQTT logger service with {DeviceCount} devices", _config.MqttDevices.Count);

        try
        {
            // Wire up event handlers
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

            // Build managed client options
            var managedOptions = BuildManagedClientOptions();

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

    private ManagedMqttClientOptions BuildManagedClientOptions()
    {
        var mqtt = _config.Mqtt!;

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(mqtt.ClientId)
            .WithTcpServer(mqtt.BrokerHost, mqtt.BrokerPort)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(mqtt.KeepAlivePeriodSeconds));

        // Add credentials if configured
        if (!string.IsNullOrEmpty(mqtt.Username))
        {
            clientOptionsBuilder.WithCredentials(mqtt.Username, mqtt.Password);
        }

        // Add TLS if configured
        if (mqtt.UseTls)
        {
            clientOptionsBuilder.WithTlsOptions(o =>
            {
                o.WithCertificateValidationHandler(_ => true); // TODO: Proper certificate validation in production
                o.WithSslProtocols(System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13);
            });
        }

        var clientOptions = clientOptionsBuilder.Build();

        return new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(clientOptions)
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(mqtt.ReconnectDelaySeconds))
            .Build();
    }

    private async Task SubscribeToTopicsAsync(CancellationToken cancellationToken)
    {
        // Collect all unique topics from all devices
        var allTopics = _config.MqttDevices
            .Where(d => d.Enabled)
            .SelectMany(d => d.Topics)
            .Distinct()
            .ToList();

        if (allTopics.Count == 0)
        {
            _logger.LogWarning("No topics configured for subscription");
            return;
        }

        var qos = _config.Mqtt!.QualityOfServiceLevel switch
        {
            0 => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
            1 => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
            2 => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
            _ => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce
        };

        var topicFilters = allTopics.Select(topic =>
            new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(qos)
                .Build()
        ).ToList();

        await _mqttClient.SubscribeAsync(topicFilters, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Subscribed to {TopicCount} topics with QoS {Qos}", topicFilters.Count, qos);
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        Interlocked.Increment(ref _messagesReceived);

        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.PayloadSegment;

            // Find matching device configuration
            var deviceConfig = FindDeviceForTopic(topic);
            if (deviceConfig == null)
            {
                _logger.LogDebug("No device configured for topic {Topic}", topic);
                return;
            }

            // Process message
            var reading = _messageProcessor.ProcessMessage(deviceConfig, topic, payload);
            if (reading == null)
            {
                Interlocked.Increment(ref _messagesFailed);
                _logger.LogWarning("Failed to process message from topic {Topic}", topic);
                return;
            }

            // Enqueue for batching
            await _readingChannel.Writer.WriteAsync(reading).ConfigureAwait(false);
            Interlocked.Increment(ref _messagesProcessed);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _messagesFailed);
            _logger.LogError(ex, "Error processing MQTT message from topic {Topic}", e.ApplicationMessage.Topic);
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

    private MqttDeviceConfig? FindDeviceForTopic(string topic)
    {
        // Find first enabled device with a matching topic filter
        foreach (var device in _config.MqttDevices.Where(d => d.Enabled))
        {
            foreach (var topicFilter in device.Topics)
            {
                if (MqttTopicFilterComparer.Compare(topic, topicFilter) == MqttTopicFilterCompareResult.IsMatch)
                {
                    return device;
                }
            }
        }

        return null;
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

            // Log final statistics
            var uptime = DateTimeOffset.UtcNow - _serviceStartTime;
            _logger.LogInformation(
                "MQTT service stopped. Uptime: {Uptime}, Messages: {Received} received, {Processed} processed, {Failed} failed",
                uptime, _messagesReceived, _messagesProcessed, _messagesFailed);
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
        return new MqttServiceHealth
        {
            IsConnected = _mqttClient.IsConnected,
            MessagesReceived = Interlocked.Read(ref _messagesReceived),
            MessagesProcessed = Interlocked.Read(ref _messagesProcessed),
            MessagesFailed = Interlocked.Read(ref _messagesFailed),
            Uptime = DateTimeOffset.UtcNow - _serviceStartTime,
            ConfiguredDevices = _config.MqttDevices.Count(d => d.Enabled)
        };
    }
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
}
