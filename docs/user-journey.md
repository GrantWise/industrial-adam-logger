# User Journey - Industrial ADAM Logger Frontend

## Context: The Real-World Scenario

**Who**: Field service technician or automation engineer
**Where**: Factory floor, control room, or remote site
**When**: During commissioning, troubleshooting, or system expansion
**Time Pressure**: Limited on-site time, production downtime costs money
**Environment**: Harsh lighting, may be wearing gloves, laptop on cart or makeshift desk

**Critical Insight**: The technician is NOT sitting in an office. They're standing next to equipment, verifying connections, checking labels on ADAM modules, and confirming data flow. The frontend must support this **physical, iterative workflow**.

---

## Journey 1: Fresh Installation (Greenfield Deployment)

### Pre-Conditions
- TimescaleDB running (Docker container started)
- ADAM Logger service running (systemd or Docker)
- Frontend accessible at `http://localhost:5173` or `http://{server-ip}:5173`
- **No devices configured yet**

### Step 0: First Load - The "Empty State"

**What Technician Sees**:
```
┌─────────────────────────────────────────────────────┐
│ Industrial ADAM Logger              [Health: ●]     │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ✅ System Health: Healthy                          │
│  ┌────────────┬────────────┬────────────┐         │
│  │ Database   │ Service    │ Dead Letter│         │
│  │ ● Connected│ ● Running  │ Queue: 0   │         │
│  └────────────┴────────────┴────────────┘         │
│                                                     │
│  📦 No Devices Configured                           │
│  ┌────────────────────────────────────────┐        │
│  │                                        │        │
│  │   🚀 Get Started                       │        │
│  │                                        │        │
│  │   No Modbus or MQTT devices found.    │        │
│  │   Add your first device to start      │        │
│  │   logging data.                       │        │
│  │                                        │        │
│  │   [+ Add Modbus Device]               │        │
│  │   [+ Add MQTT Device]                 │        │
│  │                                        │        │
│  │   Or import configuration:            │        │
│  │   [📁 Upload appsettings.json]        │        │
│  │                                        │        │
│  └────────────────────────────────────────┘        │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Why This Matters**:
- Clear "empty state" tells technician what to do next
- No confusing errors or cryptic messages
- Two clear paths: Add devices manually OR import config file
- System health shows database is ready (not a config problem)

---

### Step 1: Physical Survey (Before Touching Computer)

**What Technician Does** (in the real world):
1. Walks to electrical panel/DIN rail
2. Finds ADAM-6051 modules (or 6050, 6017, etc.)
3. Reads labels on each module:
   - IP address (e.g., sticker says "192.168.1.100")
   - Device ID or location label (e.g., "Line 1 Counter")
   - Channel assignments (e.g., "Ch0: Main Product, Ch1: Rejects")
4. Opens notebook or takes photo of labels
5. Verifies network cable is connected (LED blinking on ADAM module)

**Critical Realization**: The technician has **physical documentation** (stickers, labels, drawings) that the frontend must help them **transfer into configuration**.

**Frontend Support Needed**:
- **Device discovery wizard** that prompts for this information step-by-step
- **IP scanner** to find devices on network automatically
- **Import from CSV/Excel** if they have a commissioning spreadsheet

---

### Step 2A: Add First Device (Manual Entry - Modbus)

**Technician Clicks**: `[+ Add Modbus Device]`

**Modal Opens - Device Setup Wizard**:
```
┌─────────────────────────────────────────────────────┐
│ Add Modbus Device                          [✕ Close]│
├─────────────────────────────────────────────────────┤
│                                                     │
│  Step 1 of 4: Basic Information                    │
│                                                     │
│  Device ID *                                        │
│  [ADAM-6051-LINE1____________]                     │
│  ℹ️ Must be unique. Use location or function name   │
│                                                     │
│  Display Name                                       │
│  [Line 1 Production Counter_]                      │
│                                                     │
│  Model Type (optional)                              │
│  [ADAM-6051 ▼]                                     │
│  Options: 6050, 6051, 6052, 6053, 6055, 6056,      │
│           6015, 6017, 6018, 6024                   │
│                                                     │
│  [Cancel]                         [Next: Network →]│
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Step 2: Network Configuration**:
```
┌─────────────────────────────────────────────────────┐
│ Add Modbus Device                          [✕ Close]│
├─────────────────────────────────────────────────────┤
│                                                     │
│  Step 2 of 4: Network Configuration                │
│                                                     │
│  IP Address *                                       │
│  [192.168.1.100_______] [🔍 Test Connection]       │
│  ℹ️ IP address must be reachable from this server   │
│                                                     │
│  Port *                                             │
│  [502___] (Default: 502 for Modbus TCP)            │
│                                                     │
│  Unit ID *                                          │
│  [1__] (Default: 1 for most ADAM modules)          │
│                                                     │
│  ✅ Connection successful! (Responded in 15ms)      │
│  Device: ADAM-6051, Firmware: 1.2.3                │
│                                                     │
│  [← Back]                        [Next: Channels →]│
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Feature**: `[🔍 Test Connection]` button
- Sends Modbus read request to verify connectivity
- Shows device info if successful (model, firmware)
- Shows clear error if failed: "Connection timeout. Check IP address, network cable, and device power."
- **This saves 10 minutes of debugging** - technician knows IMMEDIATELY if IP is wrong

**Step 3: Channel Configuration**:
```
┌─────────────────────────────────────────────────────┐
│ Add Modbus Device                          [✕ Close]│
├─────────────────────────────────────────────────────┤
│                                                     │
│  Step 3 of 4: Channel Configuration                │
│                                                     │
│  Model Detected: ADAM-6051 (8 Digital Counter Chs) │
│  📋 Auto-configure channels based on model?         │
│  [✓ Use Default for ADAM-6051] (2 channels enabled)│
│                                                     │
│  Channels: [+ Add Channel]                          │
│                                                     │
│  ┌──────────────────────────────────────────┐      │
│  │ ☑ Channel 0: Main Product Counter       │      │
│  │   Registers: 0-1 (UInt32)                │      │
│  │   Unit: items  Max Rate: 1000/sec        │      │
│  │   [✏️ Edit] [🗑️ Remove]                   │      │
│  └──────────────────────────────────────────┘      │
│                                                     │
│  ┌──────────────────────────────────────────┐      │
│  │ ☑ Channel 1: Reject Counter              │      │
│  │   Registers: 2-3 (UInt32)                │      │
│  │   Unit: items  Max Rate: 100/sec         │      │
│  │   [✏️ Edit] [🗑️ Remove]                   │      │
│  └──────────────────────────────────────────┘      │
│                                                     │
│  [← Back]                     [Next: Advanced →]   │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Feature**: **Model-based auto-configuration**
- If model selected (ADAM-6051), pre-populate typical channels
- ADAM-6051 → 2 channels (Ch0: registers 0-1, Ch1: registers 2-3, UInt32Counter)
- ADAM-6017 → 8 channels (analog inputs, registers 0-7, Int16/Float32)
- Technician can accept defaults or customize

