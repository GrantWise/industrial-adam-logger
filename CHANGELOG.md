# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] - 2025-10-04

### Added
- **MQTT Protocol Support** - General-purpose MQTT data logger
  - Support for any MQTT-enabled device (not just ADAM hardware)
  - JSON, Binary, and CSV payload formats
  - Flexible JsonPath extraction for custom JSON payloads
  - Topic wildcards (`+`, `#`) for flexible subscriptions
  - Auto-reconnect with configurable QoS levels (0/1/2)
  - Event-driven architecture with System.Threading.Channels batching
  - Reuses existing TimescaleDB + DeadLetterQueue infrastructure
  - `MqttLoggerService` background service
  - `MqttController` REST API endpoints (`/mqtt/health`, `/mqtt/devices`)
  - Comprehensive documentation (`docs/mqtt-guide.md`)
  - 30+ unit and integration tests
- **MQTT Configuration Models**
  - `MqttSettings` - Broker connection, authentication, TLS, QoS
  - `MqttDeviceConfig` - Device topics, payload format, data extraction
- **MQTT Core Components**
  - `IMqttClientWrapper` / `MqttClientWrapper` - Testable MQTT client abstraction
  - `MqttConnectionFactory` - Creates ManagedClient with auto-reconnect
  - `MqttMessageProcessor` - Parses JSON/Binary/CSV payloads
  - `TopicSubscriptionManager` - Topic-to-device mapping with wildcards
  - `MqttHealthMonitor` - Connection statistics and per-topic metrics

### Changed
- Version bump to 3.0.0 (major feature addition)
- Extended `LoggerConfiguration` with `Mqtt` and `MqttDevices` sections
- WebApi conditionally registers MQTT services (only if configured)
- Updated README.md with dual-protocol support documentation
- Updated CLAUDE.md with MQTT architecture and patterns

### Fixed
- **CRITICAL**: MQTT channel handling now optional with default to 0
  - Most MQTT devices don't have "channels" like Modbus hardware
  - JSON/Binary/CSV formats now support simple single-value payloads
  - Multi-channel devices can still explicitly include channel numbers

### Technical Details
- **Library**: MQTTnet 4.3.7.1207 (ManagedClient for auto-reconnect)
- **JsonPath**: Newtonsoft.Json for flexible JSON field extraction
- **Performance**: 1000+ messages/sec throughput
- **Reliability**: QoS 1 (at-least-once) default, DLQ for storage failures
- **Backpressure**: Bounded channel (10,000 message capacity)
- **Testing**: 145 total tests (142 Core + 3 Integration)

### Non-Breaking
- All existing Modbus functionality unchanged
- MQTT is optional (only runs if `Mqtt` section configured)
- No changes to TimescaleDB schema
- No changes to existing REST API endpoints
- Backward compatible configuration

### Documentation
- `docs/mqtt-guide.md` - Complete MQTT setup guide
  - Broker installation (Mosquitto, EMQX)
  - Configuration examples (JSON/CSV/Binary)
  - Topic patterns and wildcards
  - QoS level explanations
  - TLS/SSL security setup
  - Troubleshooting guide
  - Performance tuning
  - Integration examples (Python, Node.js, Arduino/ESP32)
  - Production deployment with Docker Compose

## [2.0.0] - 2025-10-02

### Added
- Initial release with Modbus TCP support
- ADAM-6000 series device support (digital counters + analog inputs)
  - Digital I/O: ADAM-6050, 6051, 6052, 6053, 6055, 6056
  - Analog Input: ADAM-6015, 6017, 6018, 6024
- TimescaleDB time-series storage with batching
- Dead Letter Queue for zero data loss
- Windowed rate calculation for production metrics
- Counter overflow detection (16-bit and 32-bit)
- Data quality indicators (Good, Uncertain, Bad, Unavailable)
- REST API with Swagger documentation
- Health monitoring and diagnostics
- Comprehensive E2E test suite
- Docker deployment support
- Device simulators for testing without hardware

---

## [Unreleased]

### Planned
- OPC UA protocol support (v4.0.0)
- Web dashboard for real-time monitoring
- Grafana integration guide
- Alerting system for device failures
