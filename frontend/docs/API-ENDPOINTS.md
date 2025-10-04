# Backend API Endpoints Documentation

**Base URL**: `http://localhost:5139`

## Authentication

Most endpoints require API Key authentication via `X-API-Key` header.
**Exception**: `/health` and `/health/detailed` are public endpoints.

---

## ⚠️ IMPORTANT: Health Endpoint Differences

The backend has **two different health endpoints** with **different response structures**:

| Feature | `/health` | `/health/detailed` |
|---------|-----------|-------------------|
| Database status | ❌ NOT included | ✅ Included in `components.database` |
| MQTT status | ❌ NOT included | ✅ Included in `components.mqtt` (future) |
| Devices location | `devices.health` | `components.devices.details` |
| Response wrapper | Direct properties | Wrapped in `components` |
| Use case | Simple health checks | Dashboard/monitoring |

**For frontend Dashboard: Always use `/health/detailed`** to get complete system status including database.

## Health Endpoints

### GET /health

**Auth**: None required
**Response Structure**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-04T15:02:49.5116437+00:00",
  "service": {
    "isRunning": true,
    "startTime": "2025-10-04T14:40:49.2025635+00:00",
    "uptime": "00:22:00.3092865"
  },
  "devices": {
    "total": 3,
    "connected": 0,
    "health": {
      "DEVICE-ID": {
        "deviceId": "string",
        "isConnected": boolean,
        "lastSuccessfulRead": "datetime | null",
        "consecutiveFailures": number,
        "lastError": "string | null",
        "totalReads": number,
        "successfulReads": number,
        "successRate": number,
        "isOffline": boolean
      }
    }
  }
}
```

**Note**: This endpoint does NOT include database or MQTT status.

### GET /health/detailed

**Auth**: None required
**Response Structure**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-04T14:42:29.7007404+00:00",
  "components": {
    "service": {
      "status": "Healthy",
      "isRunning": true,
      "startTime": "2025-10-04T14:40:49.2025635+00:00",
      "uptime": "00:01:40.4981782"
    },
    "database": {
      "status": "Healthy",
      "connected": true
    },
    "devices": {
      "status": "Unhealthy",
      "total": 3,
      "connected": 0,
      "details": {
        "DEVICE-ID": {
          "deviceId": "string",
          "isConnected": boolean,
          "lastSuccessfulRead": "datetime | null",
          "consecutiveFailures": number,
          "lastError": "string | null",
          "totalReads": number,
          "successfulReads": number,
          "successRate": number,
          "isOffline": boolean
        }
      }
    }
  }
}
```

**Key Differences from `/health`**:
- Wrapped in `components` object
- Includes `database` status
- Devices in `components.devices.details` (not `devices.health`)
- Each component has a `status` field ("Healthy", "Unhealthy", "Degraded")

## Device Endpoints

### GET /devices

**Auth**: Required (`X-API-Key` header)
**Returns**: Dictionary of device health statuses (same structure as `/health/detailed` devices)

### GET /devices/{deviceId}

**Auth**: Required
**Returns**: Single device health object

## Data Endpoints

### GET /data/latest

**Auth**: Required
**Returns**: Latest readings from all devices

### GET /data/latest/{deviceId}

**Auth**: Required
**Returns**: Latest readings for specific device

### GET /data/stats

**Auth**: Required
**Returns**: Data collection statistics

## Frontend Usage

**For Dashboard (no auth required)**:
- Use `/health/detailed` for complete system status including database
- Poll every 10 seconds with React Query

**For Device Management (requires auth)**:
- Use `/devices` for device list
- Use `/data/latest` for current readings

**Current Issue**: Frontend proxy working but authenticated endpoints need API key setup.
