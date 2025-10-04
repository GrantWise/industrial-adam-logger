# Troubleshooting Guide - Industrial ADAM Logger

**Purpose**: Quick reference for common issues and resolutions

---

## Service Won't Start

### Symptom
Service fails to start with error message

### Common Causes & Solutions

**1. TimescaleDB host not configured**
```
Error: TimescaleDB host not configured. Set 'AdamLogger:TimescaleDb:Host' in appsettings.json
```
**Solution**: Edit `appsettings.json` and set:
```json
{
  "AdamLogger": {
    "TimescaleDb": {
      "Host": "localhost",
      "Port": 5432,
      "Database": "adam_logger"
    }
  }
}
```

**2. TimescaleDB database not configured**
```
Error: TimescaleDB database not configured. Set 'AdamLogger:TimescaleDb:Database' in appsettings.json
```
**Solution**: Edit `appsettings.json` and set database name (see above)

**3. API keys file permission denied**
```
Error: Permission denied reading API keys file config/apikeys.json
```
**Solution**: Fix file permissions:
```bash
# Unix/Linux
chmod 600 config/apikeys.json

# Windows (run as Administrator)
icacls config\apikeys.json /inheritance:r /grant:r "%USERNAME%:F"
```

---

## Authentication Failing

### Symptom
API returns `401 Unauthorized` for all requests

### Common Causes & Solutions

**1. API keys file not found**
Check startup logs for:
```
WARN: API keys file not found at config/apikeys.json. Authentication will fail until file is created.
```

**Solution**: Create the file:
```bash
cp config/apikeys.example.json config/apikeys.json
# Edit config/apikeys.json and set strong API keys
```

**2. Invalid API key**
Check logs for:
```
WARN: Authentication failed: Invalid or expired API key
```

**Solution**: Verify API key in `config/apikeys.json` matches the one you're using

**3. Expired API key**
**Solution**: Check `expiresAt` field in `config/apikeys.json`. Update or remove expiration.

**4. Not providing X-API-Key header**
**Solution**: Add header to requests:
```bash
curl -H "X-API-Key: your-key-here" http://localhost:5000/devices
```

---

## No Data Being Collected

### Symptom
Service running but no device readings in database

### Diagnostic Steps

**1. Check health endpoint**
```bash
curl http://localhost:5000/health
```

Look for:
```json
{
  "status": "Healthy",
  "devices": {
    "total": 3,
    "connected": 0  // ← Problem: No devices connected
  }
}
```

**2. Check device connectivity**
```bash
curl -H "X-API-Key: your-key" http://localhost:5000/devices
```

Look for device status - should show "Connected": true

**3. Check logs for connection errors**
```bash
# Docker
docker logs adam-logger | grep -i "error\|failed"

# Systemd
journalctl -u adam-logger | grep -i "error\|failed"
```

### Common Causes & Solutions

**1. Wrong device IP address**
**Solution**: Verify IP in `appsettings.json` matches actual device

**2. Network/firewall blocking Modbus TCP port 502**
**Solution**: Test connectivity:
```bash
telnet <device-ip> 502
```
If fails, check network and firewall rules

**3. Device not responding**
**Solution**: Check device is powered on and accessible on network

---

## Database Connection Failing

### Symptom
Logs show database errors:
```
WARN: TimescaleDB connection test failed at startup
```

### Diagnostic Steps

**1. Check if TimescaleDB is running**
```bash
# Docker
docker ps | grep timescaledb

# PostgreSQL service
systemctl status postgresql
```

**2. Test connection manually**
```bash
psql -h localhost -p 5432 -U adam_logger -d adam_logger
```

**3. Check database health from API**
```bash
curl http://localhost:5000/health/detailed
```

### Common Causes & Solutions

**1. TimescaleDB not running**
**Solution**: Start TimescaleDB:
```bash
# Docker
docker-compose up -d timescaledb

# Service
systemctl start postgresql
```

**2. Wrong database credentials**
**Solution**: Verify connection string in `appsettings.json` matches database

**3. Database doesn't exist**
**Solution**: Create database:
```bash
createdb -h localhost -U postgres adam_logger
```

---

## Service Running Slowly

### Symptom
API responses taking >1 second

### Diagnostic Steps

