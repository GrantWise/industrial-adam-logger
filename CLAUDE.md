# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Industrial ADAM Logger** - Industrial-grade data acquisition service that reads data from ADAM-6000 series modules (digital counters and analog inputs) via Modbus TCP **and any MQTT-enabled device** via MQTT broker, persisting time-series data to TimescaleDB with zero data loss guarantees.

**Key Purpose:** Dual-protocol (Modbus TCP + MQTT) → TimescaleDB pipeline with dead letter queue reliability for 24/7 manufacturing operations.

**Supported Protocols:**
- **Modbus TCP**: Direct polling of ADAM-6000 hardware
- **MQTT**: Event-driven pub/sub from any MQTT device (JSON, Binary, CSV payloads)

**Supported Devices:**
- Digital I/O: ADAM-6050, 6051, 6052, 6053, 6055, 6056
- Analog Input: ADAM-6015, 6017, 6018, 6024
- MQTT: Any device publishing to MQTT broker

## 1. Core Philosophy: Pragmatic Over Dogmatic

The primary goal is to write clean, maintainable, and understandable code. Guidelines serve this goal, not the other way around.

- **Prioritize Readability**: Choose the approach that is easiest for a human to understand.
- **Value Logical Cohesion**: Keep related functionality together. A 300-line class that tells a coherent story is better than three fragmented 100-line classes that scatter related logic.
- **Focus on Developer Experience**: Optimize for the next developer who will work on the code.

Before making a change, ask yourself:
1.  Does this make the code easier to understand?
2.  Would I want to maintain this code in six months?
3.  Can a new team member grasp this quickly?

## 2. Architectural & Development Standards

Refer to the official documentation for detailed standards. Your work must adhere to these principles.

- **Architecture**: See `docs/architecture_guide.md` for details on our Clean Architecture, CQRS, and DDD patterns.
- **Development**: See `docs/development_standards.md` for unified backend and frontend standards.
- **Technical Stack**: See `docs/technical_specification.md` for the list of approved technologies.
- **Git Workflow**: See `GITHUB-WORKFLOW.md` for the definitive GitHub workflow, branching strategy, and commit conventions.

## 3. Debugging & Test Fixing Process

When fixing failing tests or debugging bugs, you **MUST** follow this process:

1.  **Identify the centralized pattern** causing multiple failures. Do not patch failures individually.
2.  **Find the root architectural violation** (e.g., SRP, SoC, DRY).
3.  **Create or fix a centralized utility/service** to resolve the root cause.
4.  **Apply the centralized fix** across all affected code.
5.  **Update test expectations** to match the correct, centralized behavior.

### Anti-Patterns to Avoid

- Fixing test failures one by one.
- Duplicating fixes across multiple files.
- Mixing unrelated concerns in a single component or class.
- Hardcoding values that should come from configuration.
- Implementing inconsistent error handling.

## 4. Code Quality Quick Reference

- **Function Size**: Aim for 20-40 lines, but prioritize readability. A 60-line function with a clear, single purpose is acceptable.
- **Class/Component Size**: Aim for ~200 lines, but prioritize logical cohesion. Do not fragment tightly coupled logic just to meet a size guideline.
- **Comments**: Comment on the *why*, not the *what*. Avoid obvious comments.
- **Error Handling**: Use specific exception types and structured logging. Return meaningful error messages.
- **Testing**: Follow the Arrange, Act, Assert pattern. Ensure tests are independent and have descriptive names.

## 5. Data Integrity and 21 CFR Part 11 Compliance

**CRITICAL REQUIREMENT**: Industrial systems must maintain absolute data integrity and transparency.

### Data Display Requirements

- **NEVER display interpolated, calculated, or fallback values without explicit user notification**
- **ALWAYS clearly indicate when data is unavailable, estimated, or synthetic**
- **NEVER present simulated data as real measurements**
- **ALWAYS provide data quality indicators (Good, Uncertain, Bad, Unavailable)**

