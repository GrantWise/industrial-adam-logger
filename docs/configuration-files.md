# Configuration Files Reference

This document lists all editable configuration files that the frontend needs to support.

## 1. Core Configuration Files

### 1.1 `appsettings.json` (Main Application Configuration)
**Location**: `src/Industrial.Adam.Logger.WebApi/appsettings.json`
**Hot-Reload**: ❌ Requires service restart
**Purpose**: Core application configuration including devices, database, CORS

**Sections**:

#### Logging
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information|Debug|Warning|Error|Critical",
      "Microsoft.AspNetCore": "Warning",
      "Industrial.Adam.Logger.Core": "Debug"
    }
  }
}
```

#### API Keys
```json
{
  "ApiKeys": {
    "FilePath": "config/apikeys.json"  // Path to API keys file
  }
}
```

#### CORS
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5173",
      "https://your-frontend-domain.com"
    ]
  }
}
```

#### Modbus Devices
```json
{
  "AdamLogger": {
    "Devices": [
      {
        "DeviceId": "string",              // Required - Unique device ID
        "Name": "string",                  // Optional - Display name
        "ModelType": "ADAM-6051",          // Optional - Device model
        "IpAddress": "192.168.1.100",      // Required - IP or hostname
        "Port": 502,                       // Required - Modbus TCP port (1-65535)
        "UnitId": 1,                       // Required - Modbus Unit ID (1-255)
        "Enabled": true,                   // Required - Enable/disable device
        "PollIntervalMs": 1000,            // Required - Poll interval (100-300000)
        "TimeoutMs": 3000,                 // Required - Connection timeout
        "MaxRetries": 3,                   // Required - Retry attempts
        "KeepAlive": true,                 // Optional - TCP keep-alive
        "Channels": [
          {
            "ChannelNumber": 0,            // Required - Channel number (0-255)
            "Name": "Counter",             // Optional - Display name
            "StartRegister": 0,            // Required - Modbus register address
            "RegisterCount": 2,            // Required - Number of registers (1 or 2)
            "RegisterType": "HoldingRegister|InputRegister",  // Default: HoldingRegister
            "DataType": "UInt32Counter|Int16|UInt16|Float32|Int32",  // Default: UInt32Counter
            "Enabled": true,               // Required - Enable/disable channel
            "ScaleFactor": 1.0,            // Optional - Scaling factor
            "Offset": 0.0,                 // Optional - Offset value
            "Unit": "items",               // Optional - Unit of measurement
            "MinValue": 0,                 // Optional - Validation: min value
            "MaxValue": 4294967295,        // Optional - Validation: max value
            "MaxChangeRate": 1000,         // Optional - Max rate change/sec
            "RateWindowSeconds": 60,       // Optional - Rate calculation window
            "Tags": {}                     // Optional - Additional metadata
          }
        ]
      }
    ]
  }
}
```

**Data Types**:
- `UInt32Counter` - 32-bit unsigned (2 registers) - ADAM-6051 counters
- `Int16` - 16-bit signed (-32768 to 32767)
- `UInt16` - 16-bit unsigned (0 to 65535)
- `Float32` - IEEE 754 32-bit float (2 registers)
- `Int32` - 32-bit signed (2 registers)

**Register Types**:
- `HoldingRegister` - Function Code 03 (digital counters, coils)
- `InputRegister` - Function Code 04 (analog inputs)

