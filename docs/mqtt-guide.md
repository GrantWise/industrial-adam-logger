# MQTT Data Logger Guide

## Overview

The Industrial ADAM Logger supports logging time-series data from **any MQTT-enabled device** via a standard MQTT broker. This provides a flexible alternative to direct Modbus TCP polling for devices that publish data via MQTT.

### Architecture

```
[MQTT Devices] → [MQTT Broker] → [Logger Service] → [TimescaleDB]
     │              │                   │                  │
  Publish      Mosquitto/         Subscribes &        Time-series
  on topics      EMQX             Processes            Storage
```

**Key Features:**
- Zero data loss (Dead Letter Queue)
- Auto-reconnect with configurable delay
- Topic wildcards (`+` and `#`)
- Multiple payload formats (JSON, Binary, CSV)
- Per-device QoS configuration
- Batched writes to TimescaleDB

## Quick Start

### 1. Install MQTT Broker

**Mosquitto (Lightweight):**
```bash
# Ubuntu/Debian
sudo apt-get install mosquitto mosquitto-clients

# Start broker
sudo systemctl start mosquitto
sudo systemctl enable mosquitto

# Test
mosquitto_pub -h localhost -t "test/topic" -m '{"value":123}'
```

**EMQX (Enterprise):**
```bash
docker run -d --name emqx \
  -p 1883:1883 \
  -p 18083:18083 \
  emqx/emqx:latest

# Web UI: http://localhost:18083 (admin/public)
```

### 2. Configure Logger

Add MQTT configuration to `appsettings.json`:

```json
{
  "AdamLogger": {
    "Mqtt": {
      "BrokerHost": "localhost",
      "BrokerPort": 1883,
      "ClientId": "industrial-logger",
      "QualityOfServiceLevel": 1,
      "ReconnectDelaySeconds": 5
    },
    "MqttDevices": [
      {
        "DeviceId": "sensor-01",
        "Name": "Temperature Sensor",
        "Enabled": true,
        "Topics": ["sensors/temperature"],
        "Format": "Json",
        "DataType": "Float32",
        "ChannelJsonPath": "$.channel",
        "ValueJsonPath": "$.temperature",
        "Unit": "°C",
        "ScaleFactor": 1.0
      }
    ]
  }
}
```

### 3. Run Logger

```bash
cd src/Industrial.Adam.Logger.WebApi
dotnet run
```

### 4. Test Data Flow

```bash
# Publish test message
mosquitto_pub -h localhost -t "sensors/temperature" \
  -m '{"channel":0,"temperature":25.5}'

# Check API
curl http://localhost:5000/mqtt/health
curl http://localhost:5000/data/latest
```

## Configuration Reference

### MQTT Broker Settings

```json
{
  "Mqtt": {
    "BrokerHost": "localhost",          // Required
    "BrokerPort": 1883,                 // Default: 1883 (TCP), 8883 (TLS)
    "ClientId": "industrial-logger",    // Unique client ID
    "Username": "mqtt-user",            // Optional
    "Password": "mqtt-password",        // Optional
    "UseTls": false,                    // Enable TLS/SSL
    "QualityOfServiceLevel": 1,         // 0=AtMostOnce, 1=AtLeastOnce, 2=ExactlyOnce
    "KeepAlivePeriodSeconds": 60,       // Heartbeat interval
    "ReconnectDelaySeconds": 5,         // Delay before reconnect
    "MaxReconnectAttempts": 0,          // 0=infinite
    "CleanSession": null,               // null=default, true=no persistence, false=persistent
    "MaxTrackedTopics": 1000,           // Memory limit for topic statistics
    "MaxJsonPayloadBytes": 1048576      // Max JSON payload size (1MB default)
  }
}
```

### Device Configuration

