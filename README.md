# Industrial ADAM Logger

**Industrial-grade data acquisition service for ADAM-6000 series modules and MQTT devices.**
Modbus TCP + MQTT → TimescaleDB pipeline with dead letter queue reliability for 24/7 manufacturing operations.

## What It Does

Connects to industrial devices via **Modbus TCP** or **MQTT**, reads data from configured channels/topics, and persists time-series data to TimescaleDB with zero data loss guarantees.

**Supported Protocols:**
- ✅ **Modbus TCP** - Direct polling of ADAM-6000 series hardware
- ✅ **MQTT** - Event-driven data logging from any MQTT-enabled device

### Supported Hardware

**Digital I/O Modules (Counter-based)**
- ✅ ADAM-6050 (12 DI / 6 DO)
- ✅ ADAM-6051 (16 DI)
- ✅ ADAM-6052 (8 DI / 8 DO)
- ✅ ADAM-6053 (16 DI / 16 DO)
- ✅ ADAM-6055 (16 DI / 16 DO sink)
- ✅ ADAM-6056 (12 DI / 6 DO)

**Analog Input Modules**
- ✅ ADAM-6015 (7-ch RTD temperature)
- ✅ ADAM-6017 (8-ch analog ±10V)
- ✅ ADAM-6018 (8-ch thermocouple)
- ✅ ADAM-6024 (4-ch analog output)

### Core Features

**Data Acquisition:**
- ✅ **Dual Protocol Support** - Modbus TCP (polling) + MQTT (event-driven)
- ✅ **Multi-Model Support** - Digital counters + analog inputs (temperature, voltage, etc.)
- ✅ **Concurrent Multi-Device** - Poll/subscribe to multiple devices simultaneously
- ✅ **MQTT Wildcards** - Topic patterns with `+` and `#` support
- ✅ **Flexible Payloads** - JSON, Binary, CSV formats

**Reliability:**
- ✅ **Dead Letter Queue** - Zero data loss with automatic recovery
- ✅ **Auto-Reconnect** - Handles network interruptions gracefully
- ✅ **Quality of Service** - MQTT QoS 0/1/2 support
- ✅ **Certificate Validation** - Production-grade TLS/SSL security

**Data Processing:**
- ✅ **Windowed Rate Calculation** - Smooth production rate metrics (for counters)
- ✅ **Counter Overflow Detection** - Handles 16-bit and 32-bit counter wraparounds
- ✅ **Data Quality Indicators** - Good, Degraded, Bad, Unavailable (21 CFR Part 11 compliant)
- ✅ **Scale Factors** - Apply unit conversions automatically

**Operations:**
- ✅ **Configuration-Driven** - Add devices via JSON, no code changes needed
- ✅ **TimescaleDB Integration** - Optimized time-series storage with hypertables
- ✅ **REST API** - Query device status and historical data
- ✅ **Health Monitoring** - Built-in health checks and diagnostics
- ✅ **Device Simulators** - Test without physical hardware

## Quick Start

### Prerequisites
- .NET 9 SDK
- Docker & Docker Compose
- PostgreSQL/TimescaleDB (via Docker)

### 3-Step Setup

```bash
# 1. Clone and navigate
git clone https://github.com/yourusername/industrial-adam-logger.git
cd industrial-adam-logger

# 2. Start TimescaleDB
cd docker
docker-compose up -d timescaledb

# 3. Run the logger
cd ..
dotnet run --project src/Industrial.Adam.Logger.WebApi
```

Visit `http://localhost:5000` for Swagger API documentation.

### With Simulators (No Hardware Required)

```bash
# Start simulators
./scripts/start-simulators.sh

# In another terminal, run logger
dotnet run --project src/Industrial.Adam.Logger.WebApi
```

## Architecture

```
┌─────────────┐    Modbus TCP    ┌──────────────┐    SQL    ┌─────────────┐
│ ADAM-6000   │─────────────────▶│              │──────────▶│             │
│ Devices     │                   │              │           │             │
└─────────────┘                   │              │           │             │
                                  │   Logger     │           │ TimescaleDB │
┌─────────────┐    MQTT Broker   │   Service    │           │             │
│ MQTT        │─────────────────▶│              │           │             │
│ Devices     │    (pub/sub)     │              │           │             │
└─────────────┘                   └──────────────┘           └─────────────┘
                                        │
                                        ├─ Dead Letter Queue
                                        ├─ Rate Calculator
                                        ├─ Data Quality Monitor
                                        └─ Health Monitor
```

### Clean Architecture Layers
- **Domain**: Core business logic and models
- **Application**: Data processing and rate calculation
- **Infrastructure**: TimescaleDB storage, Modbus/MQTT communication
- **WebApi**: REST endpoints and health checks

## Configuration

Edit `src/Industrial.Adam.Logger.WebApi/appsettings.json`:

### Digital Counter Example (ADAM-6051)
```json
{
  "DeviceId": "COUNTER-01",
  "ModelType": "ADAM-6051",
  "IpAddress": "192.168.1.100",
  "Port": 502,
  "Channels": [
    {
      "ChannelNumber": 0,
      "Name": "Production Counter",
      "StartRegister": 0,
      "RegisterCount": 2,
      "Unit": "items"
    }
  ]
}
```

