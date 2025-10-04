using FluentAssertions;
using Industrial.Adam.Logger.Core.Configuration;
using Industrial.Adam.Logger.Core.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Industrial.Adam.Logger.Core.Tests.Mqtt;

public class MqttConnectionFactoryTests
{
    private readonly MqttConnectionFactory _factory;

    public MqttConnectionFactoryTests()
    {
        _factory = new MqttConnectionFactory(NullLogger<MqttConnectionFactory>.Instance);
    }

    [Fact]
    public void CreateManagedClientOptions_ValidSettings_ReturnsOptions()
    {
        // Arrange
        var settings = new MqttSettings
        {
            BrokerHost = "localhost",
            BrokerPort = 1883,
            ClientId = "test-client",
            KeepAlivePeriodSeconds = 60,
            ReconnectDelaySeconds = 5
        };

        // Act
        var options = _factory.CreateManagedClientOptions(settings);

        // Assert
        options.Should().NotBeNull();
        options.ClientOptions.ClientId.Should().Be("test-client");
        options.AutoReconnectDelay.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateManagedClientOptions_NullSettings_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _factory.CreateManagedClientOptions(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateManagedClientOptions_InvalidSettings_ThrowsInvalidOperationException()
    {
        // Arrange
        var settings = new MqttSettings
        {
            BrokerHost = "", // Invalid - empty host
            ClientId = "test-client"
        };

        // Act
        var act = () => _factory.CreateManagedClientOptions(settings);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid MQTT settings*");
    }

    [Fact]
    public void CreateManagedClientOptions_WithCredentials_AddsAuthentication()
    {
        // Arrange
        var settings = new MqttSettings
        {
            BrokerHost = "localhost",
            BrokerPort = 1883,
            ClientId = "test-client",
            Username = "testuser",
            Password = "testpass"
        };

        // Act
        var options = _factory.CreateManagedClientOptions(settings);

        // Assert
        options.Should().NotBeNull();
        options.ClientOptions.Credentials.Should().NotBeNull();
    }

    [Fact]
    public void CreateManagedClientOptions_WithTls_EnablesTls()
    {
        // Arrange
        var settings = new MqttSettings
        {
            BrokerHost = "localhost",
            BrokerPort = 8883,
            ClientId = "test-client",
            UseTls = true
        };

        // Act
        var options = _factory.CreateManagedClientOptions(settings);

        // Assert
        options.Should().NotBeNull();
        options.ClientOptions.ChannelOptions.Should().NotBeNull();
    }

    [Fact]
    public void CreateManagedClientOptions_WithCleanSession_SetsFlag()
    {
        // Arrange
        var settings = new MqttSettings
        {
            BrokerHost = "localhost",
            BrokerPort = 1883,
            ClientId = "test-client",
            CleanSession = false
        };

        // Act
        var options = _factory.CreateManagedClientOptions(settings);

        // Assert
        options.Should().NotBeNull();
        options.ClientOptions.CleanSession.Should().BeFalse();
    }

    [Fact]
    public void CreateManagedClientOptions_WithNullCleanSession_UsesDefault()
    {
        // Arrange
        var settings = new MqttSettings
        {
            BrokerHost = "localhost",
            BrokerPort = 1883,
            ClientId = "test-client",
            CleanSession = null
        };

        // Act
        var options = _factory.CreateManagedClientOptions(settings);

        // Assert
        options.Should().NotBeNull();
        // Default behavior from MQTTnet
    }

    [Fact]
    public void CreateManagedClientOptions_ValidatesQoSRange()
    {
        // Arrange - QoS is validated in MqttSettings.Validate()
        var settingsInvalid = new MqttSettings
        {
            BrokerHost = "localhost",
            ClientId = "test-client",
            QualityOfServiceLevel = 3 // Invalid
        };

        // Act
        var act = () => _factory.CreateManagedClientOptions(settingsInvalid);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*QoS must be 0, 1, or 2*");
    }

    [Fact]
    public void CreateManagedClientOptions_KeepAliveAndReconnect_ConfiguredCorrectly()
    {
        // Arrange
        var settings = new MqttSettings
        {
            BrokerHost = "localhost",
            BrokerPort = 1883,
            ClientId = "test-client",
            KeepAlivePeriodSeconds = 120,
            ReconnectDelaySeconds = 10
        };

        // Act
        var options = _factory.CreateManagedClientOptions(settings);

        // Assert
        options.ClientOptions.KeepAlivePeriod.Should().Be(TimeSpan.FromSeconds(120));
        options.AutoReconnectDelay.Should().Be(TimeSpan.FromSeconds(10));
    }
}