**Why This Matters**: Technician doesn't need to remember register mappings. System knows ADAM-6051 Ch0 is always registers 0-1.

**Step 4: Advanced Settings** (Optional):
```
┌─────────────────────────────────────────────────────┐
│ Add Modbus Device                          [✕ Close]│
├─────────────────────────────────────────────────────┤
│                                                     │
│  Step 4 of 4: Advanced Settings (Optional)         │
│                                                     │
│  Poll Interval                                      │
│  [1000_] ms (Range: 100-5000ms, Default: 1000ms)   │
│  ℹ️ How often to read from device                   │
│                                                     │
│  Timeout                                            │
│  [3000_] ms (Must be > Poll Interval)              │
│                                                     │
│  Max Retries                                        │
│  [3_] (Default: 3)                                 │
│                                                     │
│  ☑ Enable TCP Keep-Alive (Recommended)             │
│                                                     │
│  ⚠️ Saving will restart the logger service          │
│  Downtime: ~5 seconds                              │
│                                                     │
│  [← Back]                      [💾 Save & Restart] │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**What Happens on Save**:
1. Frontend validates all inputs
2. Shows confirmation: "This will restart the service. Unsaved data may be lost. Continue?"
3. Sends `PUT /config/appsettings` with updated device list
4. Backend validates and saves
5. Frontend triggers service restart (via separate API endpoint)
6. Frontend shows progress: "Restarting service... (5s)"
7. Frontend polls `/health` until service is back online
8. Frontend redirects to device list showing new device

---

### Step 2B: Alternative - Network Discovery (Faster for Multiple Devices)

**Technician Clicks**: `[🔍 Discover Devices]` (on Dashboard or Add Device modal)

**Discovery Modal**:
```
┌─────────────────────────────────────────────────────┐
│ Network Discovery                          [✕ Close]│
├─────────────────────────────────────────────────────┤
│                                                     │
│  Scan IP Range                                      │
│  [192.168.1._] to [192.168.1.___]                  │
│  Port: [502_]                                       │
│                                                     │
│  [🔍 Start Scan]                                    │
│                                                     │
│  Scanning... 45/255 IPs checked                     │
│  ████████░░░░░░░░ 18%                               │
│                                                     │
│  Devices Found (3):                                 │
│  ┌────────────────────────────────────────────┐    │
│  │ ☑ 192.168.1.100 - ADAM-6051 (Responds)     │    │
│  │ ☑ 192.168.1.101 - ADAM-6051 (Responds)     │    │
│  │ ☑ 192.168.1.105 - ADAM-6017 (Responds)     │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  [Cancel]                    [Add Selected (3) →]  │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Feature**: Network scanner
- Scans IP range for Modbus TCP devices (port 502)
- Tests connection to each IP
- Reads device identification registers (if supported)
- Technician selects which devices to add
- Bulk import wizard for selected devices

