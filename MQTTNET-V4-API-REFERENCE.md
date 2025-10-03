# MQTTnet v4 API Reference

**Version**: 4.3.7.1207
**Date**: 2025-10-03
**Source**: Official MQTTnet GitHub samples

## Key Findings

### Packages
- `MQTTnet` version 4.3.7.1207 - Core library
- `MQTTnet.Extensions.ManagedClient` version 4.3.7.1207 - Auto-reconnect client

### API Changes from v3 to v4

1. **TLS Configuration**
   ```csharp
   // ❌ Obsolete in v4
   .WithTls()

   // ✅ Correct in v4
   .WithTlsOptions(o =>
   {
       o.WithCertificateValidationHandler(_ => true);
       o.WithSslProtocols(SslProtocols.Tls12);
   })
   ```

2. **Event Handler Signatures**
   ```csharp
   // Correct signature for v4
   mqttClient.ApplicationMessageReceivedAsync += e =>
   {
       Console.WriteLine($"Topic: {e.ApplicationMessage.Topic}");
       var payload = e.ApplicationMessage.PayloadSegment;
       return Task.CompletedTask;
   };

   mqttClient.ConnectedAsync += e =>
   {
       Console.WriteLine("Connected");
       return Task.CompletedTask;
   };

   mqttClient.DisconnectedAsync += e =>
   {
       Console.WriteLine($"Disconnected: {e.Reason}");
       return Task.CompletedTask;
   };
   ```

3. **Factory Pattern**
   ```csharp
   var mqttFactory = new MqttClientFactory();

   // For regular client
   using var mqttClient = mqttFactory.CreateMqttClient();

   // For managed client (with auto-reconnect)
   using var managedClient = mqttFactory.CreateManagedMqttClient();
   ```

## Complete Working Example (Regular Client)

```csharp
using MQTTnet;
using MQTTnet.Client;

public async Task ConnectExample()
{
    var mqttFactory = new MqttClientFactory();
    using var mqttClient = mqttFactory.CreateMqttClient();

    // Set up event handlers BEFORE connecting
    mqttClient.ApplicationMessageReceivedAsync += e =>
    {
        Console.WriteLine($"Received: {e.ApplicationMessage.Topic}");
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
        return Task.CompletedTask;
    };

    mqttClient.ConnectedAsync += e =>
    {
        Console.WriteLine("Connected successfully");
        return Task.CompletedTask;
    };

    mqttClient.DisconnectedAsync += e =>
    {
        Console.WriteLine($"Disconnected: {e.Reason}");
        return Task.CompletedTask;
    };

    // Build connection options
    var mqttClientOptions = new MqttClientOptionsBuilder()
        .WithTcpServer("broker.hivemq.com", 1883)
        .WithClientId("my-client-id")
        .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
        .Build();

    // Connect
    await mqttClient.ConnectAsync(mqttClientOptions);

    // Subscribe
    var subscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
        .WithTopicFilter(f => f.WithTopic("my/topic/#"))
        .Build();

    await mqttClient.SubscribeAsync(subscribeOptions);
}
```

## ManagedClient Pattern (For Auto-Reconnect)

Based on v4 API, the ManagedClient pattern likely follows:

```csharp
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

public async Task ManagedClientExample()
{
    var mqttFactory = new MqttClientFactory();
    using var managedClient = mqttFactory.CreateManagedMqttClient();

    // Set up event handlers
    managedClient.ApplicationMessageReceivedAsync += e =>
    {
        // Handle message
        return Task.CompletedTask;
    };

    managedClient.ConnectedAsync += e =>
    {
        // Handle connection
        return Task.CompletedTask;
    };

    managedClient.DisconnectedAsync += e =>
    {
        // Handle disconnection (will auto-reconnect)
        return Task.CompletedTask;
    };

    // Build client options
    var clientOptions = new MqttClientOptionsBuilder()
        .WithTcpServer("broker.hivemq.com", 1883)
        .WithClientId("my-managed-client")
        .Build();

    // Build managed options (includes auto-reconnect)
    var managedOptions = new ManagedMqttClientOptionsBuilder()
        .WithClientOptions(clientOptions)
        .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
        .Build();

    // Start (connects and maintains connection)
    await managedClient.StartAsync(managedOptions);

    // Subscribe
    var topicFilters = new[]
    {
        new MqttTopicFilterBuilder()
            .WithTopic("my/topic/#")
            .Build()
    };

    await managedClient.SubscribeAsync(topicFilters);
}
```

## Key Implementation Notes

1. **Event handlers must be set up BEFORE connecting**
2. **Use `Func<T, Task>` signature for events** (not `async void`)
3. **PayloadSegment is `ArraySegment<byte>`** - use `.ToArray()` or Span operations
4. **ManagedClient handles reconnection automatically**
5. **Use `MqttClientFactory` not `MqttFactory`** for client creation
6. **Statistics tracking**: Use `Interlocked` on `long` fields, not properties

## Production Recommendations

1. Use `ManagedMqttClient` for reliable production scenarios
2. Configure appropriate `WithAutoReconnectDelay`
3. Set up proper TLS with certificate validation in production
4. Use QoS 1 (AtLeastOnce) for reliability
5. Handle `DisconnectedAsync` for logging/metrics
6. Track connection statistics (messages received, bytes, reconnects)

---

**This reference is based on MQTTnet v4.3.7.1207 official samples**
**Source**: https://github.com/dotnet/MQTTnet/tree/master/Samples/Client