```json
{
  "DeviceId": "sensor-01",              // Required: Unique device ID
  "Name": "Temperature Sensor",         // Optional: Human-readable name
  "ModelType": "DHT22",                 // Optional: Device model
  "Enabled": true,                      // Required: Enable/disable device
  "Topics": [                           // Required: MQTT topics to subscribe
    "sensors/temperature",
    "sensors/+/temperature",            // Single-level wildcard
    "sensors/#"                         // Multi-level wildcard
  ],
  "Format": "Json",                     // Required: Json, Binary, or Csv
  "DataType": "Float32",                // Required: UInt32, Int16, UInt16, Float32, Float64
  "QosLevel": 1,                        // Optional: Override global QoS
  "ScaleFactor": 1.0,                   // Optional: Multiply raw value
  "Unit": "°C",                         // Optional: Unit for display

  // JSON-specific paths (JsonPath syntax)
  "DeviceIdJsonPath": "$.device_id",    // Optional: Extract device ID from payload
  "ChannelJsonPath": "$.channel",       // Required for JSON: Channel number
  "ValueJsonPath": "$.value",           // Required for JSON: Numeric value
  "TimestampJsonPath": "$.timestamp"    // Optional: ISO 8601 timestamp
}
```

## Payload Formats

### JSON (Most Flexible)

**Example Message:**
```json
{
  "device_id": "sensor-01",
  "channel": 0,
  "value": 123.45,
  "timestamp": "2025-10-03T10:00:00Z"
}
```

**Configuration:**
```json
{
  "Format": "Json",
  "DeviceIdJsonPath": "$.device_id",
  "ChannelJsonPath": "$.channel",
  "ValueJsonPath": "$.value",
  "TimestampJsonPath": "$.timestamp"
}
```

**Nested JSON:**
```json
{
  "sensor": {
    "id": "sensor-01",
    "readings": {
      "temperature": 25.5,
      "channel": 0
    }
  }
}
```

Configuration: `"ValueJsonPath": "$.sensor.readings.temperature"`

### Binary (Most Compact)

**Format:** `[1 byte channel][N bytes value]`

**Example (UInt32):**
```
Bytes: [0x00, 0x39, 0x30, 0x00, 0x00]
       └─┬─┘  └──────┬──────────┘
      Channel    Value (12345)
```

**Configuration:**
```json
{
  "Format": "Binary",
  "DataType": "UInt32"
}
```

**Data Type Sizes:**
- `Int16` / `UInt16`: 1 channel byte + 2 value bytes = 3 bytes
- `UInt32`: 1 channel byte + 4 value bytes = 5 bytes
- `Float32`: 1 channel byte + 4 value bytes = 5 bytes
- `Float64`: 1 channel byte + 8 value bytes = 9 bytes

### CSV (Simple Text)

**Example:** `channel,value,timestamp`
```
0,123.45,2025-10-03T10:00:00Z
```

**Configuration:**
```json
{
  "Format": "Csv"
}
```

**Note:** CSV assumes order: channel, value, timestamp (optional)

## Topic Wildcards

MQTT supports hierarchical topics with wildcards:

### Single-Level Wildcard (`+`)

Matches **one** topic level:

```
sensors/+/temperature
```

**Matches:**
- `sensors/room1/temperature` ✓
- `sensors/room2/temperature` ✓

**Does NOT match:**
- `sensors/room1/humidity` ✗
- `sensors/building1/room1/temperature` ✗

### Multi-Level Wildcard (`#`)

Matches **any number** of topic levels (must be last):

```
sensors/#
```

**Matches:**
- `sensors/temperature` ✓
- `sensors/room1/temperature` ✓
- `sensors/building1/room1/temperature` ✓

## Quality of Service (QoS)

MQTT supports three QoS levels:

| QoS | Name | Delivery Guarantee | Use Case |
|-----|------|-------------------|----------|
| 0 | At Most Once | Fire and forget | Non-critical data |
| 1 | At Least Once | Acknowledged | **Default - Industrial use** |
| 2 | Exactly Once | Guaranteed once | Financial/critical data |