**Why This Matters**: Adding 10 devices manually takes 30+ minutes. Network scan + bulk import takes 5 minutes.

---

### Step 3: First Verification - "Is Data Flowing?"

**After Adding First Device, Frontend Auto-Navigates to Device Details**:

```
┌─────────────────────────────────────────────────────┐
│ ADAM-6051-LINE1                           ● Online  │
├─────────────────────────────────────────────────────┤
│                                                     │
│  🎉 Device added successfully!                      │
│  Waiting for first reading... (polling every 1s)   │
│                                                     │
│  ⏱️ Time elapsed: 3 seconds                         │
│                                                     │
│  ● Polling active                                   │
│  ● Connection established                           │
│  ⏳ Waiting for data...                             │
│                                                     │
│  [View Live Data Stream]                            │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**After First Reading (2-3 seconds later)**:
```
┌─────────────────────────────────────────────────────┐
│ ADAM-6051-LINE1                           ● Online  │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ✅ Data flowing! First reading received.           │
│                                                     │
│  Latest Readings:                                   │
│  ┌────────────────────────────────────────────┐    │
│  │ Ch0: Main Product Counter                  │    │
│  │ 12,345 items (Good) ✓                      │    │
│  │ Last updated: 1s ago                       │    │
│  │                                            │    │
│  │ Ch1: Reject Counter                        │    │
│  │ 234 items (Good) ✓                         │    │
│  │ Last updated: 1s ago                       │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  [View Dashboard] [Add Another Device]             │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Feature**: **Immediate visual feedback**
- Technician doesn't have to guess if it's working
- Clear "waiting for first reading" state
- Celebration when data arrives (✅ Data flowing!)
- Shows actual values so technician can verify they're reasonable

**Real-World Verification**:
Technician now walks to the ADAM module and:
1. Triggers a count (e.g., waves hand in front of sensor)
2. Watches counter increment on screen
3. Confirms it's the correct counter (not a different channel)

---

### Step 4: Add Remaining Devices (Bulk Operation)

**Technician Returns to Dashboard**:

**Dashboard Now Shows**:
```
┌─────────────────────────────────────────────────────┐
│ Industrial ADAM Logger              [Health: ●]     │
├─────────────────────────────────────────────────────┤
│                                                     │
│  Modbus Devices (1)         [+ Add] [🔍 Discover]  │
│  ┌────────────────────────────────────────┐        │
│  │ ADAM-6051-LINE1  ● Online  Last: 1s   │        │
│  │ Ch0: 12,345  Ch1: 234                 │        │
│  └────────────────────────────────────────┘        │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Technician Clicks** `[🔍 Discover]` to add remaining 9 devices
**Result**: All 10 devices added in 5-10 minutes (vs. 60+ minutes manually)

---

## Journey 2: Monitoring (Normal Operations)

### Pre-Conditions
- 10 Modbus devices configured
- All devices online and sending data
- Technician checks dashboard periodically

### The Dashboard - At-A-Glance Status

```
┌─────────────────────────────────────────────────────┐
│ Industrial ADAM Logger              [Health: ●]     │
├─────────────────────────────────────────────────────┤
│                                                     │
│  System Health                                      │
│  ┌────────────┬────────────┬────────────┐         │
│  │ Database   │ Service    │ Dead Letter│         │
│  │ ● Connected│ ● Running  │ Queue: 0   │         │
│  └────────────┴────────────┴────────────┘         │
│                                                     │
│  Modbus Devices (10)    9 online, 1 offline        │
│  ┌────────────────────────────────────────┐        │
│  │ LINE1-6051   ● Online  12,345  234    │        │
│  │ LINE2-6051   ● Online  8,901   156    │        │
│  │ LINE3-6051   ○ Offline  ---    ---    │  ⚠️    │
│  │ PACK1-6051   ● Online  45,678  1,023  │        │
│  │ ...                                    │        │
│  └────────────────────────────────────────┘        │
│                                                     │
│  Recent Events                                      │
│  ┌────────────────────────────────────────┐        │
│  │ 14:32:15 LINE3-6051 disconnected       │  ⚠️    │
│  │ 14:30:45 Database connection restored  │  ✓    │
│  │ 14:28:12 LINE1-6051 high rate detected│  ⚠️    │
│  └────────────────────────────────────────┘        │
│                                                     │
│  [Auto-refresh: ON (5s)]  Last update: 2s ago      │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Key Features**:
- **Auto-refresh every 5 seconds** (configurable)
- **Visual hierarchy**: Offline devices stand out (○ vs ●)
- **Recent events** show what changed (alerts technician to problems)
- **Device counts** (9 online, 1 offline) - quick health check
- **Last update timestamp** - confirms dashboard is live