### Implementation Standards

- **ZERO TOLERANCE for synthetic data**: When real data is unavailable, display "Data Not Available" instead of generating fallback values
- Use clear visual indicators for data status:
  - ✅ **Real Data**: No special marking required
  - ⚠️ **Estimated/Interpolated**: Yellow warning with "ESTIMATED" label
  - ❌ **Unavailable**: Red indicator with "NO DATA" or "OFFLINE" label
  - 🔧 **Simulated**: Clear "SIMULATED" or "TEST DATA" marking (for test environments only)
- Include timestamps for all measurements
- Log all data quality decisions for audit trails
- Provide detailed tooltips explaining data status
- **CRITICAL**: Never use Math.random(), fixed patterns, or interpolation to fill missing data

### Code Examples

```typescript
// CORRECT - Clear indication of data status
return {
  value: realValue,
  quality: 'good',
  timestamp: new Date(),
  isRealData: true
}

// CORRECT - Unavailable data handling
return {
  value: null,
  quality: 'bad',
  timestamp: new Date(),
  error: 'Device offline',
  isRealData: false
}

// INCORRECT - Synthetic data without indication
return {
  value: 42.5, // This is fake!
  quality: 'good' // This is misleading!
}
```

This principle applies to ALL industrial data: device readings, OEE calculations, system metrics, and dashboard displays.

## 6. Common Commands

### Build & Test
```bash
# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test src/Industrial.Adam.Logger.Core.Tests

# Run specific test by name
dotnet test --filter "FullyQualifiedName~DataProcessor"

# Build in Release mode
dotnet build -c Release
```

### Running the Application

```bash
# Run WebApi (REST API + Swagger)
dotnet run --project src/Industrial.Adam.Logger.WebApi

# Run Console host
dotnet run --project src/Industrial.Adam.Logger.Console

# Run with specific environment
dotnet run --project src/Industrial.Adam.Logger.WebApi --environment Production
```

### Docker & Infrastructure

```bash
# Start TimescaleDB only
cd docker
docker-compose up -d timescaledb

# Start database + MQTT broker
docker-compose up -d timescaledb mosquitto

# Start everything (logger + database + MQTT)
docker-compose up -d

# Start with simulators for testing
docker-compose -f docker-compose.simulator.yml up -d

# View logs
docker-compose logs -f adam-logger

# Stop all services
docker-compose down
```

### MQTT Testing

```bash
# Install mosquitto clients
sudo apt-get install mosquitto-clients

# Subscribe to topics (monitoring)
mosquitto_sub -h localhost -t "sensors/#" -v

# Publish test message (JSON)
mosquitto_pub -h localhost -t "sensors/temperature" \
  -m '{"channel":0,"value":25.5}'

# Test MQTT health endpoint
curl http://localhost:5000/mqtt/health
```

### Simulators (No Hardware Required)

```bash
# Start ADAM-6051 simulators
./scripts/start-simulators.sh

# Stop simulators
./scripts/stop-simulators.sh

# Simulators listen on ports 5502, 5503, 5504
```

### Benchmarks

```bash
# Run performance benchmarks
dotnet run --project src/Industrial.Adam.Logger.Benchmarks -c Release
```

## 7. Architecture

### Clean Architecture Layers

**Domain Layer** (`Industrial.Adam.Logger.Core/Models/`)
- `DeviceReading.cs` - Counter reading with timestamp and quality
- `DeviceHealth.cs` - Device health status and diagnostics
- `DataQuality.cs` - Enum for data quality (Good, Uncertain, Bad, Unavailable)

**Application Layer** (`Industrial.Adam.Logger.Core/Processing/`)
- `DataProcessor.cs` - Processes raw counter readings, handles overflow detection
- `WindowedRateCalculator.cs` - Calculates production rate using sliding window
- `CircularBuffer.cs` - Fixed-size buffer for windowed calculations

