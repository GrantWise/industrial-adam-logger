# Frontend Specification - Industrial ADAM Logger

## Design Philosophy: The Toyota/Lexus Principle

**Simple, Robust, Dependable** - Not over-engineered, sophisticated where needed, built for 24/7 industrial operations.

This is a **technician's tool**, not a consumer app. Prioritize:
- **Clarity over beauty** - Industrial grey/blue, high contrast, readable fonts
- **Functionality over aesthetics** - No animations unless they serve a purpose
- **Reliability over features** - Every feature must work 100% of the time
- **Speed over flair** - Fast load, instant feedback, no unnecessary round trips

## Tech Stack

- **React 18** - UI framework
- **shadcn/ui** - Component library (Tailwind-based)
- **Tailwind CSS** - Utility-first styling
- **React Query** - Server state management
- **React Router** - Client-side routing
- **Zod** - Schema validation
- **Recharts** - Time-series charts (simple, lightweight)

**NO**:
- Redux/MobX (overkill for this use case)
- Complex state machines
- Heavy animation libraries
- UI frameworks beyond shadcn

## User Persona

**Name**: Field Service Technician
**Environment**: Factory floor, industrial laptop, harsh lighting
**Tasks**:
- Install new ADAM modules (Modbus)
- Configure MQTT devices
- Troubleshoot connectivity issues
- Verify data flow to database
- Monitor system health during commissioning
- Edit configuration files (appsettings.json, .env)

**Pain Points**:
- Limited time on-site
- Needs quick diagnostics
- Must verify everything is working before leaving
- May have gloves on (large touch targets)
- Poor lighting conditions (high contrast needed)

## Core Functionality

### 1. Dashboard (Landing Page)

**Purpose**: At-a-glance system health and data flow status

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ Industrial ADAM Logger              [Health: ●]     │
├─────────────────────────────────────────────────────┤
│                                                     │
│  System Health                                      │
│  ┌────────────┬────────────┬────────────┐         │
│  │ Database   │ MQTT       │ Dead Letter│         │
│  │ ● Connected│ ● Connected│ Queue: 0   │         │
│  └────────────┴────────────┴────────────┘         │
│                                                     │
│  Modbus Devices (3)              [View All →]      │
│  ┌────────────────────────────────────────┐        │
│  │ ADAM-6051-01  ● Online  Last: 2s ago  │        │
│  │ ADAM-6051-02  ● Online  Last: 1s ago  │        │
│  │ ADAM-6051-03  ○ Offline Last: 5m ago  │        │
│  └────────────────────────────────────────┘        │
│                                                     │
│  MQTT Devices (5)                [View All →]      │
│  ┌────────────────────────────────────────┐        │
│  │ TEMP-01       ● Online  Last: 3s ago  │        │
│  │ COUNTER-01    ● Online  Last: 1s ago  │        │
│  │ PRESS-01      ● Online  Last: 2s ago  │        │
│  └────────────────────────────────────────┘        │
│                                                     │
│  Recent Events (Last 10)                           │
│  ┌────────────────────────────────────────┐        │
│  │ 14:32:15 ADAM-6051-03 disconnected     │        │
│  │ 14:30:45 MQTT device TEMP-01 online    │        │
│  │ 14:28:12 Database connection restored  │        │
│  └────────────────────────────────────────┘        │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Features**:
- System health indicators (Database, MQTT broker, DLQ count)
- Device counts (Modbus, MQTT)
- Top 3-5 devices with status and last seen time
- Recent events log (connection changes, errors)
- Large status indicators (●/○) - easy to see from distance

**Auto-refresh**: Every 5 seconds (configurable)

### 2. Modbus Devices Page