**1. Check /data/stats endpoint**
```bash
curl -H "X-API-Key: your-key" http://localhost:5000/data/stats
```

Look for:
- High `totalReadings` count (memory usage)
- Many devices with "Bad" or "Unavailable" quality

**2. Check /health/detailed**
```bash
curl http://localhost:5000/health/detailed
```

Look for database connection issues

**3. Check logs for errors**

### Common Causes & Solutions

**1. Dead letter queue backing up**
Check logs for:
```
WARN: Added failed batch to dead letter queue
```
**Solution**: Fix database connectivity so batches can be processed

**2. Too many failed device connections**
**Solution**: Remove or fix unreachable devices in configuration

**3. Database slow**
**Solution**: Check TimescaleDB performance, consider tuning

---

## High Memory Usage

### Symptom
Service using excessive RAM

### Diagnostic Steps

**1. Check /health/detailed**
Look for `pendingWrites` or `deadLetterQueueSize`

**2. Check /data/stats**
Look for `totalReadings` (in-memory cache)

### Solutions

**1. Clear data cache** (if not needed):
```bash
curl -X DELETE -H "X-API-Key: your-key" http://localhost:5000/data/cache
```

**2. Reduce poll interval** in appsettings.json:
```json
{
  "AdamLogger": {
    "Devices": [{
      "PollIntervalMs": 5000  // Increase from 1000 to 5000
    }]
  }
}
```

**3. Increase database batch flush frequency**:
```json
{
  "AdamLogger": {
    "TimescaleDb": {
      "FlushIntervalMs": 1000  // Decrease from 5000 to 1000
    }
  }
}
```

---

## Health Check Always Returns "Unhealthy"

### Symptom
`/health` returns 503 Service Unavailable

### Diagnostic Steps

**1. Check detailed health**
```bash
curl http://localhost:5000/health/detailed
```

Look at each component:
- Service: Should be "Healthy"
- Database: Should be "Healthy"
- Devices: May be "Degraded" if some offline

### Solutions

**Service Unhealthy**: Service not running properly - check logs
**Database Unhealthy**: Fix database connection (see "Database Connection Failing")
**Devices Degraded/Unhealthy**: Some devices offline - check device connectivity

---

## MQTT Devices Not Receiving Data

### Symptom
MQTT devices configured but no data

### Diagnostic Steps

**1. Check MQTT health**
```bash
curl -H "X-API-Key: your-key" http://localhost:5000/mqtt/health
```

**2. Check MQTT broker is running**
```bash
# Docker
docker ps | grep mosquitto

# Test connection
mosquitto_sub -h localhost -t "test" -v
```

**3. Check logs for MQTT errors**

### Common Causes & Solutions

**1. MQTT broker not running**
**Solution**: Start broker:
```bash
docker-compose up -d mosquitto
```

**2. Wrong topic subscription**
**Solution**: Verify topics in `appsettings.json` match published topics

**3. QoS mismatch**
**Solution**: Check QoS levels match between publisher and subscriber

---

## Quick Diagnostics Checklist

Run these commands to get full system status:

```bash
# 1. Service health
curl http://localhost:5000/health/detailed

# 2. Device status
curl -H "X-API-Key: your-key" http://localhost:5000/devices

# 3. Data statistics
curl -H "X-API-Key: your-key" http://localhost:5000/data/stats

# 4. MQTT health (if using MQTT)
curl -H "X-API-Key: your-key" http://localhost:5000/mqtt/health

# 5. Check logs
docker logs adam-logger --tail 100
# or
journalctl -u adam-logger -n 100

# 6. Check database connection
docker exec -it timescaledb psql -U adam_logger -d adam_logger -c "SELECT COUNT(*) FROM device_readings;"
```

---

## Getting Help

If issue persists after trying these solutions:

1. **Collect diagnostic information**:
   - Output from all commands in "Quick Diagnostics Checklist"
   - Recent logs (last 100 lines)
   - Configuration file (with secrets removed)

2. **Check documentation**:
   - `docs/getting-started.md` - Setup guide
   - `docs/api-key-authentication.md` - Authentication guide
   - `docs/mqtt-guide.md` - MQTT setup guide

3. **Contact support** with:
   - Clear description of issue
   - Steps to reproduce
   - Diagnostic information collected above
   - Environment (OS, Docker version, etc.)