### Analog Input Example (ADAM-6017)
```json
{
  "DeviceId": "TEMP-01",
  "ModelType": "ADAM-6017",
  "IpAddress": "192.168.1.105",
  "Port": 502,
  "Channels": [
    {
      "ChannelNumber": 0,
      "Name": "Oven Temperature",
      "StartRegister": 0,
      "RegisterCount": 1,
      "RegisterType": "InputRegister",
      "DataType": "Int16",
      "ScaleFactor": 0.1,
      "Unit": "°C",
      "MinValue": -50,
      "MaxValue": 1000
    }
  ]
}
```

**Key Configuration Fields:**
- `RegisterType`: `HoldingRegister` (counters) or `InputRegister` (analog)
- `DataType`: `UInt32Counter`, `Int16`, `UInt16`, `Float32`, `Int32`
- `ScaleFactor`: Multiply raw value (e.g., 0.1 to convert to decimal)
- `Unit`: Measurement unit for display

### MQTT Device Example

```json
{
  "Mqtt": {
    "BrokerHost": "localhost",
    "BrokerPort": 1883,
    "ClientId": "industrial-logger",
    "QualityOfServiceLevel": 1
  },
  "MqttDevices": [
    {
      "DeviceId": "TEMP-SENSOR-01",
      "Name": "Temperature Sensor",
      "Topics": ["sensors/temperature", "sensors/+/temp"],
      "Format": "Json",
      "DataType": "Float32",
      "ChannelJsonPath": "$.channel",
      "ValueJsonPath": "$.temperature",
      "Unit": "°C"
    }
  ]
}
```

**MQTT Configuration Fields:**
- `Topics`: Array of MQTT topics (supports `+` and `#` wildcards)
- `Format`: `Json`, `Binary`, or `Csv`
- `QosLevel`: Optional per-device QoS (0, 1, or 2)
- `ChannelJsonPath`: JsonPath to channel number (for JSON format)
- `ValueJsonPath`: JsonPath to numeric value (for JSON format)

**📖 See [MQTT Guide](docs/mqtt-guide.md) for complete setup instructions**

## API Endpoints

### Health
- `GET /health` - Service health status
- `GET /health/detailed` - Detailed health including database
- `GET /health/checks` - Built-in ASP.NET health checks

### Devices
- `GET /devices` - List all devices and their status
- `GET /devices/{id}` - Get specific device status
- `POST /devices/{id}/restart` - Restart device connection

### Data
- `GET /data/latest` - Latest readings from all devices
- `GET /data/latest/{deviceId}` - Latest readings for specific device
- `GET /data/stats` - Data collection statistics

### MQTT
- `GET /mqtt/health` - MQTT broker connection and message statistics
- `GET /mqtt/devices` - List configured MQTT devices

## Additional Documentation

See the `docs/` directory for detailed guides:
- [Getting Started Guide](docs/getting-started.md) - Step-by-step setup
- [MQTT Guide](docs/mqtt-guide.md) - **MQTT setup, configuration, and troubleshooting**
- [Simulator Guide](docs/simulator-guide.md) - Testing without hardware
- [E2E Testing Guide](docs/e2e-testing-guide.md) - Integration testing
- [Development Guidelines](CLAUDE.md) - For contributors and AI assistants
- [GitHub Workflow](GITHUB-WORKFLOW.md) - Branching and commit conventions

## Development

### Build & Test
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run all tests
dotnet test

# Run with coverage
./scripts/run-coverage.sh
```

### Project Structure
```
industrial-adam-logger/
├── src/
│   ├── Industrial.Adam.Logger.Core/          # Core business logic
│   ├── Industrial.Adam.Logger.Core.Tests/    # Unit tests
│   ├── Industrial.Adam.Logger.WebApi/        # REST API
│   ├── Industrial.Adam.Logger.Console/       # Console host
│   ├── Industrial.Adam.Logger.Simulator/     # Device simulators
│   ├── Industrial.Adam.Logger.IntegrationTests/
│   └── Industrial.Adam.Logger.Benchmarks/
├── docker/                                   # Docker infrastructure
├── scripts/                                  # Development scripts
└── docs/                                     # Documentation
```

## Deployment

### Docker Compose (Recommended)
```bash
cd docker
cp .env.template .env
# Edit .env with your passwords
docker-compose up -d
```

### Production Checklist
- [ ] Change all default passwords
- [ ] Generate secure JWT secret key (min 32 characters)
- [ ] Configure firewall rules
- [ ] Set up database backups
- [ ] Configure monitoring/alerting
- [ ] Review CORS allowed origins
- [ ] Enable HTTPS

## Technology Stack

- **.NET 9** with C# 13
- **TimescaleDB** (PostgreSQL + time-series)
- **NModbus** - Modbus TCP client
- **Polly** - Retry policies
- **Npgsql** - PostgreSQL driver
- **xUnit** - Testing framework

## License

[Your License Here]

## Support

For issues, questions, or contributions, please open an issue on GitHub.

---

**Built for industrial reliability. Designed for 24/7 operation. Optimized for manufacturing.**
