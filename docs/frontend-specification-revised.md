# Frontend Specification - Industrial ADAM Logger (Revised)

## Design Philosophy: Toyota + User Empathy

**Simple, Robust, Dependable** - with user workflow as priority.

**Core Principle**: Technician should NOT switch between 5 different tools (frontend, SSH, Grafana, MQTT Explorer, VSCode). Keep context in ONE application.

**Balance**:
- ✅ Use tools team knows (shadcn/ui, Tailwind) - developer velocity matters
- ✅ Forms for common operations (add device weekly) - user-friendly
- ✅ Inline troubleshooting - no context switch
- ❌ No duplicate features (historical charts → use Grafana)
- ❌ No over-engineering (full log viewer → use inline recent logs)

---

## Tech Stack (Balanced)

### Core
- **React 18** - UI framework
- **TypeScript** - Type safety
- **Vite** - Build tool (fast, modern)
- **Tailwind CSS** - Utility-first styling (team knows it)
- **shadcn/ui** - Component library (team knows it, looks professional)
- **React Query** - Server state management
- **React Router** - Client-side routing

### Utilities
- **Recharts** - Simple live charts (60-second only, not historical)
- **Axios** - HTTP client
- **date-fns** - Date formatting

### NOT Using
- ❌ Zod - Use native HTML5 validation + TypeScript
- ❌ Monaco/CodeMirror - Too heavy (10MB), use download/edit/upload
- ❌ WebSocket libraries - Polling is sufficient
- ❌ Complex state management (Redux, MobX) - React Query handles it

---

## Application Structure

### Two Main Pages

**1. Dashboard** (Primary - 90% of time spent here)
- Dense device table (all Modbus + MQTT devices)
- System health summary
- Critical alerts
- Inline troubleshooting (expand device row)

**2. Configuration** (Secondary - used during setup)
- Device management (add/edit via forms)
- API Keys table (hot-reload)
- Advanced config (download/upload JSON)

**No separate Logs page** - Recent logs shown inline when troubleshooting

---

## Page 1: Dashboard (Dense, All-in-One)

### Layout