**Monitoring Pattern**:
- Technician glances at dashboard 2-3 times per shift
- All green = go home happy
- Any red/yellow = click to investigate

---

## Journey 3: Troubleshooting (Device Offline)

### Scenario: LINE3-6051 Offline

**Technician Sees Red on Dashboard** → Clicks on `LINE3-6051`

**Device Details Page**:
```
┌─────────────────────────────────────────────────────┐
│ LINE3-6051 (Line 3 Production Counter)    ○ Offline│
├─────────────────────────────────────────────────────┤
│                                                     │
│  ⚠️ Device Offline - Troubleshooting Guide          │
│                                                     │
│  Status:                                            │
│  ○ Offline (5 consecutive failures)                │
│  Last seen: 5 minutes ago (14:27:30)               │
│  Last error: Connection timeout                    │
│                                                     │
│  Troubleshooting Steps:                             │
│  ┌────────────────────────────────────────────┐    │
│  │ 1. Check network cable                     │    │
│  │    [🔍 Ping 192.168.1.102]                 │    │
│  │    ✅ Host is reachable (12ms)              │    │
│  │                                            │    │
│  │ 2. Check device power                      │    │
│  │    Walk to device and verify power LED     │    │
│  │                                            │    │
│  │ 3. Test Modbus connection                  │    │
│  │    [🔍 Test Modbus Read]                   │    │
│  │    ❌ Connection timeout (3000ms)           │    │
│  │                                            │    │
│  │ 4. Check firewall / network switch         │    │
│  │    Device responds to ping but not Modbus  │    │
│  │    → Likely firewall blocking port 502     │    │
│  │                                            │    │
│  │ 5. Restart connection                      │    │
│  │    [🔄 Restart Connection]                 │    │
│  │                                            │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Connection History (Last 24h):                     │
│  ┌────────────────────────────────────────────┐    │
│  │ Uptime: 98.5% (23h 38m online)             │    │
│  │ Total reads: 85,230 | Failed: 1,270       │    │
│  │ Success rate: 98.5%                        │    │
│  │                                            │    │
│  │ [View Full History]                        │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Features**:
1. **Guided troubleshooting wizard** - step-by-step diagnosis
2. **Ping test** - separates network from Modbus issues
3. **Modbus test** - tests actual protocol, not just network
4. **Contextual advice** - "Responds to ping but not Modbus → firewall"
5. **One-click restart** - try simple fix first
6. **Historical context** - was this always broken or just started?

**Troubleshooting Flow**:
```
Ping works? → YES → Modbus works? → NO  → Firewall issue
                                     ↓ YES → Connection restored
                   ↓ NO → Network cable unplugged or device powered off