**Recommendation:** Use QoS 1 for industrial logging (balances reliability and performance).

**Per-Device QoS:**
```json
{
  "MqttDevices": [
    {
      "DeviceId": "critical-sensor",
      "QosLevel": 2,              // Override global QoS
      "Topics": ["critical/data"]
    }
  ]
}
```

## TLS/SSL Security

### Production Setup

```json
{
  "Mqtt": {
    "BrokerHost": "mqtt.example.com",
    "BrokerPort": 8883,
    "UseTls": true,
    "Username": "logger",
    "Password": "secure-password",
    "AllowInvalidCertificates": false  // NEVER set true in production
  }
}
```

### Development Setup

```json
{
  "Mqtt": {
    "BrokerHost": "localhost",
    "BrokerPort": 1883,
    "UseTls": false
  }
}
```

**Security Notes:**
- Always use TLS in production
- Never set `AllowInvalidCertificates: true` in production (validation will fail)
- Use strong passwords (minimum 16 characters)
- Rotate credentials regularly

## Monitoring & Health

### API Endpoints

```bash
# MQTT service health
GET /mqtt/health

Response:
{
  "isConnected": true,
  "messagesReceived": 1234,
  "messagesProcessed": 1230,
  "messagesFailed": 4,
  "uptime": "2.03:45:12",
  "configuredDevices": 5,
  "topicStatistics": {
    "sensors/temperature": {
      "messagesReceived": 500,
      "messagesProcessed": 498,
      "messagesFailed": 2,
      "lastMessageTime": "2025-10-03T10:15:30Z"
    }
  }
}

# Configured devices
GET /mqtt/devices

# Latest data
GET /data/latest
```

### Logs

```bash
# View MQTT logs
docker-compose logs -f adam-logger | grep MQTT

# Common log messages
[Information] MQTT client connected to broker
[Information] Subscribed to 3 topics
[Debug] Processing JSON payload: {"channel":0,"value":123}
[Warning] Failed to process message from topic sensors/error
[Error] MQTT client disconnected: Connection lost. Will auto-reconnect
```

## Troubleshooting

### Connection Issues

**Problem:** Logger can't connect to broker

```
[Error] MQTT client disconnected: Connection refused
```

**Solution:**
1. Check broker is running: `systemctl status mosquitto`
2. Verify host/port: `telnet localhost 1883`
3. Check firewall: `sudo ufw allow 1883/tcp`
4. Review broker logs: `sudo journalctl -u mosquitto -f`

### No Data Received

**Problem:** Broker connected but no data

```
[Information] MQTT client connected to broker
[Information] Subscribed to 3 topics
# ... no message logs
```

**Solution:**
1. Test topic subscription manually:
   ```bash
   mosquitto_sub -h localhost -t "sensors/#" -v
   ```
2. Publish test message:
   ```bash
   mosquitto_pub -h localhost -t "sensors/test" -m '{"channel":0,"value":123}'
   ```
3. Check topic matching (wildcards)
4. Verify device is `Enabled: true`

### Message Processing Failures

**Problem:** Messages received but not processed

```
[Warning] Failed to process message from topic sensors/temp
[Warning] Could not extract value from JSON using path $.value
```

**Solution:**
1. Check JSON payload structure matches configured paths
2. Verify `ChannelJsonPath` and `ValueJsonPath` are correct
3. Test JSON path: Use online JsonPath evaluator
4. Check data type matches payload (Float32 vs Int16)

### Memory Issues

**Problem:** High memory usage

```
[Warning] Maximum tracked topics limit (1000) reached
```

**Solution:**
1. Increase `MaxTrackedTopics` in configuration
2. Use more specific topic filters (avoid broad wildcards)
3. Review topic statistics and remove unused devices

## Best Practices

### Topic Design