**Purpose**: View all Modbus devices, troubleshoot connectivity, verify data flow

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ Modbus Devices                     [+ Add Device]   │
├─────────────────────────────────────────────────────┤
│                                                     │
│  Filters: [Status: All ▼] [Search: ________]       │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │ ADAM-6051-01                      ● Online     │ │
│  │ IP: 192.168.1.100:502  Poll: 1000ms           │ │
│  │ Channels: 4  Last Reading: 2s ago             │ │
│  │                                                │ │
│  │ Channel 0: 12,345 counts (Good)   [Chart]     │ │
│  │ Channel 1: 8,901 counts (Good)    [Chart]     │ │
│  │ Channel 2: 0 counts (Uncertain)   [Chart]     │ │
│  │ Channel 3: 45,678 counts (Good)   [Chart]     │ │
│  │                                                │ │
│  │ [View Details] [Restart Connection] [Edit]    │ │
│  └───────────────────────────────────────────────┘ │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │ ADAM-6051-02                      ○ Offline    │ │
│  │ IP: 192.168.1.101:502  Poll: 1000ms           │ │
│  │ Error: Connection timeout                     │ │
│  │ Last seen: 5m ago                             │ │
│  │                                                │ │
│  │ [View Details] [Restart Connection] [Edit]    │ │
│  └───────────────────────────────────────────────┘ │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Features**:
- Device list with status, IP, poll interval
- Latest readings per channel with data quality
- Connection status and error messages
- Restart connection button
- Mini chart preview (click to expand)
- Add/Edit device (opens config editor)

**Troubleshooting Helpers**:
- Ping test button (verify network connectivity)
- Register read test (verify Modbus response)
- Connection diagnostics (show last error, retry count)

### 3. MQTT Devices Page

**Purpose**: View all MQTT devices, verify topic subscriptions, troubleshoot message flow

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ MQTT Devices                      [+ Add Device]    │
├─────────────────────────────────────────────────────┤
│                                                     │
│  Broker Status: ● Connected to localhost:1883      │
│  Messages/sec: 12.5  Total: 45,678                 │
│                                                     │
│  Filters: [Status: All ▼] [Search: ________]       │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │ TEMP-SENSOR-01                    ● Active     │ │
│  │ Topics: sensors/temperature                    │ │
│  │ Format: JSON  Type: Float32  Unit: °C         │ │
│  │ Last Message: 3s ago                          │ │
│  │                                                │ │
│  │ Latest Reading: 25.5°C (Good)     [Chart]     │ │
│  │ Message Rate: 0.33/sec (1 per 3s)             │ │
│  │                                                │ │
│  │ [View Details] [Test Subscription] [Edit]     │ │
│  └───────────────────────────────────────────────┘ │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │ COUNTER-01                        ● Active     │ │
│  │ Topics: counters/production                    │ │
│  │ Format: Binary  Type: UInt32                  │ │
│  │ Last Message: 1s ago                          │ │
│  │                                                │ │
│  │ Latest Reading: 1,234 (Good)      [Chart]     │ │
│  │ Message Rate: 1.0/sec                         │ │
│  │                                                │ │
│  │ [View Details] [Test Subscription] [Edit]     │ │
│  └───────────────────────────────────────────────┘ │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Features**:
- Broker connection status
- Device list with topics, format, data type
- Latest message time and reading
- Message rate (messages/sec)
- Test subscription button (shows raw MQTT messages)
- Add/Edit device (opens config editor)

**Troubleshooting Helpers**:
- MQTT message viewer (live tail of incoming messages)
- Topic pattern tester (test wildcard patterns)
- Payload preview (show last 5 raw payloads)

### 4. Device Details Modal

**Purpose**: Deep dive into single device - charts, diagnostics, raw data

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ ADAM-6051-01 Details                      [✕ Close] │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [Overview] [Charts] [Diagnostics] [Raw Data]      │
│                                                     │
│  Overview Tab:                                      │
│  ┌────────────────────────────────────────────┐    │
│  │ Status: ● Online                           │    │
│  │ IP Address: 192.168.1.100:502              │    │
│  │ Poll Interval: 1000ms                      │    │
│  │ Last Successful Read: 2s ago               │    │
│  │ Uptime: 3h 24m                             │    │
│  │ Total Readings: 12,340                     │    │
│  │ Failed Reads: 0                            │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Channels:                                          │
│  ┌────────────────────────────────────────────┐    │
│  │ Ch0: 12,345  Quality: Good  Type: UInt32   │    │
│  │ Ch1: 8,901   Quality: Good  Type: UInt32   │    │
│  │ Ch2: 0       Quality: Uncertain Type: UInt32│   │
│  │ Ch3: 45,678  Quality: Good  Type: UInt32   │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Actions:                                           │
│  [Restart Connection] [Ping Test] [Edit Config]    │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Charts Tab**:
- Time-series line chart (last 1h/6h/24h)
- One line per channel
- Zoom/pan controls
- Export to CSV button
- **CRITICAL**: Show data quality indicators (no synthetic data)

**Diagnostics Tab**:
- Connection attempts (success/fail counts)
- Average response time
- Last error message with timestamp
- Network diagnostics (ping, traceroute)
- Modbus register dump (raw hex values)