```

**Technician Workflow**:
1. Sees device offline on dashboard
2. Clicks device, sees troubleshooting guide
3. Clicks `[🔍 Ping]` → Success (network OK)
4. Clicks `[🔍 Test Modbus Read]` → Timeout (Modbus blocked)
5. Calls IT: "Port 502 blocked to 192.168.1.102"
6. IT opens firewall
7. Clicks `[🔄 Restart Connection]` → Device comes online

**Time Saved**: 30 minutes of manual ping/telnet/wireshark debugging → 2 minutes with guided wizard

---

## Journey 4: Adding MQTT Devices (Different Workflow)

### Pre-Conditions
- Modbus devices already configured
- MQTT broker running (Mosquitto or EMQX)
- IoT sensors publishing to MQTT topics

### Step 1: MQTT Broker Configuration

**Technician Navigates to**: Configuration → MQTT Settings

```
┌─────────────────────────────────────────────────────┐
│ MQTT Broker Configuration                           │
├─────────────────────────────────────────────────────┤
│                                                     │
│  Broker Connection                                  │
│  ┌────────────────────────────────────────────┐    │
│  │ Host: [localhost__________]                │    │
│  │ Port: [1883____]                           │    │
│  │ Username: [logger_________]                │    │
│  │ Password: [••••••••] [👁 Show]             │    │
│  │                                            │    │
│  │ [🔍 Test Connection]                       │    │
│  │ ✅ Connected successfully                   │    │
│  │ Broker: Mosquitto 2.0.18                   │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Advanced Settings                                  │
│  ┌────────────────────────────────────────────┐    │
│  │ Client ID: [industrial-logger_]            │    │
│  │ QoS Level: [1 (At Least Once) ▼]          │    │
│  │ Keep-Alive: [60_] seconds                  │    │
│  │ ☐ Use TLS                                  │    │
│  │ ☑ Auto-Reconnect (5s delay)                │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  ⚠️ Saving will restart the logger service          │
│                                                     │
│  [Save & Restart]  [Cancel]                        │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Feature**: Test Connection before saving
- Verifies broker is reachable
- Shows broker type and version
- Prevents saving broken config

---

### Step 2: Discover MQTT Topics (Listen Mode)

**Before adding devices, technician needs to know WHAT topics exist**

**Technician Navigates to**: MQTT Devices → `[🔍 Discover Topics]`

```
┌─────────────────────────────────────────────────────┐
│ MQTT Topic Discovery                       [✕ Close]│
├─────────────────────────────────────────────────────┤
│                                                     │
│  Subscribe to pattern (listens for messages):      │
│  [#______________________________________]         │
│  ℹ️ Use # to listen to all topics                  │
│                                                     │
│  [▶️ Start Listening]  Duration: [30_] seconds     │
│                                                     │
│  Live Messages (12 topics found):                   │
│  ┌────────────────────────────────────────────┐    │
│  │ sensors/temperature/zone1                  │    │
│  │ {"channel": 0, "value": 25.5, "unit": "C"} │    │
│  │ QoS: 1 | 3 msg/min | Last: 2s ago          │    │
│  │ [+ Add Device]                             │    │
│  ├────────────────────────────────────────────┤    │
│  │ counters/production/line1                  │    │
│  │ 0x00,0x00,0x30,0x39 (binary)               │    │
│  │ QoS: 1 | 60 msg/min | Last: 1s ago         │    │
│  │ [+ Add Device]                             │    │
│  ├────────────────────────────────────────────┤    │
│  │ sensors/+/humidity                         │    │
│  │ Multiple sources (sensors/zone1/humidity,  │    │
│  │                  sensors/zone2/humidity)   │    │
│  │ [+ Add Device Pattern]                     │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  [Export as Template]  [Close]                     │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Features**:
1. **Live topic discovery** - listens for 30 seconds, shows all active topics
2. **Payload preview** - shows actual message format (JSON, binary, CSV)
3. **Message rate** - shows how often each topic publishes
4. **Pattern detection** - identifies wildcard patterns (sensors/+/humidity)
5. **One-click add** - click [+ Add Device] to auto-configure

**Why This Matters**: MQTT topics are often unknown or poorly documented. Discovery mode lets technician SEE what's actually publishing.

---

### Step 3: Add MQTT Device (Auto-Configured from Discovery)

**Technician Clicks** `[+ Add Device]` on `sensors/temperature/zone1`

**Device Wizard Opens (Pre-Filled)**:
```
┌─────────────────────────────────────────────────────┐
│ Add MQTT Device                            [✕ Close]│
├─────────────────────────────────────────────────────┤
│                                                     │
│  Auto-Configuration from Topic Discovery            │
│  ✅ Detected JSON format                            │
│  ✅ Detected fields: channel, value, unit           │
│                                                     │
│  Device Information:                                │
│  Device ID: [TEMP-ZONE1___________]                │
│  Name: [Temperature Sensor Zone 1_]                │
│                                                     │
│  Topic Subscription:                                │
│  [sensors/temperature/zone1____]                   │
│  ℹ️ Supports wildcards: + (single level), # (multi) │
│                                                     │
│  Payload Format: [JSON ▼]                          │
│  Data Type: [Float32 ▼]                            │
│                                                     │
│  JSON Field Mapping (auto-detected):                │
│  Channel: [$.channel______] ✓ Valid                │
│  Value:   [$.value________] ✓ Valid                │
│  Unit:    [$.unit_________] ✓ Valid (optional)     │
│                                                     │
│  Preview:                                           │
│  ┌────────────────────────────────────────────┐    │
│  │ Last message:                              │    │
│  │ {"channel": 0, "value": 25.5, "unit": "C"} │    │
│  │                                            │    │
│  │ Parsed:                                    │    │
│  │ Channel: 0                                 │    │
│  │ Value: 25.5                                │    │
│  │ Unit: C                                    │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  ⚠️ Saving will restart the logger service          │
│                                                     │
│  [Cancel]                      [💾 Save & Restart] │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Features**:
1. **Auto-detection** - guesses format (JSON/Binary/CSV) from payload
2. **JsonPath validation** - tests JsonPath against real message
3. **Live preview** - shows parsed result from actual MQTT message
4. **Fail-fast validation** - won't save if JsonPath invalid