```
┌────────────────────────────────────────────────────────────────────────────┐
│ Industrial ADAM Logger    Uptime: 3h 24m   DB: ● MQTT: ● DLQ: 0           │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│ CRITICAL ALERTS                                                            │
│ ⚠ LINE3-6051 offline for 5 minutes - Check network cable                  │
│                                                                            │
│ DEVICES (15)                                      [+ Add] [🔍 Scan] [⚙️]   │
│ ┌──┬──────────────┬──────┬─────────────────┬────────┬────────┬─────────┐ │
│ │✓│Device ID     │Status│IP:Port          │Ch0     │Ch1     │Actions  │ │
│ ├─┼──────────────┼──────┼─────────────────┼────────┼────────┼─────────┤ │
│ │☑│LINE1-6051    │ ●    │192.168.1.100:502│12,345  │234     │[↻][✎]  │ │
│ │☑│LINE2-6051    │ ●    │192.168.1.101:502│8,901   │156     │[↻][✎]  │ │
│ │☑│LINE3-6051    │ ○    │192.168.1.102:502│---     │---     │[↻][✎]  │▶│
│ │☑│PACK1-6051    │ ●    │192.168.1.103:502│45,678  │1,023   │[↻][✎]  │ │
│ │☑│TEMP-ZONE1    │ ●    │sensors/temp/zone1│25.5°C │---     │[✎]     │ │
│ └─┴──────────────┴──────┴─────────────────┴────────┴────────┴─────────┴─┘ │
│                                                                            │
│ Auto-refresh: ●ON (10s)   Last update: 3s ago   [Pause]                   │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

### Inline Troubleshooting Panel (Expanded Device)

**Triggered by**: Click ▶ arrow on offline device

```
│ ├──────────────┼──────┼─────────────────┼────────┼────────┼─────────┤     │
│ │LINE3-6051    │ ○    │192.168.1.102:502│---     │---     │[↻][✎]  │▼   │
│ ├──────────────┴──────┴─────────────────┴────────┴────────┴─────────┴────┤
│ │ TROUBLESHOOTING: LINE3-6051                                            │
│ │                                                                         │
│ │ Status: ○ Offline (5 minutes) | Last seen: 14:27:30                    │
│ │                                                                         │
│ │ Quick Tests:                                                            │
│ │ [🔍 Ping 192.168.1.102]  → ✅ Reachable (12ms)                          │
│ │ [🔍 Test Modbus Read]    → ❌ Connection timeout (3000ms)               │
│ │                                                                         │
│ │ Diagnosis: Ping works but Modbus fails → Firewall blocking port 502    │
│ │ Action: Check firewall rules for TCP port 502 to this device           │
│ │                                                                         │
│ │ Recent Logs (Last 10 for this device):                                 │
│ │ ┌─────────┬───────┬──────────────────────────────────────────────┐    │
│ │ │Time     │Level  │Message                                       │    │
│ │ ├─────────┼───────┼──────────────────────────────────────────────┤    │
│ │ │14:32:15 │ERROR  │Connection timeout (3000ms)                   │    │
│ │ │14:32:12 │WARN   │Retry 3 of 3 failed                           │    │
│ │ │14:32:09 │ERROR  │Modbus read failed                            │    │
│ │ └─────────┴───────┴──────────────────────────────────────────────┘    │
│ │                                                                         │
│ │ Live Data (Last 60 seconds):                                           │
│ │ ┌─────────────────────────────────────────────────────────────────┐   │
│ │ │ [No data - device offline]                                      │   │
│ │ └─────────────────────────────────────────────────────────────────┘   │
│ │                                                                         │
│ │ [Collapse] [Full Device Details]                                       │
│ └─────────────────────────────────────────────────────────────────────── │
```

**Key Features**:
- **No context switch** - Everything in one panel
- **Quick tests** - Ping + Modbus test (diagnose network vs. protocol)
- **Recent logs** - Last 10 entries for THIS device (not all devices)
- **Live chart** - 60-second view (verify counter working)
- **Contextual diagnosis** - "Ping works + Modbus fails = Firewall"

### Live Chart (When Device Online)

**Triggered by**: Expand online device row

```
│ │ Live Data (Last 60 seconds):                                           │
│ │ ┌─────────────────────────────────────────────────────────────────┐   │
│ │ │ Ch0: Main Counter                                               │   │
│ │ │                                                                 │   │
│ │ │ 12,400 ┤              ╭─────────────                           │   │
│ │ │        │            ╭─╯                                         │   │
│ │ │ 12,350 ┤         ╭──╯                                           │   │
│ │ │        │      ╭──╯                                              │   │
│ │ │ 12,300 ┼──────╯                                                 │   │
│ │ │        └─────────────────────────────────────────────────────→ │   │
│ │ │        60s ago                                           Now    │   │
│ │ │                                                                 │   │
│ │ │ ✓ Counter incrementing steadily (5.2 items/sec average)        │   │
│ │ └─────────────────────────────────────────────────────────────────┘   │
```

**Purpose**:
- Verify counter is working during commissioning
- NOT for historical analysis (use Grafana for that)

**Data source**: `/data/latest/{deviceId}` polled every 5 seconds

---

## Page 2: Configuration

### Layout

```
┌────────────────────────────────────────────────────────────────────────────┐
│ Configuration                                               [← Dashboard]  │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│ [Devices] [API Keys] [Advanced]                                            │
│                                                                            │
│ ──────────── Devices ────────────────────────────────────                 │
│                                                                            │
│ MODBUS DEVICES (10)                         [+ Add Modbus] [🔍 Scan LAN]  │
│ ┌──┬──────────────┬──────┬─────────────────┬──────┬────────┬──────────┐  │
│ │✓│Device ID     │Status│IP:Port          │Chans │Poll(ms)│Actions   │  │
│ ├─┼──────────────┼──────┼─────────────────┼──────┼────────┼──────────┤  │
│ │☑│LINE1-6051    │ ●    │192.168.1.100:502│2     │1000    │[✎][🗑]   │  │
│ │☑│LINE2-6051    │ ●    │192.168.1.101:502│2     │1000    │[✎][🗑]   │  │
│ └─┴──────────────┴──────┴─────────────────┴──────┴────────┴──────────┘  │
│                                                                            │
│ MQTT DEVICES (5)                                      [+ Add MQTT]         │
│ ┌──┬──────────────┬──────┬─────────────────────────┬────────┬──────────┐ │
│ │✓│Device ID     │Status│Topic                    │Format  │Actions   │ │
│ ├─┼──────────────┼──────┼─────────────────────────┼────────┼──────────┤ │
│ │☑│TEMP-ZONE1    │ ●    │sensors/temp/zone1       │JSON    │[✎][🗑]   │ │
│ └─┴──────────────┴──────┴─────────────────────────┴────────┴──────────┘ │
│                                                                            │
│ ⚠ Changes require service restart (~5 seconds downtime)                   │
│                                                                            │
│ [Save All Changes & Restart Service]                                      │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

