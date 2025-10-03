# Industrial ADAM Logger Simulator Configuration Guide

This guide explains how to configure the Industrial ADAM Logger Simulators for different production scenarios. The simulators now support a dual configuration system with separate technical and production settings.

## Table of Contents

1. [Overview](#overview)
2. [Configuration Architecture](#configuration-architecture)
3. [Technical Configuration (appsettings.json)](#technical-configuration-appsettingsjson)
4. [Production Configuration (production-profile.json)](#production-configuration-production-profilejson)
5. [Configuration Options Reference](#configuration-options-reference)
6. [Common Configuration Scenarios](#common-configuration-scenarios)
7. [Testing and Validation](#testing-and-validation)
8. [Troubleshooting](#troubleshooting)

## Overview

The Industrial ADAM Logger Simulator provides realistic production line simulation with:

- **Continuous Operation**: Automatic job restart with configurable idle periods
- **Counter Reset Logic**: Automatic counter resets when new jobs start (not during breakdowns)
- **Configurable Production Profiles**: Separate files for technical vs production settings
- **Realistic State Transitions**: Setup → RampUp → Running → RampDown → Idle cycle
- **Configurable Stoppages**: Minor and major breakdowns with realistic timing
- **Modbus TCP Server**: Full ADAM-6051 register compatibility
- **REST API**: Real-time monitoring and control

## Configuration Architecture

The simulator uses a **dual configuration system**:

```
├── appsettings.json          # Technical settings (ports, IDs, channels)
├── config/
    └── production-profile.json  # Production settings (rates, timing, behavior)
```

### Key Benefits

- **Separation of Concerns**: Technical settings stay with deployment, production settings can be easily modified
- **Environment Flexibility**: Different production profiles for different environments
- **Backward Compatibility**: Legacy appsettings.json structure still supported
- **Hot Reload**: Production profile changes can be applied without restart (future enhancement)

## Technical Configuration (appsettings.json)

Located in the simulator's root directory, this file contains deployment-specific settings:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Industrial.Adam.Logger.Simulator.Simulation": "Debug"
    }
  },
  "SimulatorSettings": {
    "DeviceId": "SIM-6051-01",
    "DeviceName": "Production Line 1 Simulator", 
    "ModbusPort": 5502,
    "ApiPort": 8080,
    "DatabasePath": "data/simulator.db"
  },
  "Channels": [
    {
      "Number": 0,
      "Name": "Main Product Counter",
      "Type": "ProductionCounter",
      "Enabled": true
    },
    {
      "Number": 1,
      "Name": "Reject Counter", 
      "Type": "RejectCounter",
      "Enabled": true,
      "RejectRate": 0.05
    },
    {
      "Number": 2,
      "Name": "Auxiliary Counter",
      "Type": "IndependentCounter", 
      "Enabled": false
    }
  ],
  "Schedule": {
    "ShiftStart": "08:00",
    "ShiftEnd": "17:00",
    "Breaks": [
      {"Time": "10:00", "Duration": 15},
      {"Time": "12:00", "Duration": 30},
      {"Time": "15:00", "Duration": 15}
    ]
  }
}
```

### Technical Settings Explained

| Section | Setting | Description |
|---------|---------|-------------|
| SimulatorSettings | DeviceId | Unique identifier for this simulator instance |
| SimulatorSettings | ModbusPort | TCP port for Modbus server (5502-5504 typically) |
| SimulatorSettings | ApiPort | HTTP port for REST API (8080-8083 typically) |
| Channels | Type | ProductionCounter, RejectCounter, or IndependentCounter |
| Schedule | Breaks | Scheduled production breaks during shifts |

## Production Configuration (production-profile.json)

Located in `config/production-profile.json`, this file contains production behavior settings:

```json
{
  "ProductionProfile": {
    "BaseRate": 120.0,
    "RateVariation": 0.1,
    "JobSizeMin": 1000,
    "JobSizeMax": 5000,
    "TimingSettings": {
      "SetupDurationMinutes": 0.25,
      "RampUpDurationSeconds": 30.0,
      "RampDownDurationSeconds": 10.0,
      "IdleBetweenJobsSeconds": 45.0,
      "DigitalPulseWidthMs": 50.0
    },
    "RampSettings": {
      "RampUpStartPercent": 20.0,
      "RampUpEndPercent": 100.0,
      "RampDownStartPercent": 100.0,
      "RampDownEndPercent": 10.0
    },
    "StoppageSettings": {
      "MinorStoppageProbability": 0.02,
      "MajorStoppageProbability": 0.005,
      "MinorStoppageMinSeconds": 30,
      "MinorStoppageMaxSeconds": 120,
      "MajorStoppageMinMinutes": 10,
      "MajorStoppageMaxMinutes": 30
    },
    "ContinuousOperation": {
      "Enabled": true,
      "AutoRestartAfterJob": true,
      "ResetCountersOnNewJob": true
    }
  }
}
```

## Configuration Options Reference

### Production Profile Settings

#### Base Production Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `BaseRate` | double | 120.0 | Target production rate (units per minute) |
| `RateVariation` | double | 0.1 | Random variation (±10% = ±0.1) |
| `JobSizeMin` | int | 1000 | Minimum units per job |
| `JobSizeMax` | int | 5000 | Maximum units per job |

#### Timing Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SetupDurationMinutes` | double | 0.25 | Job setup time (15 seconds) |
| `RampUpDurationSeconds` | double | 30.0 | Time to reach full speed |
| `RampDownDurationSeconds` | double | 10.0 | Time to slow down at job end |
| `IdleBetweenJobsSeconds` | double | 45.0 | Idle time between jobs |
| `DigitalPulseWidthMs` | double | 50.0 | Digital output pulse width |

#### Ramp Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `RampUpStartPercent` | double | 20.0 | Starting production rate during ramp-up |
| `RampUpEndPercent` | double | 100.0 | Ending production rate after ramp-up |
| `RampDownStartPercent` | double | 100.0 | Starting production rate during ramp-down |
| `RampDownEndPercent` | double | 10.0 | Ending production rate after ramp-down |

#### Stoppage Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MinorStoppageProbability` | double | 0.02 | Probability per minute (2%) |
| `MajorStoppageProbability` | double | 0.005 | Probability per minute (0.5%) |
| `MinorStoppageMinSeconds` | int | 30 | Minimum minor stoppage duration |
| `MinorStoppageMaxSeconds` | int | 120 | Maximum minor stoppage duration |
| `MajorStoppageMinMinutes` | int | 10 | Minimum major stoppage duration |
| `MajorStoppageMaxMinutes` | int | 30 | Maximum major stoppage duration |

#### Continuous Operation Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | true | Enable continuous operation mode |
| `AutoRestartAfterJob` | bool | true | Automatically start new jobs after completion |
| `ResetCountersOnNewJob` | bool | true | Reset counters when starting new jobs |

## Common Configuration Scenarios

### High-Speed Production Line

For fast production with frequent minor stoppages:

```json
{
  "ProductionProfile": {
    "BaseRate": 300.0,
    "RateVariation": 0.05,
    "JobSizeMin": 2000,
    "JobSizeMax": 8000,
    "TimingSettings": {
      "SetupDurationMinutes": 0.5,
      "RampUpDurationSeconds": 45.0,
      "IdleBetweenJobsSeconds": 30.0
    },
    "StoppageSettings": {
      "MinorStoppageProbability": 0.03,
      "MinorStoppageMinSeconds": 15,
      "MinorStoppageMaxSeconds": 60
    }
  }
}
```

### Slow Precision Manufacturing

For slow, precise production with rare but long stoppages:

```json
{
  "ProductionProfile": {
    "BaseRate": 30.0,
    "RateVariation": 0.02,
    "JobSizeMin": 100,
    "JobSizeMax": 500,
    "TimingSettings": {
      "SetupDurationMinutes": 2.0,
      "RampUpDurationSeconds": 120.0,
      "IdleBetweenJobsSeconds": 120.0
    },
    "StoppageSettings": {
      "MinorStoppageProbability": 0.01,
      "MajorStoppageProbability": 0.002,
      "MajorStoppageMinMinutes": 30,
      "MajorStoppageMaxMinutes": 120
    }
  }
}
```

### Testing and Development

For rapid testing with frequent state changes:

```json
{
  "ProductionProfile": {
    "BaseRate": 600.0,
    "JobSizeMin": 50,
    "JobSizeMax": 200,
    "TimingSettings": {
      "SetupDurationMinutes": 0.1,
      "RampUpDurationSeconds": 5.0,
      "RampDownDurationSeconds": 5.0,
      "IdleBetweenJobsSeconds": 10.0
    },
    "StoppageSettings": {
      "MinorStoppageProbability": 0.1,
      "MinorStoppageMinSeconds": 5,
      "MinorStoppageMaxSeconds": 15
    }
  }
}
```

### Batch Processing Mode

For discrete batches with long setup times:

```json
{
  "ProductionProfile": {
    "BaseRate": 80.0,
    "JobSizeMin": 5000,
    "JobSizeMax": 10000,
    "TimingSettings": {
      "SetupDurationMinutes": 5.0,
      "IdleBetweenJobsSeconds": 300.0
    },
    "ContinuousOperation": {
      "Enabled": false,
      "AutoRestartAfterJob": false
    }
  }
}
```

## Multiple Simulator Setup

To run multiple simulators simultaneously, create separate directories or use different configuration files:

### Simulator 1 (SIM-6051-01)
- Modbus Port: 5502
- API Port: 8081
- Device ID: SIM-6051-01

### Simulator 2 (SIM-6051-02)  
- Modbus Port: 5503
- API Port: 8082
- Device ID: SIM-6051-02

### Simulator 3 (SIM-6051-03)
- Modbus Port: 5504
- API Port: 8083
- Device ID: SIM-6051-03

## Testing and Validation

### 1. Configuration Validation

Test configuration loading:
```bash
# Check if simulator starts without errors
dotnet run --project src/Industrial.Adam.Logger.Simulator

# Verify configuration in logs
grep -i "production configured" logs/simulator*.log
```

### 2. API Testing

Verify simulator status:
```bash
# Health check
curl http://localhost:8081/api/simulator/health

# Get current status
curl http://localhost:8081/api/simulator/status | jq
```

### 3. Automated Testing

Run the comprehensive test suite:
```bash
./scripts/test-simulators.sh
```

### 4. Production State Testing

Force state transitions:
```bash
# Start new job
curl -X POST http://localhost:8081/api/simulator/start-job

# Force minor stoppage
curl -X POST http://localhost:8081/api/simulator/force-stoppage \
  -H "Content-Type: application/json" \
  -d '{"type": "MinorStoppage", "reason": "Test stoppage"}'
```

## Troubleshooting

### Common Issues

#### 1. Simulator Stuck in Idle
**Symptoms**: Simulator shows "Idle" state for extended periods
**Cause**: Continuous operation disabled or configuration error
**Solution**:
```json
{
  "ContinuousOperation": {
    "Enabled": true,
    "AutoRestartAfterJob": true
  }
}
```

#### 2. Counters Not Resetting
**Symptoms**: Counters continue incrementing across job boundaries
**Cause**: Counter reset disabled
**Solution**:
```json
{
  "ContinuousOperation": {
    "ResetCountersOnNewJob": true
  }
}
```

#### 3. Port Conflicts
**Symptoms**: "Port already in use" errors
**Cause**: Multiple simulators using same ports
**Solution**: Ensure unique ports in appsettings.json:
```json
{
  "SimulatorSettings": {
    "ModbusPort": 5502,  // Unique for each simulator
    "ApiPort": 8081      // Unique for each simulator
  }
}
```

#### 4. Configuration Not Loading
**Symptoms**: Default values used despite custom configuration
**Cause**: File path or JSON syntax errors
**Solution**:
1. Verify file exists: `config/production-profile.json`
2. Validate JSON syntax: `python -m json.tool config/production-profile.json`
3. Check file permissions: `chmod 644 config/production-profile.json`

### Logging Configuration

For detailed troubleshooting, enable debug logging:
```json
{
  "Logging": {
    "LogLevel": {
      "Industrial.Adam.Logger.Simulator.Simulation": "Debug",
      "Industrial.Adam.Logger.Simulator.Configuration": "Debug"
    }
  }
}
```

### Performance Monitoring

Monitor simulator performance:
```bash
# Watch API status
watch -n 1 'curl -s http://localhost:8081/api/simulator/status | jq ".state, .unitsProduced, .timeInState"'

# Monitor logs
tail -f logs/simulator1.log | grep -E "(state changed|counter reset|job|stoppage)"
```

## Integration with Logger Application

The simulators are designed to work seamlessly with the Industrial ADAM Logger:

### Logger Configuration (appsettings.json)
```json
{
  "DeviceSettings": [
    {
      "DeviceId": "SIM-6051-01",
      "Name": "Simulator 1",
      "ModbusEndpoint": "127.0.0.1:5502",
      "Channels": [
        {"Number": 0, "Name": "Main Counter", "Type": "Counter"},
        {"Number": 1, "Name": "Reject Counter", "Type": "Counter"}
      ]
    }
  ]
}
```

### Data Flow Verification

1. **Start Simulators**: All 3 simulators running on ports 5502-5504
2. **Start Logger**: Connects to all simulators via Modbus TCP
3. **Verify Data Flow**: Check TimescaleDB for incoming data
4. **Monitor Metrics**: Use Grafana dashboards for real-time monitoring

This configuration system provides maximum flexibility for different production scenarios while maintaining ease of use and deployment consistency.