**Time Saved**: Manual MQTT device config takes 10 minutes (trial and error with JsonPath). Auto-config takes 30 seconds.

---

### Step 4: MQTT Troubleshooting (Different from Modbus)

**MQTT Device Shows No Data**:

```
┌─────────────────────────────────────────────────────┐
│ TEMP-ZONE1 (Temperature Sensor Zone 1)    ⚠️ No Data│
├─────────────────────────────────────────────────────┤
│                                                     │
│  ⚠️ Subscribed but no messages received             │
│                                                     │
│  Status:                                            │
│  ● Broker connected                                 │
│  ● Subscribed to: sensors/temperature/zone1        │
│  ⚠️ No messages received in last 5 minutes          │
│                                                     │
│  Troubleshooting Steps:                             │
│  ┌────────────────────────────────────────────┐    │
│  │ 1. Check broker connection                 │    │
│  │    ✅ Connected to localhost:1883           │    │
│  │                                            │    │
│  │ 2. Verify topic subscription               │    │
│  │    [👁️ Show Live Messages]                  │    │
│  │    Opens MQTT message viewer...            │    │
│  │                                            │    │
│  │ 3. Check if sensor is publishing           │    │
│  │    Use external MQTT client (mosquitto_sub)│    │
│  │    mosquitto_sub -h localhost \            │    │
│  │       -t "sensors/temperature/zone1"       │    │
│  │                                            │    │
│  │ 4. Verify topic name matches exactly       │    │
│  │    MQTT topics are case-sensitive          │    │
│  │    sensors/temperature/zone1 ≠             │    │
│  │    sensors/Temperature/Zone1               │    │
│  │                                            │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Last Message Received:                             │
│  ┌────────────────────────────────────────────┐    │
│  │ Never                                      │    │
│  │                                            │    │
│  │ Total messages: 0                          │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**MQTT Message Viewer** (for debugging):
```
┌─────────────────────────────────────────────────────┐
│ MQTT Live Messages - sensors/temperature/zone1     │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [▶️ Start] [⏸️ Pause] [Clear]  Auto-scroll: ON     │
│                                                     │
│  ┌────────────────────────────────────────────┐    │
│  │ 14:35:42.123 | QoS: 1 | Retained: No      │    │
│  │ Topic: sensors/temperature/zone1           │    │
│  │ Payload (JSON):                            │    │
│  │ {                                          │    │
│  │   "channel": 0,                            │    │
│  │   "value": 25.5,                           │    │
│  │   "unit": "C",                             │    │
│  │   "timestamp": "2025-10-04T14:35:42Z"      │    │
│  │ }                                          │    │
│  │                                            │    │
│  │ Parsed Result: ✅                           │    │
│  │ Channel: 0, Value: 25.5 C                  │    │
│  ├────────────────────────────────────────────┤    │
│  │ 14:35:32.045 | QoS: 1 | Retained: No      │    │
│  │ ...                                        │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Messages received: 145 | Parsing errors: 0        │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Feature**: Live MQTT message viewer
- Shows raw MQTT messages as they arrive
- Shows parsed result (channel, value)
- Highlights parsing errors (invalid JsonPath, wrong data type)
- Essential for debugging MQTT config issues

