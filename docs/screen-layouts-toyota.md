# Screen Layouts - Toyota Philosophy

## Design Principle: Dense, Not Pretty

**Target User**: Daily technician, not first-time user
**Pattern**: Industrial HMI/SCADA, not consumer SaaS
**Priority**: Information density over aesthetics

**Anti-Patterns to Avoid**:
- ❌ Wizards (technicians know what they're doing)
- ❌ Empty states with long explanations
- ❌ Progressive disclosure (show everything, let them scan)
- ❌ Animations and transitions (waste of time)
- ❌ Large margins and whitespace (information is valuable, space is not)

**What We Want**:
- ✅ Table-heavy layouts (like Excel, not like Notion)
- ✅ Inline editing (click cell, type, save)
- ✅ Status at a glance (color-coded, compact)
- ✅ Popout modals for configuration (keeps main screen clean)
- ✅ High information density (more like Bloomberg Terminal, less like Stripe Dashboard)

---

## Main Dashboard - Single Page, All Information

**Layout: Dense Single-Page View**

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Industrial ADAM Logger    Uptime: 3h 24m    DB: ● Connected    DLQ: 0    [⚙️ Config]  │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                        │
│ MODBUS DEVICES (10)                                          [+ Add] [🔍 Scan Network]│
│ ┌──────────────┬──────┬─────────────────┬────────┬──────────┬─────────┬────────────┐ │
│ │ Device ID    │Status│ IP:Port         │ Ch0    │ Ch1      │ Last OK │ Actions    │ │
│ ├──────────────┼──────┼─────────────────┼────────┼──────────┼─────────┼────────────┤ │
│ │LINE1-6051    │ ●    │192.168.1.100:502│12,345  │234       │2s ago   │[↻][✎][🗑] │ │
│ │LINE2-6051    │ ●    │192.168.1.101:502│8,901   │156       │1s ago   │[↻][✎][🗑] │ │
│ │LINE3-6051    │ ○    │192.168.1.102:502│---     │---       │5m ago   │[↻][✎][🗑] │ │
│ │PACK1-6051    │ ●    │192.168.1.103:502│45,678  │1,023     │3s ago   │[↻][✎][🗑] │ │
│ │PACK2-6051    │ ●    │192.168.1.104:502│23,456  │678       │2s ago   │[↻][✎][🗑] │ │
│ │WELD1-6051    │ ●    │192.168.1.105:502│9,012   │45        │1s ago   │[↻][✎][🗑] │ │
│ │TEMP1-6017    │ ●    │192.168.1.200:502│24.5°C  │101.3 PSI │2s ago   │[↻][✎][🗑] │ │
│ │TEMP2-6017    │ ●    │192.168.1.201:502│26.1°C  │99.8 PSI  │1s ago   │[↻][✎][🗑] │ │
│ │PRESS1-6024   │ ●    │192.168.1.210:502│150 PSI │---       │3s ago   │[↻][✎][🗑] │ │
│ │FLOW1-6015    │ ⚠    │192.168.1.220:502│45.2 L/m│---       │30s ago  │[↻][✎][🗑] │ │
│ └──────────────┴──────┴─────────────────┴────────┴──────────┴─────────┴────────────┘ │
│ Status: 8 online, 1 offline, 1 slow                         Success Rate: 98.9%      │
│                                                                                        │
│ MQTT DEVICES (5)                                   Broker: ● localhost:1883  12.5 msg/s│
│ ┌──────────────┬──────┬─────────────────────────┬──────────┬─────────┬────────────┐  │
│ │ Device ID    │Status│ Topic                   │ Value    │ Last OK │ Actions    │  │
│ ├──────────────┼──────┼─────────────────────────┼──────────┼─────────┼────────────┤  │
│ │TEMP-ZONE1    │ ●    │sensors/temp/zone1       │25.5°C    │3s ago   │[✎][🗑]     │  │
│ │TEMP-ZONE2    │ ●    │sensors/temp/zone2       │24.8°C    │2s ago   │[✎][🗑]     │  │
│ │COUNTER-PROD  │ ●    │counters/production      │12,345    │1s ago   │[✎][🗑]     │  │
│ │HUMID-MAIN    │ ●    │sensors/humidity         │65.3%     │5s ago   │[✎][🗑]     │  │
│ │PRESS-LINE1   │ ○    │sensors/pressure/line1   │---       │2m ago   │[✎][🗑]     │  │
│ └──────────────┴──────┴─────────────────────────┴──────────┴─────────┴────────────┘  │
│ Status: 4 active, 1 stale (no messages)                                               │
│                                                                                        │
│ SYSTEM EVENTS (Last 20)                                               [Clear] [Export]│
│ ┌──────────┬──────────────────────────────────────────────────────────────────────┐  │
│ │ Time     │ Event                                                                 │  │
│ ├──────────┼──────────────────────────────────────────────────────────────────────┤  │
│ │ 14:32:15 │ ⚠ LINE3-6051 offline (Connection timeout)                            │  │
│ │ 14:30:45 │ ✓ Database connection restored (was down 30s)                        │  │
│ │ 14:28:12 │ ⚠ FLOW1-6015 slow response (150ms > 100ms threshold)                 │  │
│ │ 14:25:33 │ ⚠ LINE1-6051 Ch0 high rate detected (1050/sec > 1000/sec limit)      │  │
│ │ 14:20:01 │ ✓ MQTT device PRESS-LINE1 reconnected                                │  │
│ │ 14:15:22 │ ⚠ Dead Letter Queue: 5 items (database write failed)                 │  │
│ │ 14:12:45 │ ✓ Dead Letter Queue drained (all items written)                      │  │
│ │ 14:10:10 │ ✓ Service restarted (configuration updated)                          │  │
│ └──────────┴──────────────────────────────────────────────────────────────────────┘  │
│                                                                                        │
│ CRITICAL ALERTS                                                                        │
│ ┌────────────────────────────────────────────────────────────────────────────────┐   │
│ │ ⚠ LINE3-6051 offline for 5 minutes - Check network cable                       │   │
│ │ ⚠ PRESS-LINE1 no MQTT messages for 2 minutes - Check sensor power              │   │
│ └────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘

Auto-refresh: ●ON (5s)   Last update: 2s ago   [Pause] [Manual Refresh]
```

**Key Features**:
- **Everything on one screen** - No tabs, no navigation
- **Table-first design** - Like Excel, not like a modern SaaS
- **Inline actions** - Click [↻] to restart, [✎] to edit, [🗑] to delete
- **Status at a glance** - ● green, ○ grey, ⚠ yellow
- **Critical alerts** at bottom - Can't be missed
- **Auto-refresh** - Updates every 5 seconds automatically
- **No whitespace** - Dense, information-rich

**What Technician Sees in 2 Seconds**:
- LINE3-6051 is offline (red dot)
- PRESS-LINE1 MQTT device stale (grey dot)
- Critical alerts at bottom show exactly what's wrong
- All other devices green (good)

**Action**: Click [↻] next to LINE3-6051 → Restart connection → Problem fixed

---

## Device Configuration Modal (Popout)

**Triggered by**: Click [✎] edit icon next to any device

```
┌────────────────────────────────────────────────────────────────┐
│ Edit Device: LINE3-6051                            [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ Device ID:  [LINE3-6051__________]  (Cannot change)           │
│ Name:       [Line 3 Production Counter___________]            │
│ Model:      [ADAM-6051 ▼]                                     │
│ IP:         [192.168.1.102_] Port: [502_] Unit ID: [1_]       │
│ Poll (ms):  [1000_] Timeout: [3000_] Retries: [3_]            │
│                                                                │
│ [🔍 Test Connection]  Status: ❌ Connection timeout            │
│                                                                │
│ CHANNELS                                        [+ Add Channel]│
│ ┌──┬──────────────────┬─────┬─────┬────────┬─────┬────────┐  │
│ │✓│Name              │Reg  │Count│Type    │Scale│Unit    │  │
│ ├─┼──────────────────┼─────┼─────┼────────┼─────┼────────┤  │
│ │☑│Main Counter      │0    │2    │UInt32  │1.0  │items   │  │
│ │☑│Reject Counter    │2    │2    │UInt32  │1.0  │items   │  │
│ └─┴──────────────────┴─────┴─────┴────────┴─────┴────────┘  │
│                                                                │
│ ADVANCED                                                       │
│ ☑ TCP Keep-Alive                                              │
│ ☐ Enabled (temporarily disable this device)                   │
│                                                                │
│ ⚠ Saving will restart logger service (~5 seconds downtime)    │
│                                                                │
│ [Cancel]  [Delete Device]            [Save & Restart Service] │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Key Features**:
- **Form-based editing** - No JSON wizardry
- **Inline test** - Test connection without saving
- **Table for channels** - Add/edit channels in grid
- **Clear warning** - Restart required
- **Delete option** - Remove device if decommissioned

**Fast Workflow**:
1. Click [✎] on device
2. Change IP address
3. Click [🔍 Test Connection] → Success
4. Click [Save & Restart] → Done

Time: 15 seconds

---

## Add Device Modal (Popout)

**Triggered by**: Click [+ Add] button

```
┌────────────────────────────────────────────────────────────────┐
│ Add Modbus Device                                  [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ Device ID:  [_________________]  (Required, unique)           │
│ Name:       [_________________]                               │
│ Model:      [ADAM-6051 ▼]  [Load Defaults]                   │
│ IP:         [_____________] Port: [502_] Unit ID: [1_]        │
│                                                                │
│ [🔍 Test Connection]  Status: ---                             │
│                                                                │
│ CHANNELS (Auto-filled for ADAM-6051)               [+ Add]    │
│ ┌──┬──────────────────┬─────┬─────┬────────┬─────┬────────┐  │
│ │✓│Name              │Reg  │Count│Type    │Scale│Unit    │  │
│ ├─┼──────────────────┼─────┼─────┼────────┼─────┼────────┤  │
│ │☑│Channel 0         │0    │2    │UInt32  │1.0  │counts  │  │
│ │☑│Channel 1         │2    │2    │UInt32  │1.0  │counts  │  │
│ └─┴──────────────────┴─────┴─────┴────────┴─────┴────────┘  │
│                                                                │
│ ⚠ Saving will restart logger service (~5 seconds downtime)    │
│                                                                │
│ [Cancel]                             [Save & Restart Service] │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Key Features**:
- **Model-based defaults** - ADAM-6051 → auto-fill 2 channels
- **Test before save** - Verify connection works
- **Minimal fields** - Only essentials, rest defaults to sane values

---

## Network Scanner Modal (Popout)

**Triggered by**: Click [🔍 Scan Network] button

```
┌────────────────────────────────────────────────────────────────┐
│ Network Scanner                                    [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ Scan Range:  [192.168.1._] to [192.168.1.___]  Port: [502_]  │
│                                                                │
│ [▶ Start Scan]  [⏸ Pause]  [⏹ Stop]                           │
│                                                                │
│ Progress: ████████░░░░░░░░ 45/255 (18%)  Elapsed: 12s         │
│                                                                │
│ DEVICES FOUND (3)                               [Add All (3)]  │
│ ┌──┬─────────────────┬──────────────┬──────────┬──────────┐   │
│ │✓│IP               │Model         │Response  │Status    │   │
│ ├─┼─────────────────┼──────────────┼──────────┼──────────┤   │
│ │☑│192.168.1.100    │ADAM-6051     │15ms      │Ready     │   │
│ │☑│192.168.1.101    │ADAM-6051     │18ms      │Ready     │   │
│ │☑│192.168.1.105    │ADAM-6017     │22ms      │Ready     │   │
│ └─┴─────────────────┴──────────────┴──────────┴──────────┘   │
│                                                                │
│ ⚠ Adding 3 devices will restart logger service                │
│                                                                │
│ [Cancel]                          [Add Selected & Restart]    │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Key Features**:
- **Fast bulk add** - Scan network, select all, add
- **Shows response time** - Verify devices are responsive
- **Model detection** - Identifies ADAM model (if supported)

**Workflow**:
1. Click [🔍 Scan Network]
2. Scanner finds 3 devices
3. Check all boxes (or click [Add All])
4. Click [Add Selected & Restart]
5. 3 devices now in table

Time: 30 seconds (vs. 15 minutes manual)

---

## Configuration Editor (Popout)

**Triggered by**: Click [⚙️ Config] button in header

```
┌────────────────────────────────────────────────────────────────────────────┐
│ Configuration                                                  [✕ Close]   │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│ [Devices (JSON)] [API Keys] [Environment] [Database]                      │
│                                                                            │
│ ──────────────────── Devices (JSON) ────────────────────                  │
│                                                                            │
│ ⚠ Direct JSON editing - Advanced users only                               │
│ ⚠ Invalid JSON will prevent service restart                               │
│                                                                            │
│ ┌────────────────────────────────────────────────────────────────────┐   │
│ │ {                                                                  │   │
│ │   "AdamLogger": {                                                  │   │
│ │     "Devices": [                                                   │   │
│ │       {                                                            │   │
│ │         "DeviceId": "LINE1-6051",                                  │   │
│ │         "IpAddress": "192.168.1.100",                              │   │
│ │         "Port": 502,                                               │   │
│ │         "UnitId": 1,                                               │   │
│ │         "PollIntervalMs": 1000,                                    │   │
│ │         "TimeoutMs": 3000,                                         │   │
│ │         "Channels": [                                              │   │
│ │           {                                                        │   │
│ │             "ChannelNumber": 0,                                    │   │
│ │             "StartRegister": 0,                                    │   │
│ │             "RegisterCount": 2,                                    │   │
│ │             "Unit": "items"                                        │   │
│ │           }                                                        │   │
│ │         ]                                                          │   │
│ │       }                                                            │   │
│ │     ]                                                              │   │
│ │   }                                                                │   │
│ │ }                                                                  │   │
│ └────────────────────────────────────────────────────────────────────┘   │
│                                                                            │
│ Validation: ✓ Valid JSON   2 warnings                                     │
│ - Device LINE2-6051: TimeoutMs < PollIntervalMs (should be >)             │
│ - Device TEMP1-6017: ScaleFactor not set (defaulting to 1.0)              │
│                                                                            │
│ [📥 Download Backup]  [📤 Upload]  [Validate]        [Save & Restart]     │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

**API Keys Tab**:
```
│ ──────────────────── API Keys ──────────────────────                      │
│                                                                            │
│ ✅ Hot-reload enabled - Changes apply automatically (no restart)           │
│                                                                            │
│ ┌──┬────────────┬──────────────────┬────────────┬──────────┬──────────┐  │
│ │✓│ID          │Name              │Key         │Expires   │Actions   │  │
│ ├─┼────────────┼──────────────────┼────────────┼──────────┼──────────┤  │
│ │☑│dev-srv-1   │Dev Service       │IND-...789  │Never     │[✎][🗑]   │  │
│ │☑│prod-dash-1 │Dashboard         │IND-...456  │2026-01-01│[✎][🗑]   │  │
│ │☑│temp-tech-1 │Technician        │IND-...123  │2025-10-05│[✎][🗑]   │  │
│ └─┴────────────┴──────────────────┴────────────┴──────────┴──────────┘  │
│                                                                            │
│ [+ Add Key]                                                  [Save]       │
```

**Environment Tab**:
```
│ ──────────────────── Environment Variables ──────────────────────         │
│                                                                            │
│ ⚠ Requires service restart after save                                     │
│                                                                            │
│ ┌────────────────────────┬─────────────────────────────────────────┐     │
│ │ Variable               │ Value                                   │     │
│ ├────────────────────────┼─────────────────────────────────────────┤     │
│ │ TIMESCALEDB_PASSWORD   │ [••••••••] [👁 Show]                    │     │
│ │ ASPNETCORE_ENVIRONMENT │ [Production ▼]                          │     │
│ │ LOG_LEVEL              │ [Information ▼]                         │     │
│ └────────────────────────┴─────────────────────────────────────────┘     │
│                                                                            │
│ [+ Add Variable]                                      [Save & Restart]    │
```

**Key Features**:
- **Tabs for different config types** - Devices, API Keys, Env Vars, Database
- **JSON editor for advanced users** - Direct editing if needed
- **Table editor for simple tasks** - Most users never touch JSON
- **Clear restart warnings** - Know what requires downtime
- **Hot-reload indicator** - API keys apply immediately

---

## MQTT Topic Discovery Modal

**Triggered by**: MQTT Devices section → [🔍 Discover Topics] button

```
┌────────────────────────────────────────────────────────────────┐
│ MQTT Topic Discovery                               [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ Subscribe: [#______________]  Duration: [30_] sec             │
│                                                                │
│ [▶ Start] [⏹ Stop]  Listening... 12s elapsed                  │
│                                                                │
│ TOPICS FOUND (8)                                    [Add All]  │
│ ┌──┬──────────────────────────┬────────┬──────────┬──────┐   │
│ │✓│Topic                     │Format  │Rate      │Value │   │
│ ├─┼──────────────────────────┼────────┼──────────┼──────┤   │
│ │☑│sensors/temp/zone1        │JSON    │0.3 msg/s │25.5°C│   │
│ │☑│sensors/temp/zone2        │JSON    │0.3 msg/s │24.8°C│   │
│ │☑│counters/production       │Binary  │1.0 msg/s │12345 │   │
│ │☑│sensors/humidity          │JSON    │0.5 msg/s │65.3% │   │
│ │☑│sensors/pressure/line1    │CSV     │0.2 msg/s │150PSI│   │
│ └─┴──────────────────────────┴────────┴──────────┴──────┘   │
│                                                                │
│ ⚠ Adding 5 devices will restart logger service                │
│                                                                │
│ [Cancel]                          [Add Selected & Restart]    │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Key Features**:
- **Listen mode** - Subscribe to # (all topics) for 30 seconds
- **Auto-detect format** - JSON, Binary, or CSV
- **Show message rate** - Verify topic is active
- **Bulk add** - Select all, add at once

---

## Troubleshooting Panel (Inline Expansion)

**Triggered by**: Click on offline device row in main table

**Main table expands inline**:

```
│ ├──────────────┼──────┼─────────────────┼────────┼──────────┼─────────┼────────────┤ │
│ │LINE3-6051    │ ○    │192.168.1.102:502│---     │---       │5m ago   │[↻][✎][🗑] │ │
│ ├──────────────┴──────┴─────────────────┴────────┴──────────┴─────────┴────────────┤ │
│ │ ⚠ OFFLINE - Troubleshooting:                                                      │ │
│ │                                                                                    │ │
│ │ Last Error: Connection timeout (3000ms)                                           │ │
│ │ Last Successful Read: 5 minutes ago (14:27:30)                                    │ │
│ │ Consecutive Failures: 15                                                          │ │
│ │                                                                                    │ │
│ │ Quick Tests:                                                                      │ │
│ │ [🔍 Ping 192.168.1.102]      Result: ✅ Reachable (12ms)                          │ │
│ │ [🔍 Test Modbus Read]        Result: ❌ Connection timeout                         │ │
│ │                                                                                    │ │
│ │ Diagnosis: Device responds to ping but not Modbus → Likely firewall issue        │ │
│ │ Action: Check firewall rules for port 502 to 192.168.1.102                       │ │
│ │                                                                                    │ │
│ │ [Collapse]                                                                        │ │
│ └────────────────────────────────────────────────────────────────────────────────── │ │
│ ├──────────────┼──────┼─────────────────┼────────┼──────────┼─────────┼────────────┤ │
│ │PACK1-6051    │ ●    │192.168.1.103:502│45,678  │1,023     │3s ago   │[↻][✎][🗑] │ │
```

**Key Features**:
- **Inline expansion** - No modal, stays in context
- **One-click tests** - Ping and Modbus test buttons
- **Contextual diagnosis** - Interprets test results
- **Suggested action** - Tells technician exactly what to do

---

## Summary: Toyota Design Principles Applied

### 1. **Dense Information, Not Pretty**
- Tables everywhere (like Excel)
- Minimal whitespace
- 20+ devices visible on one screen
- No scrolling for critical info

### 2. **Direct Manipulation**
- Click [↻] → Restart immediately
- Click [✎] → Edit modal pops up
- Click device row → Troubleshooting expands inline
- No multi-step workflows

### 3. **Popouts for Configuration**
- Add/Edit device → Modal
- Configuration → Modal
- Network scan → Modal
- **Main screen stays clean** (just tables + status)

### 4. **Status at a Glance**
- Color-coded dots (●/○/⚠)
- Critical alerts at bottom (can't miss)
- Auto-refresh (no manual clicking)
- Last update timestamp

### 5. **No Clever Features**
- No wizards
- No animations
- No progressive disclosure
- No "empty states" with illustrations

### 6. **Inline Editing Where Possible**
- Table cells editable (like Excel)
- Click cell, type, press Enter, save
- No modals for simple edits

### 7. **Fast Actions**
- Restart device: 1 click
- Edit device: 1 click → modal → change → save
- Add device: 1 click → fill form → save
- Bulk add: Scan → select all → add

**This is Industrial UI. Not SaaS UI.**

Like a factory HMI. Like Bloomberg Terminal. Like Excel.

**Information-dense. Fast. Boring. Reliable.**

That's Toyota.
