using Industrial.Adam.Logger.Core.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace Industrial.Adam.Logger.Core.Mqtt;

/// <summary>
/// Factory for creating MQTT client connection options.
/// Handles broker configuration, authentication, TLS, and reconnection policies.
/// </summary>
public sealed class MqttConnectionFactory
{
    private readonly ILogger<MqttConnectionFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MqttConnectionFactory"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public MqttConnectionFactory(ILogger<MqttConnectionFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates managed MQTT client options from configuration.
    /// </summary>
    /// <param name="settings">MQTT broker and connection settings.</param>
    /// <returns>Configured managed client options ready for connection.</returns>
    public ManagedMqttClientOptions CreateManagedClientOptions(MqttSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Validate settings
        var validationResult = settings.Validate();
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors);
            throw new InvalidOperationException($"Invalid MQTT settings: {errors}");
        }

        _logger.LogInformation("Building MQTT client options for broker {BrokerHost}:{BrokerPort}",
            settings.BrokerHost, settings.BrokerPort);

        // Build base client options
        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(settings.ClientId)
            .WithTcpServer(settings.BrokerHost, settings.BrokerPort)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(settings.KeepAlivePeriodSeconds));

        // Add credentials if configured
        if (!string.IsNullOrEmpty(settings.Username))
        {
            _logger.LogDebug("Adding MQTT authentication for user {Username}", settings.Username);
            clientOptionsBuilder.WithCredentials(settings.Username, settings.Password);
        }

        // Add TLS if configured
        if (settings.UseTls)
        {
            _logger.LogInformation("Enabling TLS for MQTT connection");
            clientOptionsBuilder.WithTlsOptions(o =>
            {
                // TODO: Implement proper certificate validation in production
                // For now, accept all certificates for development/testing
                o.WithCertificateValidationHandler(_ => true);
                o.WithSslProtocols(
                    System.Security.Authentication.SslProtocols.Tls12 |
                    System.Security.Authentication.SslProtocols.Tls13);
            });

            if (settings.BrokerPort == 1883)
            {
                _logger.LogWarning("TLS enabled but using default non-TLS port 1883. Consider port 8883.");
            }
        }

        // Add clean session flag if configured
        if (settings.CleanSession.HasValue)
        {
            _logger.LogDebug("Setting CleanSession flag to {CleanSession}", settings.CleanSession.Value);
            clientOptionsBuilder.WithCleanSession(settings.CleanSession.Value);
        }

        var clientOptions = clientOptionsBuilder.Build();

        // Build managed client options (adds auto-reconnect)
        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(clientOptions)
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(settings.ReconnectDelaySeconds))
            .Build();

        _logger.LogInformation("MQTT client options built successfully");

        return managedOptions;
    }
}