**Infrastructure Layer** (`Industrial.Adam.Logger.Core/Storage/`, `Services/`, `Mqtt/`)
- `TimescaleStorage.cs` - TimescaleDB persistence with batching
- `DeadLetterQueue.cs` - Ensures zero data loss on storage failures
- `AdamLoggerService.cs` - Main background service orchestrating Modbus polling
- `MqttDataLoggerService.cs` - MQTT subscriber service with auto-reconnect
- `MqttMessageProcessor.cs` - Processes MQTT payloads (JSON/Binary/CSV)
- `TopicSubscriptionManager.cs` - Manages topic-to-device mapping with wildcards

**Presentation Layer** (`Industrial.Adam.Logger.WebApi/`)
- REST API endpoints for device status, health, and data queries
- Swagger/OpenAPI documentation at `/swagger`
- JWT authentication with CORS support

### Key Design Patterns

- **Dual Background Services**: `AdamLoggerService` (Modbus polling) + `MqttDataLoggerService` (MQTT pub/sub)
- **Dead Letter Queue**: Failed database writes go to DLQ for retry (both protocols)
- **Batching**: Readings batched before database insert (configurable batch size/timeout)
- **Sliding Window**: Rate calculation uses circular buffer for smooth metrics
- **Overflow Detection**: Handles 16-bit and 32-bit counter wraparounds
- **Topic-Device Mapping**: Wildcard pattern matching (`+`, `#`) for flexible topic subscriptions
- **Auto-Reconnect**: MQTT client automatically reconnects with configurable delay

### Configuration Structure

All configuration in `appsettings.json`:

```json
{
  "AdamLogger": {
    "Devices": [/* Modbus device configs */],
    "Mqtt": {/* MQTT broker settings */},
    "MqttDevices": [/* MQTT device configs */],
    "TimescaleDb": {/* DB config */}
  },
  "Jwt": {/* JWT settings */},
  "Cors": {/* CORS origins */}
}
```

**Modbus Device Configuration:**
- `DeviceId`, `IpAddress`, `Port` - Modbus connection
- `PollIntervalMs` - How often to poll (default 1000ms)
- `Channels[]` - Counter channels to read
- `StartRegister`, `RegisterCount` - Modbus register mapping

**MQTT Configuration:**
- `BrokerHost`, `BrokerPort` - MQTT broker connection
- `ClientId`, `Username`, `Password` - Authentication
- `QualityOfServiceLevel` - Global QoS (0/1/2)
- `UseTls`, `AllowInvalidCertificates` - Security settings
- `ReconnectDelaySeconds` - Auto-reconnect delay

**MQTT Device Configuration:**
- `DeviceId`, `Topics[]` - Device ID and MQTT topics (supports `+` and `#` wildcards)
- `Format` - Payload format (Json, Binary, Csv)
- `DataType` - Data type (UInt32, Int16, UInt16, Float32, Float64)
- `ChannelJsonPath`, `ValueJsonPath` - JsonPath for field extraction (JSON format)
- `ScaleFactor`, `Unit` - Value transformation and display

**See `docs/mqtt-guide.md` for complete MQTT setup and configuration details.**

## 8. Testing Approach

### Test Organization

- **Unit Tests**: `Industrial.Adam.Logger.Core.Tests/`
- **Integration Tests**: `Industrial.Adam.Logger.IntegrationTests/` (require TimescaleDB)
- **Benchmarks**: `Industrial.Adam.Logger.Benchmarks/`

### Running Tests

```bash
# All tests (some integration tests may fail without Docker)
dotnet test

# Unit tests only (no database required)
dotnet test src/Industrial.Adam.Logger.Core.Tests

# Integration tests (requires TimescaleDB running)
dotnet test src/Industrial.Adam.Logger.IntegrationTests
```

**Note**: Integration tests require TimescaleDB running on localhost:5432. Start with `docker-compose up -d timescaledb` first.

### Test Pattern: Arrange-Act-Assert