#### MQTT Configuration (Optional)
```json
{
  "AdamLogger": {
    "Mqtt": {
      "BrokerHost": "localhost",         // Required - MQTT broker hostname
      "BrokerPort": 1883,                // Required - MQTT broker port
      "Username": "logger",              // Optional - MQTT username
      "Password": "secret",              // Optional - MQTT password
      "ClientId": "industrial-logger",   // Optional - MQTT client ID
      "UseTls": false,                   // Optional - Enable TLS
      "AllowInvalidCertificates": false, // Optional - Skip cert validation
      "KeepAlivePeriodSeconds": 60,      // Optional - Keep-alive interval
      "QualityOfServiceLevel": 1,        // Optional - Global QoS (0/1/2)
      "ReconnectDelaySeconds": 5,        // Optional - Auto-reconnect delay
      "MaxReconnectAttempts": 0,         // Optional - 0 = infinite
      "CleanSession": null               // Optional - Clean session flag
    },
    "MqttDevices": [
      {
        "DeviceId": "TEMP-SENSOR-01",    // Required - Unique device ID
        "Name": "Temperature Sensor",    // Optional - Display name
        "ModelType": "DHT22",            // Optional - Device model
        "Enabled": true,                 // Required - Enable/disable device
        "Topics": [                      // Required - MQTT topics (supports +, #)
          "sensors/temperature",
          "sensors/+/temp"
        ],
        "Format": "Json|Binary|Csv",     // Required - Payload format
        "DataType": "UInt32|Int16|UInt16|Float32|Float64",  // Required
        "QosLevel": 1,                   // Optional - Override global QoS
        "DeviceIdJsonPath": "$.device.id",  // Optional - Extract device ID
        "ChannelJsonPath": "$.channel",  // Required for JSON - Extract channel
        "ValueJsonPath": "$.temperature",// Required for JSON - Extract value
        "TimestampJsonPath": "$.timestamp",  // Optional - Extract timestamp
        "ScaleFactor": 1.0,              // Optional - Scaling factor
        "Unit": "°C"                     // Optional - Unit of measurement
      }
    ]
  }
}
```

**MQTT Payload Formats**:
- `Json` - Most flexible, uses JsonPath for field extraction
- `Binary` - Compact, `[1 byte channel][N bytes value]`
- `Csv` - Simple text, `channel,value,timestamp`

#### TimescaleDB
```json
{
  "AdamLogger": {
    "TimescaleDb": {
      "Host": "localhost",               // Required - Database host
      "Port": 5432,                      // Required - Database port
      "Database": "adam_counters",       // Required - Database name
      "Username": "logger_service",      // Required - DB username
      "Password": "secret",              // Required - DB password
      "TableName": "counter_data",       // Required - Table name (max 63 chars)
      "BatchSize": 100,                  // Optional - Batch size (1-1000)
      "BatchTimeoutMs": 5000,            // Optional - Batch timeout
      "FlushIntervalMs": 5000,           // Optional - Flush interval
      "EnableSsl": false,                // Optional - Enable SSL
      "TimeoutSeconds": 30,              // Optional - Query timeout
      "MaxPoolSize": 20,                 // Optional - Connection pool max
      "MinPoolSize": 5,                  // Optional - Connection pool min
      "Tags": {                          // Optional - Global tags for all data
        "location": "factory_floor",
        "environment": "production"
      }
    }
  }
}
```

**Validation Rules**:
- `TableName` max 63 characters (PostgreSQL limit)
- `BatchSize` range: 1-1000
- `Port` range: 1-65535

---

### 1.2 `appsettings.Development.json` (Development Overrides)
**Location**: `src/Industrial.Adam.Logger.WebApi/appsettings.Development.json`
**Hot-Reload**: ❌ Requires service restart
**Purpose**: Override settings for development environment

**Typical Usage**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Industrial.Adam.Logger.Core": "Trace"
    }
  }
}
```

**Note**: This file is optional and only loaded when `ASPNETCORE_ENVIRONMENT=Development`

---

### 1.3 `config/apikeys.json` (API Key Storage)
**Location**: `src/Industrial.Adam.Logger.WebApi/config/apikeys.json`
**Hot-Reload**: ✅ Auto-reload enabled (500ms debounce)
**Purpose**: API key authentication (file-based, no database required)

**Format**:
```json
{
  "keys": [
    {
      "id": "dev-service-1",                          // Required - Unique key ID
      "key": "IND-ADAM-DEV-2024-abc123def456ghi789",  // Required - Min 16 chars
      "name": "Development Service",                  // Required - Display name
      "expiresAt": "2026-01-01T00:00:00Z",           // Optional - Expiration (ISO 8601)
      "permissions": ["read", "write", "restart"]     // Optional - Future RBAC
    }
  ]
}
```

**Validation Rules**:
- `key` minimum 16 characters
- `id` must be unique
- Expired keys automatically ignored
- File must be in `config/` directory (path traversal protection)

**Security Features**:
- Constant-time comparison (prevents timing attacks)
- Hot-reload with FileSystemWatcher
- JSON validation on load
- Duplicate ID detection

**Example File**: `config/apikeys.example.json` (template for production)

---

### 1.4 `.env` Files (Environment Variables)
**Location**: `docker/.env` or `src/Industrial.Adam.Logger.WebApi/.env`
**Hot-Reload**: ❌ Requires service restart
**Purpose**: Secrets and environment-specific settings

**Template**: `docker/.env.template`
```bash
# TimescaleDB Configuration
TIMESCALEDB_PASSWORD=IndustrialLogger2024!@#$