---

## Journey 5: Configuration Changes (Production Updates)

### Scenario: Add 3 New Devices to Existing System

**Challenge**: System has 10 devices logging data. Production is running. Technician needs to add 3 more devices WITHOUT disrupting existing data flow.

**Current Limitation**: Config changes require service restart (5-10 seconds downtime)

**Workflow**:

1. **Schedule Maintenance Window** (optional)
   - Technician coordinates with production manager
   - Chooses low-production period (lunch break, shift change)

2. **Backup Current Config**
   ```
   Configuration → [📥 Download Backup]
   Saves: appsettings.backup.2025-10-04-14-30.json
   ```

3. **Add New Devices** (via wizard or bulk import)

4. **Validate Config**
   ```
   [Validate] button runs checks:
   ✓ Valid JSON
   ✓ No duplicate Device IDs
   ✓ All IP addresses valid
   ⚠️ Warning: Device ADAM-6017-NEW: Port 502 not responding
   ```

5. **Confirmation Dialog**
   ```
   ┌─────────────────────────────────────────────┐
   │ Save Configuration & Restart Service        │
   ├─────────────────────────────────────────────┤
   │                                             │
   │ ⚠️ This will restart the logger service     │
   │                                             │
   │ Downtime: ~5-10 seconds                     │
   │ Existing devices will stop logging briefly │
   │                                             │
   │ Changes:                                    │
   │ + Added 3 new devices                       │
   │ ~ Modified TimescaleDB batch size           │
   │                                             │
   │ ⚠️ 1 Warning:                                │
   │ Device ADAM-6017-NEW not responding         │
   │ (will retry after restart)                  │
   │                                             │
   │ [Cancel]              [Continue & Restart]  │
   │                                             │
   └─────────────────────────────────────────────┘
   ```

6. **Save & Restart**
   - Frontend saves config
   - Triggers service restart
   - Shows progress bar: "Restarting service... 3... 2... 1..."
   - Polls `/health` every 500ms until service responds
   - Shows "Service online" when ready

7. **Verification**
   - Dashboard auto-refreshes
   - Shows all 13 devices (10 old + 3 new)
   - Technician verifies new devices are online
   - Checks that old devices resumed logging

**Time to Complete**: 5 minutes (3 min config, 2 min restart + verify)

---

## Journey 6: Production Emergency (Database Full)

### Scenario: Database Disk Full, Data Not Saving

**Alert**: Dead Letter Queue growing (backup queue when DB fails)

**Dashboard Shows**:
```
┌─────────────────────────────────────────────────────┐
│ Industrial ADAM Logger            [Health: ⚠️ Degraded]│
├─────────────────────────────────────────────────────┤
│                                                     │
│  System Health                                      │
│  ┌────────────┬────────────┬────────────┐         │
│  │ Database   │ Service    │ Dead Letter│         │
│  │ ● Connected│ ● Running  │ Queue: 1,234│ ⚠️     │
│  └────────────┴────────────┴────────────┘         │
│                                                     │
│  ⚠️ ALERT: Dead Letter Queue Growing                │
│  Database writes failing. Data queued for retry.   │
│  Action required to prevent data loss.             │
│                                                     │
│  [View Dead Letter Queue →]                        │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Technician Clicks** `[View Dead Letter Queue →]`

```
┌─────────────────────────────────────────────────────┐
│ Dead Letter Queue                                    │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ⚠️ WARNING: Queue growing rapidly                   │
│  Queue Size: 1,234 readings (↑ 50/sec)             │
│  Disk Usage: 2.5 MB (queue file)                   │
│  Auto-retry: Enabled (next attempt in 45s)         │
│                                                     │
│  Recent Failures (Last 10):                         │
│  ┌────────────────────────────────────────────┐    │
│  │ 14:35:42 - ADAM-6051-LINE1 Ch0             │    │
│  │ Value: 12,345 | Error: Disk full           │    │
│  │ Retry count: 5                             │    │
│  ├────────────────────────────────────────────┤    │
│  │ 14:35:41 - ADAM-6051-LINE2 Ch0             │    │
│  │ Value: 8,901 | Error: Disk full            │    │
│  │ Retry count: 5                             │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Troubleshooting:                                   │
│  ┌────────────────────────────────────────────┐    │
│  │ Database Error: Disk full                  │    │
│  │                                            │    │
│  │ Recommended Actions:                       │    │
│  │ 1. Free disk space on database server      │    │
│  │ 2. Enable TimescaleDB compression          │    │
│  │ 3. Set up data retention policy            │    │
│  │                                            │    │
│  │ [View Database Health] [System Logs]       │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  Once database issue is resolved:                   │
│  [🔄 Retry All Now]  [Clear Queue (⚠️ Data Loss)]  │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Critical Features**:
1. **Early warning** - DLQ growing = database problem
2. **Contextual diagnosis** - shows actual error (disk full)
3. **Actionable advice** - tells technician what to do
4. **Manual retry** - after fixing DB, retry queued writes
5. **No data loss** - queue persists across restarts