```csharp
[Fact]
public void DataProcessor_Should_DetectOverflow()
{
    // Arrange
    var processor = new DataProcessor();

    // Act
    var result = processor.Process(reading);

    // Assert
    result.Should().NotBeNull();
    result.OverflowDetected.Should().BeTrue();
}
```

## 9. Technology Stack

- **.NET 9** with C# 13
- **TimescaleDB** (PostgreSQL + time-series)
- **NModbus** - Modbus TCP client
- **MQTTnet 4.3.7.1207** - MQTT v4 client library
- **Newtonsoft.Json.JsonPath** - JsonPath parsing for MQTT payloads
- **Polly** - Retry policies
- **Npgsql** - PostgreSQL driver
- **xUnit** - Testing framework
- **FluentAssertions** - Assertion library
- **Moq** - Mocking framework

### Build Quality Standards

- `TreatWarningsAsErrors` enabled (except Infrastructure/WebApi - see Directory.Build.props)
- All public APIs must have XML documentation
- Code analysis enabled with latest analyzers
- Deterministic builds for CI/CD
- Source Link enabled for debugging

## 10. API Endpoints

Base URL: `http://localhost:5000`

### Health Endpoints
- `GET /health` - Basic health status
- `GET /health/detailed` - Detailed health including database
- `GET /health/checks` - ASP.NET Core health checks

### Device Endpoints
- `GET /devices` - List all configured devices and status
- `GET /devices/{id}` - Get specific device status
- `POST /devices/{id}/restart` - Restart device connection

### Data Endpoints
- `GET /data/latest` - Latest readings from all devices
- `GET /data/latest/{deviceId}` - Latest readings for specific device
- `GET /data/stats` - Data collection statistics

### MQTT Endpoints
- `GET /mqtt/health` - MQTT broker connection and message statistics
- `GET /mqtt/devices` - List configured MQTT devices

Swagger UI: `http://localhost:5000/swagger`

## 11. Hardware Configuration

### ADAM-6000 Series Devices

**Digital I/O Modules (Counters)**
- **Models**: ADAM-6050, 6051, 6052, 6053, 6055, 6056
- **Protocol**: Modbus TCP (Holding Registers, Function Code 03)
- **Data Type**: 32-bit counters (2 registers, little-endian)
- **Configuration**: `RegisterType: HoldingRegister`, `DataType: UInt32Counter`

**Analog Input Modules**
- **Models**: ADAM-6015 (RTD), 6017 (analog), 6018 (thermocouple), 6024 (output)
- **Protocol**: Modbus TCP (Input Registers, Function Code 04)
- **Data Types**: Int16, UInt16, or Float32 depending on module
- **Configuration**: `RegisterType: InputRegister`, `DataType: Int16/UInt16/Float32`

**Register Mapping:**
- Digital counters: 2 consecutive registers per channel (e.g., Ch0 = 0-1, Ch1 = 2-3)
- Analog inputs: 1 or 2 registers per channel depending on data type
- All devices use default Modbus TCP port 502

### Configuration Examples

**Digital Counter (ADAM-6051):**
```json
{
  "ChannelNumber": 0,
  "StartRegister": 0,
  "RegisterCount": 2,
  "RegisterType": "HoldingRegister",
  "DataType": "UInt32Counter"
}
```

**Analog Temperature (ADAM-6017):**
```json
{
  "ChannelNumber": 0,
  "StartRegister": 0,
  "RegisterCount": 1,
  "RegisterType": "InputRegister",
  "DataType": "Int16",
  "ScaleFactor": 0.1,
  "Unit": "°C"
}
```

### MQTT Devices

**Protocol**: MQTT v4 (via MQTT broker like Mosquitto or EMQX)

**Supported Payload Formats:**
- **JSON**: Most flexible - uses JsonPath to extract channel and value
- **Binary**: Most compact - `[1 byte channel][N bytes value]`
- **CSV**: Simple text - `channel,value,timestamp`