### Add/Edit Modbus Device Form (Modal)

**Triggered by**: Click [+ Add Modbus] or [✎] on device

```
┌────────────────────────────────────────────────────────────────┐
│ Add Modbus Device                                  [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ BASIC INFORMATION                                              │
│                                                                │
│ Device ID *        [LINE4-6051__________]                     │
│                    ℹ️ Unique identifier                        │
│                                                                │
│ Display Name       [Line 4 Production Counter___________]     │
│                                                                │
│ Model Type         [ADAM-6051 ▼]  [Load Model Defaults]      │
│                    Options: 6050, 6051, 6052, 6017, 6024...   │
│                                                                │
│ NETWORK SETTINGS                                               │
│                                                                │
│ IP Address *       [192.168.1.104_]                           │
│ Port *             [502____]  (Default: 502)                  │
│ Unit ID *          [1______]  (Range: 1-255)                  │
│                                                                │
│ [🔍 Test Connection]  Status: ✅ Device responding (18ms)      │
│                                Model: ADAM-6051, FW: 1.2.3    │
│                                                                │
│ POLLING SETTINGS                                               │
│                                                                │
│ Poll Interval      [1000__] ms  (100-5000, Default: 1000)    │
│ Timeout            [3000__] ms  (Must be > Poll Interval)     │
│ Max Retries        [3_____]     (0-10, Default: 3)            │
│ [☑] TCP Keep-Alive (Recommended)                              │
│                                                                │
│ CHANNELS (Auto-filled for ADAM-6051)               [+ Add]    │
│ ┌──┬─────┬──────────────────┬─────┬─────┬────────┬──────┐   │
│ │✓│Ch # │Name              │Reg  │Cnt  │Type    │Unit  │   │
│ ├─┼─────┼──────────────────┼─────┼─────┼────────┼──────┤   │
│ │☑│0    │Channel 0         │0    │2    │UInt32  │counts│[✎]│
│ │☑│1    │Channel 1         │2    │2    │UInt32  │counts│[✎]│
│ └─┴─────┴──────────────────┴─────┴─────┴────────┴──────┴───┘   │
│                                                                │
│ [Cancel]  [Delete Device]                              [Save] │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Key Features**:
- **Model-based defaults** - Select ADAM-6051 → Auto-fills 2 channels
- **Test connection** - Verify before saving
- **Native HTML5 validation** - No Zod library needed
- **Simple channel config** - Most users just use defaults

### Network Scanner (Optional Tool)

**Triggered by**: Click [🔍 Scan LAN]

```
┌────────────────────────────────────────────────────────────────┐
│ Network Scanner                                    [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ Scan for Modbus TCP devices on local network                  │
│                                                                │
│ IP Range:  [192.168.1._] to [192.168.1.___]  Port: [502_]    │
│                                                                │
│ [▶ Start Scan]  [⏸ Pause]                                     │
│                                                                │
│ Progress: ████████░░░░░░░░ 45/255 (18%)  Elapsed: 12s         │
│                                                                │
│ DEVICES FOUND (3)                               [Add All (3)]  │
│ ┌──┬─────────────────┬──────────────┬──────────┬──────────┐   │
│ │✓│IP               │Model         │Response  │Status    │   │
│ ├─┼─────────────────┼──────────────┼──────────┼──────────┤   │
│ │☑│192.168.1.105    │ADAM-6051     │15ms      │Ready     │   │
│ │☑│192.168.1.106    │ADAM-6051     │18ms      │Ready     │   │
│ │☑│192.168.1.107    │ADAM-6017     │22ms      │Ready     │   │
│ └─┴─────────────────┴──────────────┴──────────┴──────────┘   │
│                                                                │
│ [Cancel]                          [Add Selected & Restart]    │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Purpose**: Find devices on network during commissioning
**User need**: "What ADAM devices are available?"
**Alternative removed**: MQTT Discovery (just document `mosquitto_sub -t '#'`)

### API Keys Tab

```
│ ──────────── API Keys ────────────────────────────────                    │
│                                                                            │
│ ✅ Hot-reload enabled - Changes apply automatically (no restart needed)    │
│                                                                            │
│ API KEYS (3)                                                [+ Add Key]    │
│ ┌──┬────────────┬──────────────────┬───────────────┬────────────┬──────┐ │
│ │✓│ID          │Name              │Key            │Expires     │Action│ │
│ ├─┼────────────┼──────────────────┼───────────────┼────────────┼──────┤ │
│ │☑│dev-srv-1   │Dev Service       │IND-...789     │Never       │[✎][🗑]│ │
│ │☑│prod-dash-1 │Dashboard         │IND-...456     │2026-01-01  │[✎][🗑]│ │
│ └─┴────────────┴──────────────────┴───────────────┴────────────┴──────┘ │
│                                                                            │
│ [Save]  (Changes apply automatically within 500ms)                         │
```

### Advanced Tab (Download/Upload)

```
│ ──────────── Advanced Configuration ──────────────────────                │
│                                                                            │
│ For advanced users: Database, MQTT Broker, System Settings                │
│                                                                            │
│ These settings are configured in appsettings.json and .env files.         │
│ Changes require service restart.                                          │
│                                                                            │
│ ┌────────────────────────────────────────────────────────────────────┐   │
│ │ CONFIGURATION FILES                                                │   │
│ │                                                                    │   │
│ │ Current Configuration:                                             │   │
│ │ - Database: localhost:5432 (adam_counters)                         │   │
│ │ - MQTT Broker: localhost:1883 (Connected)                          │   │
│ │ - Log Level: Information                                           │   │
│ │ - CORS: 2 origins configured                                       │   │
│ │                                                                    │   │
│ │ [📥 Download Config (JSON)]                                        │   │
│ │ [📤 Upload Config (JSON)]                                          │   │
│ │                                                                    │   │
│ │ Instructions:                                                      │   │
│ │ 1. Download current configuration as JSON file                     │   │
│ │ 2. Edit locally in your preferred editor (VSCode recommended)      │   │
│ │ 3. Upload modified configuration                                   │   │
│ │ 4. Service will validate and restart automatically                 │   │
│ │                                                                    │   │
│ └────────────────────────────────────────────────────────────────────┘   │
│                                                                            │
│ ⚠ For historical data visualization, use Grafana:                         │
│ [🔗 Open Grafana Dashboard]                                                │
│                                                                            │
│ ⚠ For detailed log analysis, download logs:                               │
│ [📥 Download Logs (Last 24h)]                                              │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

**Key Features**:
- **No inline forms** for database/MQTT broker (rare operations)
- **Download/upload workflow** - Edit in VSCode, upload
- **Links to external tools** - Grafana (charts), log files (analysis)
- **Simple** - Don't over-engineer rare operations

---

## Features Summary

### ✅ Included (User-Focused)

1. **Dense Dashboard** - All devices on one screen
2. **Inline Troubleshooting** - Expand device → See logs, tests, chart
3. **Test Connection Buttons** - Ping + Modbus test (fast diagnosis)
4. **Recent Logs (Per Device)** - Last 10 entries, inline (no separate page)
5. **60-Second Live Chart** - Verify counter working during commissioning
6. **Device Forms** - Add/edit via forms (common operation)
7. **Network Scanner** - Find devices on LAN
8. **API Keys Management** - Table editor (hot-reload)
9. **Auto-Refresh** - 10-second polling (balance between freshness and load)
10. **Critical Alerts** - Banner at top (impossible to miss)

### ❌ Excluded (Over-Engineering or Duplicates)

1. **Full Logs Tab** - Use inline recent logs + download for advanced analysis
2. **Historical Charts** - Use Grafana (link provided)
3. **MQTT Discovery Modal** - Document `mosquitto_sub` usage instead
4. **Database/Broker Config Forms** - Use download/upload JSON
5. **Monaco/CodeMirror Editor** - Too heavy, use download/edit/upload
6. **WebSocket Real-Time** - Polling is sufficient
7. **Zod Validation** - Use native HTML5 + TypeScript
8. **Complex Log Filtering** - Use external tools (journalctl, Grafana Loki)

---

## API Endpoints (Backend Requirements)

### Existing (Ready to Use)
- ✅ `GET /health`
- ✅ `GET /health/detailed`
- ✅ `GET /devices`
- ✅ `GET /devices/{id}`
- ✅ `POST /devices/{id}/restart`
- ✅ `GET /data/latest`
- ✅ `GET /data/latest/{deviceId}`
- ✅ `GET /data/stats`

### New (Need to Implement)

**Configuration Management**:
- `GET /config/devices` - Get device configs
- `PUT /config/devices` - Update device configs (triggers restart)
- `POST /config/devices` - Add new device
- `DELETE /config/devices/{id}` - Delete device
- `POST /config/test-connection` - Test device connectivity
- `GET /config/apikeys` - Get API keys
- `PUT /config/apikeys` - Update API keys (hot-reload)
- `GET /config/export` - Download full config as JSON
- `POST /config/import` - Upload config JSON (validate + apply)

**Troubleshooting Tools**:
- `POST /tools/ping` - Ping test for device IP
- `POST /tools/modbus-test` - Test Modbus read
- `POST /tools/network-scan` - Scan IP range for Modbus devices

**Logs**:
- `GET /logs/device/{deviceId}?limit=10` - Recent logs for specific device
- `GET /logs/download` - Download log file (last 24h)

**Total New Endpoints**: 13 (reduced from 23)

---

## File Structure

```
frontend/
├── public/
│   └── favicon.ico
├── src/
│   ├── components/
│   │   ├── ui/                      # shadcn components (Button, Table, Dialog, Input)
│   │   ├── dashboard/
│   │   │   ├── DashboardPage.tsx
│   │   │   ├── DeviceTable.tsx
│   │   │   ├── SystemHealth.tsx
│   │   │   ├── CriticalAlerts.tsx
│   │   │   ├── TroubleshootingPanel.tsx  # Inline panel
│   │   │   └── LiveChart.tsx             # 60-second chart
│   │   ├── configuration/
│   │   │   ├── ConfigurationPage.tsx
│   │   │   ├── DevicesTab.tsx
│   │   │   ├── ApiKeysTab.tsx
│   │   │   ├── AdvancedTab.tsx           # Download/upload
│   │   │   ├── AddModbusDeviceModal.tsx
│   │   │   ├── EditModbusDeviceModal.tsx
│   │   │   ├── AddMqttDeviceModal.tsx
│   │   │   └── NetworkScannerModal.tsx
│   │   └── common/
│   │       ├── Layout.tsx
│   │       ├── StatusIndicator.tsx       # ●/○/⚠
│   │       ├── ErrorBoundary.tsx
│   │       └── LoadingSpinner.tsx
│   ├── hooks/
│   │   ├── useDevices.ts                 # React Query hook
│   │   ├── useHealth.ts
│   │   ├── useConfig.ts
│   │   └── useDeviceLogs.ts              # Recent logs per device
│   ├── lib/
│   │   ├── api.ts                        # Axios instance + API functions
│   │   ├── types.ts                      # TypeScript types
│   │   └── utils.ts                      # Utility functions
│   ├── App.tsx
│   ├── main.tsx
│   └── index.css                         # Tailwind imports
├── docs/
│   ├── DEVELOPMENT-PLAN.md
│   └── PROGRESS.md
├── .gitignore
├── package.json
├── tsconfig.json
├── vite.config.ts
├── tailwind.config.js
└── README.md
```

---

## Development Timeline (5 Weeks)

### Week 1: Foundation
- Initialize Vite + React + TypeScript
- Configure Tailwind CSS
- Install shadcn/ui components
- Set up React Router (Dashboard, Configuration)
- Create API client (axios)
- Set up React Query
- Basic layout component

### Week 2: Dashboard
- Dense device table (Modbus + MQTT)
- System health summary
- Auto-refresh (10s polling)
- Critical alerts banner
- Status indicators (●/○/⚠)
- Inline actions ([↻][✎])

### Week 3: Inline Troubleshooting
- Expandable device panel
- Recent logs (last 10 for device)
- Test connection buttons (ping, Modbus)
- 60-second live chart (Recharts)
- Contextual diagnosis messages
- Restart device functionality

### Week 4: Configuration
- Add/Edit Modbus device forms
- Add/Edit MQTT device forms
- API Keys table editor
- Advanced tab (download/upload)
- Network scanner modal
- Form validation (native HTML5)

### Week 5: Polish & Testing
- Error boundaries
- Loading states
- Empty states
- Offline mode indicator
- Retry failed API calls
- Accessibility check
- Responsive design
- Testing
- Documentation

---

## Success Criteria

The frontend is production-ready when:

**Dashboard**:
- ✅ Technician sees all device statuses on ONE SCREEN
- ✅ Offline device clearly visible (○ grey, critical alert banner)
- ✅ Auto-refresh works (10s interval)
- ✅ Expand offline device → See diagnosis in < 5 seconds

**Troubleshooting**:
- ✅ Click device → Inline panel opens (no context switch)
- ✅ Recent logs show last 10 entries for THIS device
- ✅ Ping + Modbus test work with clear results
- ✅ Diagnosis message tells technician exactly what's wrong
- ✅ Live chart shows counter incrementing (verification)

**Configuration**:
- ✅ Add new device via form in < 2 minutes
- ✅ Test connection before saving (prevent invalid config)
- ✅ Network scanner finds devices on LAN
- ✅ API keys can be rotated without restart (hot-reload works)
- ✅ Advanced users can download/edit/upload JSON

**Performance**:
- ✅ Page loads in < 2 seconds
- ✅ Auto-refresh doesn't cause UI flicker
- ✅ Works on factory floor laptop with poor WiFi
- ✅ No console errors

**No separate tools needed for**:
- ✅ See device status (Dashboard shows all)
- ✅ Troubleshoot offline device (inline panel)
- ✅ Verify counter working (live chart)
- ✅ Add new device (forms)

**External tools still needed for** (and that's OK):
- Historical data analysis → Use Grafana
- Advanced log analysis → Use journalctl or Grafana Loki
- MQTT message debugging → Use mosquitto_sub or MQTT Explorer

---

## Key Principles Applied

1. **User workflow first** - One tool for commissioning/troubleshooting
2. **Team velocity** - Use tools team knows (shadcn, Tailwind)
3. **Forms for common operations** - Add device weekly = form is user-friendly
4. **Download/upload for rare operations** - DB config once = JSON is fine
5. **No duplicate features** - Charts = Grafana, Logs = journalctl
6. **Inline context** - Troubleshooting panel keeps context in one view
7. **Fast feedback** - Test buttons give instant results
8. **Simple validation** - Native HTML5 is sufficient

**This balances Toyota simplicity with real user needs.**
