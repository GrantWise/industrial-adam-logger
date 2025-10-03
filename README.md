# Industrial ADAM Logger

**Industrial-grade data acquisition service for ADAM-6051 counter devices.**
Modbus TCP → TimescaleDB pipeline with dead letter queue reliability for 24/7 manufacturing operations.

## What It Does

Connects to ADAM-6051 industrial counter devices via Modbus TCP, reads counter values from configured channels, and persists time-series data to TimescaleDB with zero data loss guarantees.

### Core Features

- ✅ **Modbus TCP Communication** - Robust device connectivity with automatic retry
- ✅ **Concurrent Multi-Device Polling** - Poll multiple ADAM devices simultaneously
- ✅ **TimescaleDB Integration** - Optimized time-series storage with hypertables
- ✅ **Dead Letter Queue** - Zero data loss with automatic recovery
- ✅ **Windowed Rate Calculation** - Smooth production rate metrics
- ✅ **Counter Overflow Detection** - Handles 16-bit and 32-bit counter wraparounds
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
│ ADAM-6051   │─────────────────▶│ Logger       │──────────▶│ TimescaleDB │
│ Devices     │                   │ Service      │           │             │
└─────────────┘                   └──────────────┘           └─────────────┘
                                        │
                                        ├─ Dead Letter Queue
                                        ├─ Rate Calculator
                                        └─ Health Monitor
```

### Clean Architecture Layers
- **Domain**: Core business logic and models
- **Application**: Data processing and rate calculation
- **Infrastructure**: TimescaleDB storage, Modbus communication
- **WebApi**: REST endpoints and health checks

## Configuration

Edit `src/Industrial.Adam.Logger.WebApi/appsettings.json`:

```json
{
  "AdamLogger": {
    "Devices": [
      {
        "DeviceId": "ADAM-001",
        "IpAddress": "192.168.1.100",
        "Port": 502,
        "PollIntervalMs": 1000,
        "Channels": [
          {
            "ChannelNumber": 0,
            "Name": "Main Counter",
            "StartRegister": 0,
            "RegisterCount": 2,
            "ScaleFactor": 1.0
          }
        ]
      }
    ]
  },
  "TimescaleDb": {
    "Host": "localhost",
    "Port": 5432,
    "Database": "adam_logger",
    "Username": "industrial_system",
    "Password": "your-password"
  }
}
```

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

## Hardware Compatibility

| Model | Status | Notes |
|-------|--------|-------|
| ADAM-6051 | ✅ Fully Supported | 16-channel digital input counter |
| ADAM-6052 | 🟡 Compatible | Should work, untested |
| ADAM-6060 | 🟡 Compatible | 6-channel relay, counter mode |

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

## Documentation

- [Getting Started Guide](docs/getting-started.md)
- [API Reference](docs/api-reference.md)
- [Hardware Setup](docs/hardware-setup.md)
- [Deployment Guide](docs/deployment.md)
- [Development Guidelines](CLAUDE.md)

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