**Data Types:**
- `UInt32`, `Int16`, `UInt16`, `Float32`, `Float64`

**Configuration Example (JSON):**
```json
{
  "DeviceId": "TEMP-SENSOR-01",
  "Topics": ["sensors/temperature", "sensors/+/temp"],
  "Format": "Json",
  "DataType": "Float32",
  "ChannelJsonPath": "$.channel",
  "ValueJsonPath": "$.temperature",
  "Unit": "°C",
  "ScaleFactor": 1.0
}
```

**Configuration Example (Binary):**
```json
{
  "DeviceId": "COUNTER-01",
  "Topics": ["counters/production"],
  "Format": "Binary",
  "DataType": "UInt32",
  "QosLevel": 1
}
```

**Topic Wildcards:**
- `+` - Single-level wildcard (e.g., `sensors/+/temperature`)
- `#` - Multi-level wildcard (e.g., `sensors/#`)

**See `docs/mqtt-guide.md` for complete setup, broker configuration, and troubleshooting.**

### Simulator Configuration

Simulators mimic ADAM-6051 behavior for testing:
- Listen on ports 5502, 5503, 5504
- Generate realistic counter increments
- Support all Modbus TCP commands
- Configurable via command-line arguments
- **Note**: Simulators currently only support digital counters (ADAM-6051)

## 12. Deployment

### Production Checklist

Before deploying:
- [ ] Change all default passwords in `.env`
- [ ] Generate secure JWT secret key (minimum 32 characters)
- [ ] Update `appsettings.Production.json` with real device IPs
- [ ] Configure firewall rules (Modbus TCP port 502, API port 5000)
- [ ] Set up database backups
- [ ] Configure monitoring/alerting
- [ ] Review CORS allowed origins
- [ ] Enable HTTPS

### Environment Variables

Use `docker/.env.template` as starting point:
- `TIMESCALEDB_PASSWORD` - Database password
- `JWT_SECRET_KEY` - JWT signing key
- `JWT_ISSUER`, `JWT_AUDIENCE` - JWT claims
- `CORS_ALLOWED_ORIGINS` - Comma-separated allowed origins

## 13. Project Structure

```
industrial-adam-logger/
├── src/
│   ├── Industrial.Adam.Logger.Core/          # Core business logic
│   │   ├── Models/                           # Domain models
│   │   ├── Processing/                       # Data processing
│   │   ├── Services/                         # Background services
│   │   ├── Storage/                          # Persistence interfaces
│   │   └── Configuration/                    # Config models
│   ├── Industrial.Adam.Logger.Core.Tests/    # Unit tests
│   ├── Industrial.Adam.Logger.WebApi/        # REST API host
│   ├── Industrial.Adam.Logger.Console/       # Console host
│   ├── Industrial.Adam.Logger.Simulator/     # Device simulators
│   ├── Industrial.Adam.Logger.IntegrationTests/
│   └── Industrial.Adam.Logger.Benchmarks/
├── docker/                                   # Docker infrastructure
│   ├── docker-compose.yml                    # Production compose
│   ├── docker-compose.simulator.yml          # With simulators
│   ├── Dockerfile                            # Multi-stage build
│   └── timescaledb/                          # DB init scripts
├── scripts/                                  # Automation scripts
├── docs/                                     # Documentation
├── Industrial.Adam.Logger.sln                # Solution file
├── Directory.Build.props                     # Global build config
└── CLAUDE.md                                 # This file
```

## 14. Migration Context

This repository was extracted from a larger multi-module platform (`adam-6000-counter`) and is now a focused, single-purpose logger service. See `MIGRATION-SUMMARY.md` for details.

**Removed dependencies**:
- OEE calculation modules (moved to separate repo)
- Equipment scheduling modules
- Admin dashboard modules
- External Security module (replaced with inline JWT in WebApi)

The logger is now self-contained with no external service dependencies beyond TimescaleDB.