**Good:**
```
factory/line1/machine1/temperature
factory/line1/machine1/pressure
factory/line1/machine2/temperature
```

**Bad:**
```
data              # Too generic
sensor_123_temp   # Hard to filter
```

**Recommendation:**
- Hierarchical: `location/area/device/metric`
- Use consistent naming
- Avoid special characters except `/`, `_`, `-`

### Performance Tuning

**High-frequency data (>100 msg/sec):**
```json
{
  "TimescaleDb": {
    "BatchSize": 500,           // Larger batches
    "BatchTimeoutMs": 1000      // Shorter timeout
  },
  "Mqtt": {
    "QualityOfServiceLevel": 0  // QoS 0 for max throughput
  }
}
```

**Critical data (reliability > speed):**
```json
{
  "TimescaleDb": {
    "BatchSize": 50,
    "BatchTimeoutMs": 5000
  },
  "Mqtt": {
    "QualityOfServiceLevel": 2  // Exactly once
  }
}
```

### Data Quality

Always include data quality indicators:
- Good: Real-time data from device
- Uncertain: Interpolated or estimated values
- Bad: Sensor malfunction or out of range
- Unavailable: No data available

**Do NOT:**
- Generate synthetic data without marking it
- Display fallback values as real measurements
- Hide data quality from users

## Integration Examples

### Python Publisher

```python
import paho.mqtt.client as mqtt
import json
import time

client = mqtt.Client()
client.connect("localhost", 1883, 60)

while True:
    payload = {
        "channel": 0,
        "value": 25.5,
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    }
    client.publish("sensors/temperature", json.dumps(payload))
    time.sleep(1)
```

### Node.js Publisher

```javascript
const mqtt = require('mqtt');
const client = mqtt.connect('mqtt://localhost:1883');

client.on('connect', () => {
  setInterval(() => {
    const payload = {
      channel: 0,
      value: 25.5,
      timestamp: new Date().toISOString()
    };
    client.publish('sensors/temperature', JSON.stringify(payload));
  }, 1000);
});
```

### Arduino/ESP32

```cpp
#include <WiFi.h>
#include <PubSubClient.h>

WiFiClient espClient;
PubSubClient client(espClient);

void setup() {
  WiFi.begin("SSID", "password");
  client.setServer("mqtt.example.com", 1883);
}

void loop() {
  if (!client.connected()) {
    client.connect("esp32-sensor");
  }

  String payload = "{\"channel\":0,\"value\":25.5}";
  client.publish("sensors/temperature", payload.c_str());

  delay(1000);
}
```

## Production Deployment

### Docker Compose

Complete stack with MQTT broker:

```yaml
version: '3.8'
services:
  mosquitto:
    image: eclipse-mosquitto:latest
    ports:
      - "1883:1883"
      - "9001:9001"
    volumes:
      - ./mosquitto.conf:/mosquitto/config/mosquitto.conf
      - mosquitto-data:/mosquitto/data
      - mosquitto-logs:/mosquitto/log

  timescaledb:
    image: timescale/timescaledb:latest-pg15
    # ... existing config

  adam-logger:
    build: .
    depends_on:
      - timescaledb
      - mosquitto
    environment:
      - AdamLogger__Mqtt__BrokerHost=mosquitto
    # ... existing config

volumes:
  mosquitto-data:
  mosquitto-logs:
```

### Health Checks

```yaml
adam-logger:
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:5000/mqtt/health"]
    interval: 30s
    timeout: 10s
    retries: 3
```

## Support

- **Documentation:** `/docs` directory
- **API Reference:** `http://localhost:5000/swagger`
- **GitHub Issues:** Report bugs or request features
- **Architecture:** See `docs/architecture_guide.md`

## See Also

- [Architecture Guide](architecture_guide.md) - System design and patterns
- [Development Standards](development_standards.md) - Coding guidelines
- [Getting Started](../README.md) - Project overview