**Resolution Workflow**:
1. Technician sees DLQ alert on dashboard
2. Calls database admin: "Database full, need disk space"
3. DBA frees space or enables compression
4. Technician clicks `[🔄 Retry All Now]`
5. Queue drains to zero
6. System returns to healthy state

**Data Integrity Guarantee**: Zero data loss despite database failure (Toyota principle: robust error handling)

---

## Key Design Principles Extracted from User Journeys

### 1. **Progressive Disclosure**
- Empty state → Add first device → Verify → Add more
- Don't overwhelm with all features at once
- Show next logical step clearly

### 2. **Immediate Feedback**
- Test connection → instant success/failure
- Save config → progress bar → confirmation
- Device added → show first reading within seconds

### 3. **Guided Troubleshooting**
- Don't just show "Connection timeout"
- Show step-by-step diagnosis (ping → modbus → firewall)
- Provide contextual advice based on symptoms

### 4. **Fail-Fast Validation**
- Test connection BEFORE saving config
- Validate JSON BEFORE restarting service
- Warn about non-responding devices

### 5. **Zero Data Loss Transparency**
- Show Dead Letter Queue status prominently
- Alert when queue growing (database problem)
- Clear path to resolution (retry after fixing)

### 6. **Auto-Configuration Where Possible**
- Network discovery (scan for devices)
- Model-based presets (ADAM-6051 → 2 channels, UInt32)
- MQTT topic discovery (listen mode)
- JsonPath auto-detection (parse sample message)

### 7. **Clear State Indicators**
- ● Online / ○ Offline / ⚠️ Degraded
- ✅ Hot-reload / ⚠️ Restart required
- Loading states ("Waiting for first reading...")
- Empty states ("No devices configured")

### 8. **Undo/Backup Always Available**
- Download backup before save
- Revert button for unsaved changes
- Restore from backup option
- Last 10 backups kept automatically

### 9. **Real-World Context**
- Show IP address next to device name (technician walks to that IP)
- Show last successful read time (how stale is data?)
- Show physical location in device name (Line 1, Zone 2)

### 10. **Minimize Downtime**
- Batch operations (add 10 devices at once)
- Clear restart warnings
- Fast restart (5 seconds)
- Resume monitoring immediately after restart

---

## Frontend Feature Priority (Must-Have vs. Nice-to-Have)

### Phase 1: MVP (Commissioning)
✅ Add Modbus device (wizard)
✅ Test connection (ping + modbus)
✅ View device status (online/offline)
✅ View latest readings
✅ Edit configuration (appsettings.json)
✅ System health dashboard

### Phase 2: Production Operations
✅ Network discovery (IP scan)
✅ Troubleshooting wizard (guided diagnosis)
✅ Dead Letter Queue monitoring
✅ MQTT broker configuration
✅ MQTT topic discovery (listen mode)
✅ Device restart (manual recovery)

### Phase 3: Advanced Features
⚠️ Time-series charts (historical data)
⚠️ Bulk import/export (CSV, Excel)
⚠️ Configuration templates (save/load common configs)
⚠️ Alert notifications (email, webhook)
⚠️ Audit log (who changed what when)

### Phase 4: Nice-to-Have
💡 Mobile-responsive design
💡 Dark mode
💡 Multi-language support
💡 Role-based access control (admin vs. viewer)

---

## Conclusion: The Real User Journey

The frontend is NOT a data visualization dashboard. It's a **commissioning and troubleshooting tool** for field technicians.

**Success Criteria**:
- Technician can commission 10 devices in < 30 minutes (vs. 2 hours manually)
- Technician can diagnose offline device in < 5 minutes (vs. 30 minutes with ping/telnet)
- Zero data loss even during database failures (DLQ monitoring)
- Configuration changes don't break system (validation + backup)

**Core Philosophy**: **Make the complex simple. Make the invisible visible. Make failures recoverable.**

This is Toyota engineering applied to software.