**Raw Data Tab**:
- Table of last 100 readings
- Columns: Timestamp, Channel, Value, Quality
- Export to CSV button
- Refresh button

### 5. Configuration Editor

**Purpose**: Edit configuration files directly from UI (appsettings.json, apikeys.json, .env)

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ Configuration Editor                                 │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [Main Config] [API Keys ✅] [Environment]          │
│                                                     │
│  Main Config (appsettings.json):                    │
│  ⚠️ Requires service restart after save              │
│                                                     │
│  ┌────────────────────────────────────────────┐    │
│  │ {                                          │    │
│  │   "AdamLogger": {                          │    │
│  │     "Devices": [                           │    │
│  │       {                                    │    │
│  │         "DeviceId": "ADAM-6051-01",        │    │
│  │         "IpAddress": "192.168.1.100",      │    │
│  │         "Port": 502,                       │    │
│  │         "PollIntervalMs": 1000,            │    │
│  │         ...                                │    │
│  │       }                                    │    │
│  │     ]                                      │    │
│  │   }                                        │    │
│  │ }                                          │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  ✓ Valid JSON  2 warnings                           │
│  - Device ADAM-6051-02: PollIntervalMs < 500ms     │
│  - TimeoutMs should be > PollIntervalMs            │
│                                                     │
│  [Validate] [Save & Restart] [Revert] [Backup]     │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**API Keys Tab** (Hot-Reload Enabled):
```
┌─────────────────────────────────────────────────────┐
│ API Keys (config/apikeys.json)                      │
│ ✅ Changes apply automatically (no restart needed)   │
├─────────────────────────────────────────────────────┤
│  [+ Add Key]                                        │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │ ID         │ Name          │ Expires │ Actions│  │
│  ├────────────┼───────────────┼─────────┼────────┤  │
│  │dev-srv-1   │Dev Service    │ Never   │✏️ 🗑️   │  │
│  │prod-dash-1 │Dashboard      │2026-01-01│✏️ 🗑️   │  │
│  │temp-tech-1 │Technician     │2025-10-05│✏️ 🗑️   │  │
│  └─────────────────────────────────────────────┘   │
│                                                     │
│  Edit Key: dev-srv-1                                │
│  ┌────────────────────────────────────────────┐    │
│  │ ID: dev-srv-1                              │    │
│  │ Name: [Dev Service                       ] │    │
│  │ Key: [IND-...789] [👁 Show] [🔄 Regenerate]│    │
│  │ Expires: [Never ▼]                         │    │
│  │ Permissions: [☑ read ☑ write ☑ restart]   │    │
│  │                                            │    │
│  │ [Save] [Cancel]                            │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Environment Variables Tab**:
```
┌─────────────────────────────────────────────────────┐
│ Environment Variables (.env)                         │
│ ⚠️ Requires service restart after save              │
├─────────────────────────────────────────────────────┤
│  [+ Add Variable]                                   │
│                                                     │
│  Database:                                          │
│  ┌────────────────────────────────────────────┐    │
│  │ TIMESCALEDB_PASSWORD  [••••••••] [👁]      │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Application:                                       │
│  ┌────────────────────────────────────────────┐    │
│  │ ASPNETCORE_URLS      [http://+:5000]       │    │
│  │ ASPNETCORE_ENVIRONMENT [Production ▼]      │    │
│  │ DEMO_MODE            [false ▼]             │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Logging:                                           │
│  ┌────────────────────────────────────────────┐    │
│  │ LOG_LEVEL            [Information ▼]       │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  [Save & Restart] [Revert] [Download]              │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Features**:
- **3 tabs**: Main Config, API Keys, Environment Variables
- **Syntax-highlighted JSON editor** for Main Config (Monaco Editor or CodeMirror)
- **Table-based editor** for API Keys (easier than raw JSON)
- **Key-value editor** for Environment Variables
- **Live validation** (JSON schema)
- **Hot-reload indicator** (✅ for API keys, ⚠️ for others)
- **Save button** with restart confirmation when needed
- **Revert button** (undo unsaved changes)
- **Download backup** (saves to laptop)
- **Validation errors** highlighted inline

**Files to Edit**:
1. `appsettings.json` - Devices, database, CORS (❌ restart required)
2. `config/apikeys.json` - API keys (✅ hot-reload enabled)
3. `.env` - Secrets, environment vars (❌ restart required)

**Safety Features**:
- Automatic backup before save (last 10 backups kept)
- Validation before save (prevent invalid JSON)
- Confirm dialog: "This requires service restart. Continue?"
- Clear warning if file not writable (permissions error)
- Mask sensitive fields (passwords shown as ••••••)
- Key regeneration generates cryptographically secure random key (32 chars)

### 6. System Health Page

**Purpose**: Detailed health checks for all system components

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ System Health                                        │
├─────────────────────────────────────────────────────┤
│                                                     │
│  Overall Status: ● Healthy                          │
│  Last Updated: 2s ago  [Refresh Now]               │
│                                                     │
│  Database (TimescaleDB)                             │
│  ┌────────────────────────────────────────────┐    │
│  │ Status: ● Connected                        │    │
│  │ Host: localhost:5432                       │    │
│  │ Latency: 5ms                               │    │
│  │ Total Rows: 1,234,567                      │    │
│  │ Disk Usage: 2.3 GB                         │    │
│  │ [Test Query] [View Schema]                 │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  MQTT Broker                                        │
│  ┌────────────────────────────────────────────┐    │
│  │ Status: ● Connected                        │    │
│  │ Host: localhost:1883                       │    │
│  │ Latency: 2ms                               │    │
│  │ Messages/sec: 12.5                         │    │
│  │ Total Messages: 45,678                     │    │
│  │ [Test Publish] [View Subscriptions]        │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Dead Letter Queue                                  │
│  ┌────────────────────────────────────────────┐    │
│  │ Status: ● OK                               │    │
│  │ Queue Size: 0                              │    │
│  │ Failed Writes: 0                           │    │
│  │ Retry Interval: 60s                        │    │
│  │ [View Queue] [Retry All]                   │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Logger Service                                     │
│  ┌────────────────────────────────────────────┐    │
│  │ Status: ● Running                          │    │
│  │ Uptime: 3h 24m                             │    │
│  │ Memory: 245 MB                             │    │
│  │ CPU: 2.5%                                  │    │
│  │ [Restart Service] [View Logs]              │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Features**:
- Health status for each component
- Connection details (host, port, latency)
- Performance metrics (messages/sec, query time)
- Test buttons (test query, test publish)
- Action buttons (restart, retry, view logs)

### 7. Logs Viewer

**Purpose**: View application logs for troubleshooting

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ Application Logs                                     │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [Level: All ▼] [Search: ________] [Auto-scroll ☑] │
│  Time Range: [Last 1h ▼]  [Export CSV]             │
│                                                     │
│  ┌────────────────────────────────────────────┐    │
│  │ 14:32:15 ERROR ModbusService               │    │
│  │ Connection timeout: ADAM-6051-03           │    │
│  │ IP: 192.168.1.102:502                      │    │
│  │                                            │    │
│  │ 14:30:45 INFO MqttService                  │    │
│  │ Device connected: TEMP-SENSOR-01           │    │
│  │ Topic: sensors/temperature                 │    │
│  │                                            │    │
│  │ 14:28:12 WARN TimescaleStorage             │    │
│  │ Database connection restored               │    │
│  │ Reconnected after 30s                      │    │
│  │                                            │    │
│  │ ...                                        │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  [Clear Logs] [Download Logs] [Refresh]            │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Features**:
- Filter by log level (Debug, Info, Warn, Error)
- Search by keyword
- Time range selector (last 1h, 6h, 24h, all)
- Auto-scroll (tail -f behavior)
- Export to CSV/JSON
- Color-coded log levels (Error=red, Warn=yellow, Info=blue)

### 8. Dead Letter Queue Page

**Purpose**: View and retry failed database writes

**Layout**:
```
┌─────────────────────────────────────────────────────┐
│ Dead Letter Queue                                    │
├─────────────────────────────────────────────────────┤
│                                                     │
│  Queue Size: 0                                      │
│  Auto-retry: Enabled (every 60s)                    │
│                                                     │
│  ┌────────────────────────────────────────────┐    │
│  │ No failed writes in queue                  │    │
│  │                                            │    │
│  │ Last successful retry: 2m ago              │    │
│  │ Total retries today: 5                     │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  [Retry All Now] [Clear Queue] [View History]      │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**When queue has items**:
```
│  Queue Size: 12  ⚠                                  │
│                                                     │
│  ┌────────────────────────────────────────────┐    │
│  │ Failed Write #1                            │    │
│  │ Device: ADAM-6051-01, Channel: 0           │    │
│  │ Value: 12,345  Time: 14:30:15              │    │
│  │ Error: Database connection timeout         │    │
│  │ Retry Count: 3  Next Retry: in 45s         │    │
│  │ [Retry Now] [Delete]                       │    │
│  └────────────────────────────────────────────┘    │
```

**Features**:
- Queue size and auto-retry status
- List of failed writes with details
- Retry count and next retry time
- Retry all button
- Individual retry/delete buttons
- History of retries (last 24h)

## Non-Functional Requirements

### Performance
- Page load: < 2 seconds
- API response rendering: < 500ms
- Auto-refresh without UI flicker
- Smooth charts (no lag when panning/zooming)

### Accessibility
- High contrast (WCAG AA minimum)
- Large touch targets (44x44px minimum)
- Keyboard navigation
- Screen reader friendly (for remote support)

### Reliability
- Graceful degradation if API offline (show cached data)
- Error boundaries (don't crash entire app on component error)
- Retry failed API calls (3 attempts with backoff)
- Offline mode (read-only, cached data)

### Security
- JWT authentication (same as backend)
- CORS configured correctly
- No sensitive data in localStorage (use sessionStorage)
- Auto-logout after inactivity (30 minutes)

### Browser Support
- Modern Chrome/Edge (primary)
- Firefox (secondary)
- Safari (if technicians use MacBooks)
- **No IE11 support** (not worth the effort)

## Visual Design Guidelines

### Color Palette (Industrial Theme)
- **Primary**: `#1e3a8a` (Industrial Blue)
- **Success**: `#16a34a` (Green - Online)
- **Warning**: `#ea580c` (Orange - Uncertain)
- **Error**: `#dc2626` (Red - Offline/Error)
- **Background**: `#f8fafc` (Light Grey)
- **Text**: `#0f172a` (Near Black)
- **Border**: `#cbd5e1` (Medium Grey)

### Typography
- **Headings**: Inter or system-ui, 600 weight
- **Body**: Inter or system-ui, 400 weight
- **Monospace**: JetBrains Mono or Consolas (for logs, JSON)
- **Sizes**: 14px body, 16px buttons, 18px headings

### Status Indicators
- ● Online/Good/Connected (Green `#16a34a`)
- ○ Offline/Disconnected (Grey `#64748b`)
- ⚠ Warning/Uncertain (Orange `#ea580c`)
- ✕ Error/Failed (Red `#dc2626`)

### Layout
- **Max width**: 1400px (centered)
- **Sidebar**: 240px (navigation)
- **Spacing**: 4/8/16/24px (Tailwind scale)
- **Border radius**: 4px (subtle, not rounded)
- **Shadows**: Minimal (only for modals/dropdowns)

### Components (shadcn/ui)
Use these shadcn components:
- Button (primary, secondary, destructive variants)
- Card (for device/status containers)
- Badge (for status indicators)
- Table (for logs, raw data)
- Dialog (for modals)
- Tabs (for multi-view pages)
- Input, Textarea (for config editor)
- Select, Switch (for filters, settings)

**Avoid**:
- Complex animations
- Fancy transitions
- Gradients
- 3D effects
- Particle effects
- Unnecessary icons

## API Endpoints (Backend)

The frontend will consume these REST API endpoints:

**Health**:
- `GET /health` - Basic health status
- `GET /health/detailed` - Detailed health (DB, MQTT, DLQ)

**Devices**:
- `GET /devices` - List all devices (Modbus + MQTT)
- `GET /devices/{id}` - Single device status
- `POST /devices/{id}/restart` - Restart device connection

**Data**:
- `GET /data/latest` - Latest readings (all devices)
- `GET /data/latest/{deviceId}` - Latest readings (single device)
- `GET /data/history/{deviceId}?from=&to=` - Time-series data for charts
- `GET /data/stats` - Data collection statistics

**MQTT**:
- `GET /mqtt/health` - MQTT broker connection and message stats
- `GET /mqtt/devices` - List configured MQTT devices
- `GET /mqtt/messages/live` - WebSocket for live MQTT message stream

**Configuration** (NEW endpoints needed):
- `GET /config/appsettings` - Get appsettings.json
- `PUT /config/appsettings` - Update appsettings.json
- `GET /config/env` - Get .env (masked secrets)
- `PUT /config/env` - Update .env

**Logs**:
- `GET /logs?level=&search=&from=&to=` - Query logs
- `GET /logs/live` - WebSocket for live log tail

**Dead Letter Queue**:
- `GET /dlq` - Get queue contents
- `POST /dlq/retry` - Retry all failed writes
- `POST /dlq/retry/{id}` - Retry single failed write
- `DELETE /dlq/{id}` - Delete failed write from queue

## File Structure

```
frontend/
├── public/
│   └── favicon.ico
├── src/
│   ├── components/
│   │   ├── ui/                    # shadcn components
│   │   ├── Dashboard.tsx          # Dashboard page
│   │   ├── ModbusDevices.tsx      # Modbus devices page
│   │   ├── MqttDevices.tsx        # MQTT devices page
│   │   ├── DeviceDetails.tsx      # Device details modal
│   │   ├── ConfigEditor.tsx       # Config editor page
│   │   ├── SystemHealth.tsx       # System health page
│   │   ├── LogsViewer.tsx         # Logs viewer page
│   │   ├── DeadLetterQueue.tsx    # DLQ page
│   │   ├── DeviceCard.tsx         # Reusable device card
│   │   ├── StatusIndicator.tsx    # Status dot (●/○/⚠/✕)
│   │   ├── TimeSeriesChart.tsx    # Recharts wrapper
│   │   └── Layout.tsx             # Main layout with nav
│   ├── hooks/
│   │   ├── useDevices.ts          # React Query hook for devices
│   │   ├── useHealth.ts           # React Query hook for health
│   │   ├── useLogs.ts             # React Query hook for logs
│   │   └── useWebSocket.ts        # WebSocket hook for live data
│   ├── lib/
│   │   ├── api.ts                 # Axios instance + API functions
│   │   ├── utils.ts               # Utility functions
│   │   └── schemas.ts             # Zod schemas for validation
│   ├── App.tsx                    # Main app component
│   ├── main.tsx                   # Entry point
│   └── index.css                  # Tailwind imports
├── package.json
├── tsconfig.json
├── vite.config.ts                 # Vite build config
└── tailwind.config.js             # Tailwind config
```

## Development Plan

### Phase 1: Foundation (Week 1)
- [ ] Initialize Vite + React + TypeScript project
- [ ] Install shadcn/ui, Tailwind, React Query
- [ ] Set up API client (axios instance with JWT)
- [ ] Create Layout component with navigation
- [ ] Implement basic routing (React Router)

### Phase 2: Core Pages (Week 2)
- [ ] Dashboard page (system health summary)
- [ ] Modbus Devices page (device list)
- [ ] MQTT Devices page (device list)
- [ ] System Health page (detailed health checks)

### Phase 3: Details & Charts (Week 3)
- [ ] Device Details modal (charts, diagnostics)
- [ ] Time-series charts (Recharts integration)
- [ ] Logs Viewer page (filterable logs)
- [ ] Dead Letter Queue page

### Phase 4: Configuration (Week 4)
- [ ] Config Editor page (appsettings.json, .env)
- [ ] JSON validation (Zod schemas)
- [ ] File save/revert/backup functionality

### Phase 5: Polish & Testing (Week 5)
- [ ] Error boundaries
- [ ] Loading states
- [ ] Empty states
- [ ] Responsive design (mobile/tablet)
- [ ] Accessibility audit
- [ ] End-to-end testing (Playwright)

## Success Criteria

The frontend is **production-ready** when:
- ✅ Technician can commission new ADAM device in < 5 minutes
- ✅ Technician can diagnose connectivity issue in < 2 minutes
- ✅ All device statuses visible at a glance (Dashboard)
- ✅ Configuration changes work without service restart
- ✅ Charts load in < 1 second
- ✅ Works on factory floor laptop with poor WiFi
- ✅ No critical accessibility violations (WCAG AA)
- ✅ Zero runtime errors in production (error boundaries catch all)

## What This Frontend Is NOT

**Not a consumer app** - No fancy animations, gradients, or "delightful" interactions
**Not a data analytics platform** - No complex dashboards, pivot tables, or BI features
**Not a CMS** - No drag-and-drop, WYSIWYG, or visual builders
**Not a real-time monitoring system** - No alarm management, notification center, or alerting (that's a separate system)

This is a **field service tool** for **technicians** to **commission**, **troubleshoot**, and **verify** industrial data loggers.

**Simple. Robust. Dependable.**
