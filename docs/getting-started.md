# Getting Started with Industrial ADAM Logger

## Overview

This guide will help you set up and run the Industrial ADAM Logger for the first time.

## Prerequisites

### Required
- .NET 9 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- Docker Desktop ([Download](https://www.docker.com/products/docker-desktop))

### Optional
- Physical ADAM-6051 device (simulators available for testing)
- Visual Studio 2022 / VS Code / Rider

## Installation

### Step 1: Clone Repository

```bash
git clone https://github.com/yourusername/industrial-adam-logger.git
cd industrial-adam-logger
```

### Step 2: Start TimescaleDB

```bash
cd docker
docker-compose up -d timescaledb
```

Verify TimescaleDB is running:
```bash
docker ps
```

You should see `adam-timescaledb` container running on port 5432.

### Step 3: Configure Application

The default configuration works with simulators. For physical devices, edit:

`src/Industrial.Adam.Logger.WebApi/appsettings.json`

```json
{
  "AdamLogger": {
    "Devices": [
      {
        "DeviceId": "ADAM-001",
        "IpAddress": "192.168.1.100",  // Your device IP
        "Port": 502,
        "PollIntervalMs": 1000,
        "Channels": [...]
      }
    ]
  }
}
```

### Step 4: Run the Logger

```bash
cd ..
dotnet restore
dotnet build
dotnet run --project src/Industrial.Adam.Logger.WebApi
```

The logger will start on `http://localhost:5000`.

### Step 5: Verify Installation

Open your browser to `http://localhost:5000` to see the Swagger API documentation.

Test the health endpoint:
```bash
curl http://localhost:5000/health
```

Expected response:
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-03T...",
  "service": {
    "isRunning": true,
    "startTime": "..."
  },
  "devices": {
    "total": 1,
    "connected": 1
  }
}
```

## Testing with Simulators

If you don't have physical hardware, use the built-in simulators:

### Start Simulators
```bash
./scripts/start-simulators.sh
```

This starts 3 ADAM-6051 simulators on ports 5502, 5503, and 5504.

### Configure for Simulators
The default `appsettings.json` is already configured for simulators at:
- `127.0.0.1:5502`
- `127.0.0.1:5503`
- `127.0.0.1:5504`

### Stop Simulators
```bash
./scripts/stop-simulators.sh
```

## Next Steps

1. **Explore the API**: Visit `http://localhost:5000` to see all available endpoints
2. **Query Data**: Use `/data/latest` to see real-time counter readings
3. **Monitor Devices**: Check `/devices` for device connectivity status
4. **Configure Channels**: Edit `appsettings.json` to match your hardware setup

## Common Tasks

### Add a New Device
1. Edit `appsettings.json`
2. Add new device configuration to the `Devices` array
3. Restart the logger service

### View Logs
```bash
# Console output shows INFO level logs
# Edit appsettings.json to change log levels
```

### Check Database
```bash
# Connect to TimescaleDB
docker exec -it adam-timescaledb psql -U industrial_system -d adam_logger

# Query counter data
SELECT * FROM counter_data ORDER BY timestamp DESC LIMIT 10;
```

## Troubleshooting

### Logger won't start
- Check TimescaleDB is running: `docker ps`
- Verify connection string in `appsettings.json`
- Check logs for error messages

### Can't connect to devices
- Verify device IP addresses are correct
- Check network connectivity: `ping <device-ip>`
- Ensure Modbus TCP port 502 is open
- Try with simulators first to isolate hardware issues

### No data in database
- Check `/health` endpoint for device connectivity
- Verify channels are configured correctly
- Check TimescaleDB logs: `docker logs adam-timescaledb`

## Next Documentation

- [API Reference](api-reference.md)
- [Hardware Setup](hardware-setup.md)
- [Deployment Guide](deployment.md)
- [Simulator Guide](simulator-guide.md)
