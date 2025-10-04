# Configuration Tab - Form-Based Design

## Philosophy: Forms Only, No Raw JSON

**Why no JSON editor:**
- ❌ Easy to make syntax errors (missing comma, bracket)
- ❌ No validation until save (break production system)
- ❌ Intimidating for non-programmers
- ❌ Hard to see structure (nested objects, arrays)
- ❌ Copy/paste errors common

**Why forms:**
- ✅ Impossible to create invalid JSON
- ✅ Validation on every field (immediate feedback)
- ✅ Clear labels and help text
- ✅ Dropdowns for enums (no typos)
- ✅ Type checking (number fields only accept numbers)

**Exception**: Advanced users can download JSON, edit locally, upload. But UI is form-based.

---

## Configuration Tab Layout

```
┌────────────────────────────────────────────────────────────────────────────┐
│ [Dashboard] [Configuration] [Logs]                                         │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│ CONFIGURATION                                                              │
│                                                                            │
│ [Devices] [Database] [MQTT Broker] [API Keys] [System]                    │
│                                                                            │
│ ──────────────────── Devices ──────────────────────────                   │
│                                                                            │
│ MODBUS DEVICES (10)                              [+ Add Device] [Import]  │
│ ┌──┬──────────────┬──────┬─────────────────┬──────┬────────┬──────────┐  │
│ │✓│Device ID     │Status│IP:Port          │Chans │Poll(ms)│Actions   │  │
│ ├─┼──────────────┼──────┼─────────────────┼──────┼────────┼──────────┤  │
│ │☑│LINE1-6051    │ ●    │192.168.1.100:502│2     │1000    │[✎][🗑][⇅]│  │
│ │☑│LINE2-6051    │ ●    │192.168.1.101:502│2     │1000    │[✎][🗑][⇅]│  │
│ │☑│LINE3-6051    │ ○    │192.168.1.102:502│2     │1000    │[✎][🗑][⇅]│  │
│ │☑│PACK1-6051    │ ●    │192.168.1.103:502│2     │1000    │[✎][🗑][⇅]│  │
│ │☑│PACK2-6051    │ ●    │192.168.1.104:502│2     │1000    │[✎][🗑][⇅]│  │
│ │☑│WELD1-6051    │ ●    │192.168.1.105:502│2     │1000    │[✎][🗑][⇅]│  │
│ │☑│TEMP1-6017    │ ●    │192.168.1.200:502│2     │1000    │[✎][🗑][⇅]│  │
│ │☑│TEMP2-6017    │ ●    │192.168.1.201:502│2     │1000    │[✎][🗑][⇅]│  │
│ │☑│PRESS1-6024   │ ●    │192.168.1.210:502│1     │1000    │[✎][🗑][⇅]│  │
│ │☑│FLOW1-6015    │ ●    │192.168.1.220:502│1     │1000    │[✎][🗑][⇅]│  │
│ └─┴──────────────┴──────┴─────────────────┴──────┴────────┴──────────┘  │
│                                                                            │
│ Bulk Actions:  [Enable Selected] [Disable Selected] [Delete Selected]     │
│                [Export Selected] [Duplicate Selected]                      │
│                                                                            │
│ MQTT DEVICES (5)                                 [+ Add Device] [Import]   │
│ ┌──┬──────────────┬──────┬─────────────────────────┬────────┬──────────┐ │
│ │✓│Device ID     │Status│Topic                    │Format  │Actions   │ │
│ ├─┼──────────────┼──────┼─────────────────────────┼────────┼──────────┤ │
│ │☑│TEMP-ZONE1    │ ●    │sensors/temp/zone1       │JSON    │[✎][🗑]   │ │
│ │☑│TEMP-ZONE2    │ ●    │sensors/temp/zone2       │JSON    │[✎][🗑]   │ │
│ │☑│COUNTER-PROD  │ ●    │counters/production      │Binary  │[✎][🗑]   │ │
│ │☑│HUMID-MAIN    │ ●    │sensors/humidity         │JSON    │[✎][🗑]   │ │
│ │☑│PRESS-LINE1   │ ○    │sensors/pressure/line1   │CSV     │[✎][🗑]   │ │
│ └─┴──────────────┴──────┴─────────────────────────┴────────┴──────────┘ │
│                                                                            │
│ ⚠ Changes require service restart (~5 seconds downtime)                   │
│                                                                            │
│ [📥 Download Config] [📤 Upload Config] [🔄 Restore Backup]                │
│                                                  [Save & Restart Service]  │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

**Key Features:**
- ✅ **Table view** - All devices visible, no scrolling through JSON
- ✅ **Inline editing** - Click [✎] to edit in form
- ✅ **Reordering** - [⇅] drag to reorder devices
- ✅ **Bulk operations** - Select multiple, enable/disable/delete
- ✅ **Import/Export** - For bulk operations, but hidden from main workflow

---

## Edit Device Form (Modal)

**Triggered by**: Click [✎] on device row

```
┌────────────────────────────────────────────────────────────────┐
│ Edit Device: LINE1-6051                            [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ BASIC INFORMATION                                              │
│                                                                │
│ Device ID *        [LINE1-6051__________]  (Read-only)        │
│ Display Name       [Line 1 Production Counter___________]     │
│ Model Type         [ADAM-6051 ▼]                              │
│ Enabled            [☑] Device enabled                         │
│                                                                │
│ NETWORK SETTINGS                                               │
│                                                                │
│ IP Address *       [192.168.1.100_] [🔍 Test Connection]      │
│ Port *             [502____]  (Default: 502)                  │
│ Unit ID *          [1______]  (Default: 1, Range: 1-255)      │
│                                                                │
│ Connection Test:   ✅ Device responding (15ms)                 │
│                    Model: ADAM-6051, Firmware: 1.2.3          │
│                                                                │
│ POLLING SETTINGS                                               │
│                                                                │
│ Poll Interval      [1000__] ms  (Range: 100-5000)             │
│ Timeout            [3000__] ms  (Must be > Poll Interval)     │
│ Max Retries        [3_____]     (Range: 0-10)                 │
│ TCP Keep-Alive     [☑] Enabled (Recommended)                  │
│                                                                │
│ CHANNELS (2)                                    [+ Add Channel]│
│ ┌──┬─────┬──────────────────┬─────┬─────┬────────┬──────┐   │
│ │✓│Ch # │Name              │Reg  │Cnt  │Type    │Unit  │   │
│ ├─┼─────┼──────────────────┼─────┼─────┼────────┼──────┤   │
│ │☑│0    │Main Counter      │0    │2    │UInt32  │items │[✎]│
│ │☑│1    │Reject Counter    │2    │2    │UInt32  │items │[✎]│
│ └─┴─────┴──────────────────┴─────┴─────┴────────┴──────┴───┘   │
│                                                                │
│ [Delete Device]                                        [Save]  │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Field Validation (Real-Time):**
- Device ID: Required, alphanumeric + hyphens only
- IP Address: Valid IP or hostname
- Port: Number, 1-65535
- Unit ID: Number, 1-255
- Poll Interval: Number, 100-5000
- Timeout: Number, must be > Poll Interval
- Max Retries: Number, 0-10

**Test Connection Button:**
- Sends Modbus read request
- Shows success/failure instantly
- Displays device info if successful
- Shows error message if failed

---

## Edit Channel Form (Nested Modal)

**Triggered by**: Click [✎] on channel row, or [+ Add Channel]

```
┌────────────────────────────────────────────────────────────────┐
│ Edit Channel: LINE1-6051 - Channel 0               [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ CHANNEL INFORMATION                                            │
│                                                                │
│ Channel Number *   [0__]  (Range: 0-255)                      │
│ Name               [Main Product Counter___________]          │
│ Enabled            [☑] Channel enabled                        │
│                                                                │
│ MODBUS REGISTER MAPPING                                        │
│                                                                │
│ Start Register *   [0____]  (Range: 0-65535)                  │
│ Register Count *   [2 ▼]    (1 or 2)                          │
│ Register Type      [Holding Register ▼]                       │
│                    Options: Holding Register, Input Register  │
│ Data Type          [UInt32 Counter ▼]                         │
│                    Options: UInt32 Counter, Int16, UInt16,    │
│                             Float32, Int32                    │
│                                                                │
│ DATA PROCESSING                                                │
│                                                                │
│ Scale Factor       [1.0_____]  (Multiply raw value)           │
│ Offset             [0.0_____]  (Add after scaling)            │
│ Unit               [items____]  (Display unit)                │
│                                                                │
│ VALIDATION (Optional)                                          │
│                                                                │
│ Min Value          [0________]  (Alert if below)              │
│ Max Value          [4294967295]  (Alert if above)             │
│ Max Change Rate    [1000_____]  (per second)                  │
│ Rate Window        [60_______]  (seconds, for rate calc)      │
│                                                                │
│ TAGS (Optional)                                                │
│ ┌────────────┬─────────────────────────────────────────┐     │
│ │ Key        │ Value                                   │     │
│ ├────────────┼─────────────────────────────────────────┤     │
│ │ location   │ [production_floor_1__________]          │     │
│ │ line       │ [line_1______________________]          │     │
│ └────────────┴─────────────────────────────────────────┘     │
│ [+ Add Tag]                                                    │
│                                                                │
│ [Cancel]                                               [Save]  │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Field Validation:**
- Channel Number: Required, 0-255, unique within device
- Start Register: Required, 0-65535
- Register Count: Required, 1 or 2 (dropdown)
- Register Type: Dropdown (Holding/Input)
- Data Type: Dropdown (UInt32/Int16/UInt16/Float32/Int32)
- Scale/Offset: Numbers only
- Min/Max Value: Numbers, Min < Max

---

## Database Configuration Tab

**Triggered by**: Click [Database] tab

```
┌────────────────────────────────────────────────────────────────────────────┐
│ [Dashboard] [Configuration] [Logs]                                         │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│ CONFIGURATION                                                              │
│                                                                            │
│ [Devices] [Database] [MQTT Broker] [API Keys] [System]                    │
│                                                                            │
│ ──────────────────── Database (TimescaleDB) ────────────────────────       │
│                                                                            │
│ CONNECTION SETTINGS                                                        │
│                                                                            │
│ Host *             [localhost_________________]                            │
│ Port *             [5432____]  (Default: 5432)                             │
│ Database Name *    [adam_counters______________]                           │
│ Username *         [logger_service_____________]                           │
│ Password *         [••••••••••] [👁 Show]                                  │
│ Enable SSL         [☐] Use encrypted connection                           │
│                                                                            │
│ [🔍 Test Connection]  Status: ✅ Connected successfully (8ms)              │
│                                                                            │
│ PERFORMANCE SETTINGS                                                       │
│                                                                            │
│ Batch Size         [100___]  (Range: 1-1000, Default: 100)                │
│                    ℹ️ Number of readings to batch before write             │
│                                                                            │
│ Batch Timeout      [5000__]  ms  (Default: 5000)                          │
│                    ℹ️ Max wait time before forcing write                   │
│                                                                            │
│ Flush Interval     [5000__]  ms  (Default: 5000)                          │
│                    ℹ️ Background flush frequency                           │
│                                                                            │
│ Query Timeout      [30____]  seconds  (Default: 30)                       │
│                                                                            │
│ CONNECTION POOLING                                                         │
│                                                                            │
│ Min Pool Size      [5_____]  connections (Default: 5)                     │
│ Max Pool Size      [20____]  connections (Default: 20)                    │
│                                                                            │
│ STORAGE SETTINGS                                                           │
│                                                                            │
│ Table Name *       [counter_data_______________]                           │
│                    ℹ️ Max 63 characters (PostgreSQL limit)                 │
│                                                                            │
│ Global Tags        [+ Add Tag]                                             │
│ ┌────────────┬─────────────────────────────────────────┐                 │
│ │ Key        │ Value                                   │                 │
│ ├────────────┼─────────────────────────────────────────┤                 │
│ │ location   │ factory_floor                           │                 │
│ │ environment│ production                              │                 │
│ └────────────┴─────────────────────────────────────────┘                 │
│                                                                            │
│ ⚠ Changes require service restart (~5 seconds downtime)                   │
│                                                                            │
│ [Reset to Defaults]                             [Save & Restart Service]  │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

**Field Validation:**
- Host: Required, valid hostname or IP
- Port: Number, 1-65535
- Database/Username/Password: Required
- Batch Size: Number, 1-1000
- Batch Timeout: Number, > 0
- Table Name: Required, max 63 chars, alphanumeric + underscore only
- Pool sizes: Numbers, Min < Max

**Test Connection Button:**
- Attempts database connection
- Shows success with latency
- Shows detailed error if failed (connection refused, auth failed, etc.)

---

## MQTT Broker Configuration Tab

```
┌────────────────────────────────────────────────────────────────────────────┐
│ [Devices] [Database] [MQTT Broker] [API Keys] [System]                    │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│ ──────────────────── MQTT Broker ────────────────────────────              │
│                                                                            │
│ BROKER CONNECTION                                                          │
│                                                                            │
│ Broker Host *      [localhost_________________]                            │
│ Broker Port *      [1883____]  (Default: 1883)                             │
│ Client ID          [industrial-logger__________]                           │
│                    ℹ️ Unique identifier for this client                    │
│                                                                            │
│ Username           [logger_____________________]  (Optional)               │
│ Password           [••••••••••] [👁 Show]         (Optional)               │
│                                                                            │
│ [🔍 Test Connection]  Status: ✅ Connected to Mosquitto 2.0.18 (12ms)      │
│                                                                            │
│ MQTT SETTINGS                                                              │
│                                                                            │
│ QoS Level          [1 (At Least Once) ▼]                                  │
│                    Options: 0 (At Most Once), 1 (At Least Once),          │
│                             2 (Exactly Once)                               │
│                                                                            │
│ Keep-Alive Period  [60____]  seconds  (Default: 60)                       │
│                                                                            │
│ Clean Session      [Auto ▼]                                               │
│                    Options: Auto, True, False                              │
│                                                                            │
│ RECONNECTION                                                               │
│                                                                            │
│ Auto-Reconnect     [☑] Enabled                                            │
│ Reconnect Delay    [5_____]  seconds  (Default: 5)                        │
│ Max Reconnect      [0_____]  (0 = infinite)                               │
│                                                                            │
│ SECURITY (TLS)                                                             │
│                                                                            │
│ Use TLS            [☐] Enable encrypted connection                        │
│ Allow Invalid Cert [☐] Skip certificate validation (NOT RECOMMENDED)      │
│                                                                            │
│ ⚠ Changes require service restart (~5 seconds downtime)                   │
│                                                                            │
│ [Reset to Defaults]                             [Save & Restart Service]  │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## API Keys Configuration Tab

```
┌────────────────────────────────────────────────────────────────────────────┐
│ [Devices] [Database] [MQTT Broker] [API Keys] [System]                    │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│ ──────────────────── API Keys ──────────────────────────                  │
│                                                                            │
│ ✅ Hot-reload enabled - Changes apply automatically (no restart needed)    │
│                                                                            │
│ API KEYS (3)                                                [+ Add Key]    │
│ ┌──┬────────────┬──────────────────┬───────────────┬────────────┬──────┐ │
│ │✓│ID          │Name              │Key            │Expires     │Action│ │
│ ├─┼────────────┼──────────────────┼───────────────┼────────────┼──────┤ │
│ │☑│dev-srv-1   │Dev Service       │IND-...789     │Never       │[✎][🗑]│ │
│ │☑│prod-dash-1 │Dashboard         │IND-...456     │2026-01-01  │[✎][🗑]│ │
│ │☑│temp-tech-1 │Technician (Temp) │IND-...123     │2025-10-05  │[✎][🗑]│ │
│ └─┴────────────┴──────────────────┴───────────────┴────────────┴──────┘ │
│                                                                            │
│ ℹ️ Keys shown truncated for security. Full key visible when editing.      │
│                                                                            │
│ [Save]  (Changes apply automatically within 500ms)                         │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

**Edit API Key Form (Modal):**

```
┌────────────────────────────────────────────────────────────────┐
│ Edit API Key: dev-srv-1                            [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ KEY INFORMATION                                                │
│                                                                │
│ ID *               [dev-srv-1__________]  (Read-only)         │
│ Name *             [Dev Service___________________]           │
│                                                                │
│ API Key *          [IND-ADAM-DEV-2024-abc123def456ghi789]     │
│                    [👁 Show] [🔄 Regenerate]                   │
│                    ℹ️ Minimum 16 characters                    │
│                                                                │
│ EXPIRATION                                                     │
│                                                                │
│ Expires            [○ Never  ◉ On Date]                       │
│                    [2026-01-01________] (YYYY-MM-DD)          │
│                                                                │
│ PERMISSIONS (Future Use)                                       │
│                                                                │
│ [☑] read      Read device data and status                    │
│ [☑] write     Modify device settings                         │
│ [☑] restart   Restart devices and services                   │
│                                                                │
│ [Delete Key]                                           [Save]  │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Regenerate Button:**
- Generates new cryptographically secure key (32 characters)
- Shows confirmation dialog before replacing
- Old key immediately invalidated

---

## System Settings Tab

```
┌────────────────────────────────────────────────────────────────────────────┐
│ [Devices] [Database] [MQTT Broker] [API Keys] [System]                    │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│ ──────────────────── System Settings ────────────────────────              │
│                                                                            │
│ LOGGING                                                                    │
│                                                                            │
│ Log Level          [Information ▼]                                        │
│                    Options: Trace, Debug, Information, Warning,           │
│                             Error, Critical                                │
│                                                                            │
│ CORS (Cross-Origin Resource Sharing)                                      │
│                                                                            │
│ Allowed Origins    [+ Add Origin]                                         │
│ ┌──────────────────────────────────────────────────────┐                 │
│ │ http://localhost:3000                                │ [🗑]            │
│ │ http://localhost:5173                                │ [🗑]            │
│ │ https://dashboard.example.com                        │ [🗑]            │
│ └──────────────────────────────────────────────────────┘                 │
│                                                                            │
│ ENVIRONMENT VARIABLES                                                      │
│                                                                            │
│ Environment        [Production ▼]                                         │
│                    Options: Development, Staging, Production              │
│                                                                            │
│ Demo Mode          [☐] Enable demo/simulation mode                        │
│                                                                            │
│ BACKUPS                                                                    │
│                                                                            │
│ Auto-Backup        [☑] Create backup before every save                    │
│ Backup Retention   [10____]  (Number of backups to keep)                  │
│                                                                            │
│ BACKUP HISTORY                                        [View All Backups]  │
│ ┌────────────────────┬──────────┬──────────────────────────────────┐     │
│ │ Timestamp          │ Size     │ Actions                          │     │
│ ├────────────────────┼──────────┼──────────────────────────────────┤     │
│ │ 2025-10-04 14:30   │ 45 KB    │ [📥 Download] [🔄 Restore]       │     │
│ │ 2025-10-04 12:15   │ 43 KB    │ [📥 Download] [🔄 Restore]       │     │
│ │ 2025-10-04 09:45   │ 42 KB    │ [📥 Download] [🔄 Restore]       │     │
│ └────────────────────┴──────────┴──────────────────────────────────┘     │
│                                                                            │
│ ⚠ Changes require service restart (~5 seconds downtime)                   │
│                                                                            │
│ [Reset to Defaults]                             [Save & Restart Service]  │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Import/Export Functions

**Triggered by**: [Import] or [Export Selected] buttons

### Export Dialog:

```
┌────────────────────────────────────────────────────────────────┐
│ Export Configuration                               [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ EXPORT OPTIONS                                                 │
│                                                                │
│ What to export:                                                │
│ [☑] Modbus Devices (10 selected)                              │
│ [☑] MQTT Devices (5 selected)                                 │
│ [☑] Database Settings                                         │
│ [☑] MQTT Broker Settings                                      │
│ [☐] API Keys (Security: Not recommended)                      │
│ [☐] System Settings                                           │
│                                                                │
│ Format:                                                        │
│ [◉ JSON (appsettings.json)   ○ CSV (spreadsheet)]            │
│                                                                │
│ [Cancel]                                  [📥 Download File]   │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

### Import Dialog:

```
┌────────────────────────────────────────────────────────────────┐
│ Import Configuration                               [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ SELECT FILE                                                    │
│                                                                │
│ ┌────────────────────────────────────────────────────────┐   │
│ │                                                        │   │
│ │         [📁 Click to select file or drag & drop]       │   │
│ │                                                        │   │
│ │         Supported: JSON, CSV                           │   │
│ │                                                        │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                                │
│ IMPORT OPTIONS                                                 │
│                                                                │
│ Merge strategy:                                                │
│ [◉ Add new devices (keep existing)                            │
│  ○ Replace all (delete existing)                              │
│  ○ Update matching IDs (merge)]                               │
│                                                                │
│ [☑] Validate before import                                    │
│ [☑] Create backup before import                               │
│                                                                │
│ [Cancel]                                           [Import]    │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**After file selected, shows preview:**

```
│ PREVIEW (10 devices found)                                     │
│ ┌──────────────┬─────────────────┬──────────────────────┐     │
│ │ Device ID    │ IP:Port         │ Status               │     │
│ ├──────────────┼─────────────────┼──────────────────────┤     │
│ │ LINE1-6051   │192.168.1.100:502│ ✓ Valid, will add    │     │
│ │ LINE2-6051   │192.168.1.101:502│ ✓ Valid, will add    │     │
│ │ BAD-DEVICE   │invalid-ip:502   │ ✗ Invalid IP address │     │
│ └──────────────┴─────────────────┴──────────────────────┘     │
│                                                                │
│ Summary: 9 valid, 1 error                                      │
│                                                                │
│ [Fix Errors]  [Cancel]                      [Import 9 Devices] │
```

---

## Key Design Decisions

### 1. **No Raw JSON Editor in UI**
- Forms prevent syntax errors
- Validation happens on every field
- Impossible to save invalid config

### 2. **But Allow Import/Export**
- Power users can download JSON
- Edit in VSCode/Notepad++ locally
- Upload back when done
- Best of both worlds

### 3. **Validation Before Save**
- Test connection buttons
- Field-level validation (red border on error)
- Preview import before applying
- Can't save invalid config

### 4. **Clear Restart Warnings**
- Every tab shows if restart required
- Save button always says "Save & Restart Service"
- No surprises

### 5. **Hot-Reload Where Supported**
- API Keys tab clearly marked ✅ Hot-reload
- All others marked ⚠ Restart required
- Technician knows the cost

---

## Backup Strategy

**Automatic Backups:**
- Created before every save
- Timestamped: `appsettings.backup.2025-10-04-14-30-45.json`
- Stored in `config/backups/` directory
- Keep last 10 (configurable in System Settings)

**Manual Backups:**
- Download button exports current config
- Technician saves to laptop
- Can restore from laptop file

**Restore:**
- Click [🔄 Restore] on any backup
- Shows diff preview (what will change)
- Confirm before applying

---

## Summary

**Forms only. No raw JSON.**

- ✅ Tables for device lists
- ✅ Forms for editing
- ✅ Import/Export for bulk operations
- ✅ Validation everywhere
- ✅ Test connections before save
- ✅ Clear restart warnings

**Simple. Safe. Toyota.**