# JWT Authentication (Future Use)
JWT_SECRET_KEY=your-256-bit-secret-key-change-this-in-production-min-32-chars
JWT_ISSUER=Industrial.Adam.Logger
JWT_AUDIENCE=Industrial.Adam.Logger.API

# Optional: CORS Configuration
# CORS_ALLOWED_ORIGINS=http://localhost:3000,https://your-domain.com
```

**WebApi Template**: `src/Industrial.Adam.Logger.WebApi/.env.template`
```bash
# API Configuration
ASPNETCORE_URLS=http://+:5000
ASPNETCORE_ENVIRONMENT=Development

# TimescaleDB Override (if different from docker)
# TIMESCALE_HOST=localhost
# TIMESCALE_PORT=5433
# TIMESCALE_DATABASE=adam_counters
# TIMESCALE_USERNAME=adam_user
# TIMESCALE_PASSWORD=your_secure_password

# Production Mode
DEMO_MODE=false

# Device Polling Defaults
DEFAULT_POLL_INTERVAL_MS=1000
DEFAULT_TIMEOUT_MS=3000
DEFAULT_MAX_RETRIES=3

# Logging
LOG_LEVEL=Information
LOG_FILE_RETENTION_DAYS=30

# CORS Override
# CORS_ORIGINS=http://localhost:3000,http://localhost:3001
```

**Security Note**: `.env` files should NEVER be committed to git. Always use `.env.template` as reference.

---

## 2. Configuration Hierarchy

Settings are loaded in this order (later overrides earlier):

1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific, e.g., Development, Production)
3. Environment variables (`.env` file or system environment)
4. Command-line arguments

**Example Override**:
- `appsettings.json` has `"Port": 5432`
- `.env` has `TIMESCALE_PORT=5433`
- Result: Port `5433` is used

---

## 3. Frontend Configuration Editor Requirements

### 3.1 File Operations Needed

#### Read Operations
- `GET /config/appsettings` - Read `appsettings.json`
- `GET /config/appsettings-dev` - Read `appsettings.Development.json`
- `GET /config/apikeys` - Read `config/apikeys.json` (with masked keys)
- `GET /config/env` - Read `.env` (with masked secrets)

#### Write Operations
- `PUT /config/appsettings` - Update `appsettings.json`
- `PUT /config/appsettings-dev` - Update `appsettings.Development.json`
- `PUT /config/apikeys` - Update `config/apikeys.json`
- `PUT /config/env` - Update `.env`

#### Validation Operations
- `POST /config/validate` - Validate JSON before saving
- `POST /config/backup` - Create backup before save
- `GET /config/backups` - List available backups
- `POST /config/restore/{backupId}` - Restore from backup

---

### 3.2 UI Requirements

#### appsettings.json Editor
- **Syntax-highlighted JSON editor** (Monaco Editor or CodeMirror)
- **Live validation** (JSON schema)
- **Section collapse/expand** (Logging, Devices, TimescaleDb)
- **Add/Edit/Delete devices** (form-based or JSON)
- **Add/Edit/Delete channels** (nested form)
- **Validation errors** highlighted inline
- **Save button** with confirmation dialog
- **Revert button** to undo changes
- **Download backup** before save

**Critical Warning**:
> ⚠️ Changes to `appsettings.json` require service restart. Confirm before saving.

#### API Keys Editor
- **Table view** of all keys (ID, Name, Expires, Permissions)
- **Add key** button → generates random key (min 16 chars)
- **Edit key** → update name, expiration, permissions
- **Delete key** → confirmation required
- **Show/Hide key** toggle (masked by default)
- **Hot-reload indicator** → "Changes apply automatically in ~500ms"

**No Restart Required**:
> ✅ API key changes apply automatically without restart (hot-reload enabled)

#### Environment Variables Editor
- **Key-value table** (editable)
- **Masked sensitive fields** (passwords, secrets) → show/hide toggle
- **Add variable** button
- **Delete variable** button
- **Template reference** → show `.env.template` side-by-side
- **Save button** with confirmation

**Critical Warning**:
> ⚠️ Changes to `.env` require service restart. Confirm before saving.

---

### 3.3 Validation Rules (Frontend Must Enforce)

#### Device Validation
- `DeviceId` - Required, alphanumeric + hyphens, max 50 chars
- `IpAddress` - Required, valid IP or hostname
- `Port` - Required, range 1-65535
- `UnitId` - Required, range 1-255
- `PollIntervalMs` - Required, range 100-300000 (0.1s to 5min)
- `TimeoutMs` - Required, must be > PollIntervalMs

#### Channel Validation
- `ChannelNumber` - Required, range 0-255, unique within device
- `StartRegister` - Required, range 0-65535
- `RegisterCount` - Required, 1 or 2
- `MinValue` < `MaxValue` (if both specified)

#### MQTT Validation
- `Topics` - Required, non-empty array
- `Format` - Required, one of: Json, Binary, Csv
- `DataType` - Required, one of: UInt32, Int16, UInt16, Float32, Float64
- `ChannelJsonPath` - Required if Format=Json
- `ValueJsonPath` - Required if Format=Json

#### TimescaleDB Validation
- `TableName` - Required, max 63 characters, alphanumeric + underscore
- `BatchSize` - Range 1-1000
- `Port` - Range 1-65535

#### API Key Validation
- `key` - Min 16 characters
- `id` - Required, alphanumeric + hyphens, unique
- `expiresAt` - Optional, ISO 8601 date, must be in future

---

### 3.4 Error Handling

#### Validation Errors
```json
{
  "valid": false,
  "errors": [
    {
      "field": "AdamLogger.Devices[0].Port",
      "message": "Port must be between 1 and 65535",
      "value": 70000
    }
  ]
}
```

#### Save Errors
```json
{
  "success": false,
  "error": "Cannot write to appsettings.json: Permission denied. Check file permissions (644 recommended)."
}
```

#### Permission Errors
- Show clear error if file not writable
- Suggest `chmod 644 appsettings.json` on Linux
- Suggest "Run as Administrator" on Windows

---

## 4. Production Deployment Checklist

Before deploying to production, technician must verify:

### appsettings.json
- [ ] Update all device IPs to production IPs
- [ ] Disable simulator devices (`Enabled: false`)
- [ ] Set production database credentials
- [ ] Configure CORS allowed origins (no wildcards)
- [ ] Set log level to `Information` (not Debug)

### API Keys
- [ ] Delete development API keys
- [ ] Generate new production API keys (min 32 chars recommended)
- [ ] Set expiration dates for temporary keys
- [ ] Document key assignments (which system uses which key)

### Environment Variables
- [ ] Generate secure `JWT_SECRET_KEY` (min 32 chars)
- [ ] Change default database password
- [ ] Update CORS origins for production domain
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Set `DEMO_MODE=false`

### File Permissions
- [ ] `appsettings.json` → 644 (readable by service)
- [ ] `config/apikeys.json` → 600 (service only)
- [ ] `.env` → 600 (service only, never commit)

---

## 5. Backup & Restore Strategy

### Automatic Backups (Frontend Should Implement)
- Create backup before every save
- Backup naming: `{filename}.backup.{timestamp}.json`
- Keep last 10 backups per file
- Store in `config/backups/` directory

### Manual Backups
- **Download button** → saves to technician's laptop
- **Upload button** → restore from laptop backup

### Disaster Recovery
If configuration is broken:
1. Stop service: `sudo systemctl stop adam-logger`
2. Restore from backup: `cp config/backups/appsettings.backup.{timestamp}.json appsettings.json`
3. Validate JSON: `cat appsettings.json | jq .`
4. Start service: `sudo systemctl start adam-logger`

---

## 6. Summary

### Configuration Files to Edit
1. ✅ `appsettings.json` - Main config (devices, database, CORS)
2. ✅ `config/apikeys.json` - API keys (hot-reload)
3. ✅ `.env` - Secrets and environment variables
4. ⚠️ `appsettings.Development.json` - Development overrides (optional)

### Hot-Reload Support
- ✅ API keys (`config/apikeys.json`) - Auto-reload in ~500ms
- ❌ All other files - Require service restart

### Frontend Must Provide
- JSON editor with syntax highlighting
- Live validation (JSON schema)
- Backup before save
- Restore from backup
- Clear warnings when restart required
- Form-based editors for common tasks (add device, add channel)
- Security (mask sensitive fields, confirm destructive actions)

**Key Principle**: Make it **impossible** for technician to break the system. Validate everything, backup everything, warn about everything.
