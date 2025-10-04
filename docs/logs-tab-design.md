# Logs Tab - Error Diagnosis Design

## Philosophy: Logs Are For Troubleshooting, Not Monitoring

**When technicians use logs:**
- ❌ NOT for normal monitoring (that's the Dashboard)
- ✅ When something is broken and they need to know WHY
- ✅ When error appears on Dashboard ("Connection timeout")
- ✅ When behavior is unexpected (data not saving, device reconnecting)
- ✅ When writing support tickets (need evidence)

**Key principle: Make it FAST to find the relevant error.**

---

## Logs Tab Layout - Dense Table View

```
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ [Dashboard] [Configuration] [Logs]                                                         │
├────────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                            │
│ LOGS                                                                                       │
│                                                                                            │
│ Filters:  [Level: All ▼] [Source: All ▼] [Device: All ▼]                                 │
│           Search: [____________________________] [🔍]                                      │
│           Time:   [Last 1 hour ▼] From: [________] To: [________]                         │
│                                                                                            │
│           [Auto-Refresh ☑ ON (5s)]  [Clear Filters]  [📥 Export CSV]                      │
│                                                                                            │
│ ┌────────────┬───────┬────────────────┬──────────────────────────────────────────────┐   │
│ │ Timestamp  │ Level │ Source         │ Message                                      │   │
│ ├────────────┼───────┼────────────────┼──────────────────────────────────────────────┤   │
│ │ 14:32:15.3 │ ERROR │ModbusService   │Connection timeout: LINE3-6051                │ ▶ │
│ │ 14:30:45.1 │ INFO  │TimescaleStorage│Database connection restored (was down 30s)   │   │
│ │ 14:30:15.8 │ ERROR │TimescaleStorage│Failed to write batch: Connection refused    │ ▶ │
│ │ 14:28:12.5 │ WARN  │DataProcessor   │LINE1-6051 Ch0: High rate 1050/sec > 1000/sec│ ▶ │
│ │ 14:25:33.2 │ INFO  │AdamLoggerSvc   │Device LINE3-6051 added successfully         │   │
│ │ 14:20:01.7 │ INFO  │MqttService     │Device PRESS-LINE1 reconnected               │   │
│ │ 14:15:22.9 │ WARN  │DeadLetterQueue │Queue size: 5 items (database write failed)  │ ▶ │
│ │ 14:12:45.4 │ INFO  │DeadLetterQueue │All queued items written successfully        │   │
│ │ 14:10:10.1 │ INFO  │Program         │Service restarted (configuration updated)    │   │
│ │ 14:08:33.6 │ ERROR │ModbusConnection│LINE3-6051: Modbus exception code 02         │ ▶ │
│ │ 14:05:12.2 │ INFO  │ApiKeyAuth      │API key authenticated: prod-dashboard-01     │   │
│ │ 14:02:56.8 │ WARN  │ModbusConnection│LINE3-6051: Slow response 150ms > 100ms      │ ▶ │
│ │ 14:00:00.0 │ INFO  │Program         │Industrial ADAM Logger starting v1.0.0       │   │
│ └────────────┴───────┴────────────────┴──────────────────────────────────────────────┴───┘
│                                                                                            │
│ Showing 13 of 1,247 entries                                              [Load More (50)] │
│                                                                                            │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

**Color Coding (Background or Text):**
- ERROR = Red background
- WARN = Yellow/Orange background
- INFO = White/default
- DEBUG/TRACE = Light grey (dimmed)

**▶ Expand Arrow:**
- Click to see full details (stack trace, context, metadata)

---

## Expanded Log Entry (Inline)

**Triggered by**: Click ▶ arrow or click anywhere on error row

```
│ ├────────────┼───────┼────────────────┼──────────────────────────────────────────────┤   │
│ │ 14:32:15.3 │ ERROR │ModbusService   │Connection timeout: LINE3-6051                │ ▼ │
│ ├────────────┴───────┴────────────────┴──────────────────────────────────────────────┤   │
│ │ ERROR DETAILS                                                                       │   │
│ │                                                                                     │   │
│ │ Message:    Connection timeout attempting to read from device LINE3-6051           │   │
│ │ Device ID:  LINE3-6051                                                             │   │
│ │ IP Address: 192.168.1.102:502                                                      │   │
│ │ Timeout:    3000ms                                                                 │   │
│ │ Attempt:    3 of 3 (max retries exhausted)                                         │   │
│ │                                                                                     │   │
│ │ Exception Type: System.TimeoutException                                            │   │
│ │ Exception Message: The operation has timed out.                                    │   │
│ │                                                                                     │   │
│ │ Stack Trace:                                                                        │   │
│ │ at Industrial.Adam.Logger.Core.Services.ModbusDeviceConnection.ReadRegistersAsync  │   │
│ │ at Industrial.Adam.Logger.Core.Services.ModbusDevicePool.PollDeviceAsync           │   │
│ │ at Industrial.Adam.Logger.Core.Services.AdamLoggerService.ProcessDeviceAsync       │   │
│ │                                                                                     │   │
│ │ Context:                                                                            │   │
│ │ {                                                                                   │   │
│ │   "DeviceId": "LINE3-6051",                                                        │   │
│ │   "IpAddress": "192.168.1.102",                                                    │   │
│ │   "Port": 502,                                                                     │   │
│ │   "UnitId": 1,                                                                     │   │
│ │   "PollIntervalMs": 1000,                                                          │   │
│ │   "TimeoutMs": 3000,                                                               │   │
│ │   "ConsecutiveFailures": 5                                                         │   │
│ │ }                                                                                   │   │
│ │                                                                                     │   │
│ │ QUICK ACTIONS                                                                       │   │
│ │ [View Device] [Restart Device] [Copy Error] [View Related Logs]                    │   │
│ │                                                                                     │   │
│ │ [Collapse]                                                                          │   │
│ └─────────────────────────────────────────────────────────────────────────────────────┘   │
│ ├────────────┼───────┼────────────────┼──────────────────────────────────────────────┤   │
│ │ 14:30:45.1 │ INFO  │TimescaleStorage│Database connection restored (was down 30s)   │   │
```

**Key Features:**
1. **Structured error details** - Not just raw text dump
2. **Context object** - Shows all relevant data (Device ID, IP, settings)
3. **Stack trace** - For developers/support
4. **Quick actions** - Jump to device, restart, copy error
5. **View Related Logs** - Shows all logs for this device in timeframe

---

## Error-First View (Default When Errors Exist)

**When user opens Logs tab and recent errors exist:**

```
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ LOGS                                                                                       │
│                                                                                            │
│ ⚠️ 3 ERRORS IN LAST HOUR                                         [View All Logs Instead]   │
│                                                                                            │
│ ┌────────────┬────────────────┬──────────────────────────────────────────────────────┐   │
│ │ Time       │ Device/Source  │ Error                                                │   │
│ ├────────────┼────────────────┼──────────────────────────────────────────────────────┤   │
│ │ 14:32:15   │ LINE3-6051     │ Connection timeout (3000ms)                          │ ▶ │
│ │            │ ModbusService  │ Consecutive failures: 5                              │   │
│ │            │                │ Action: Check network cable to 192.168.1.102         │   │
│ ├────────────┼────────────────┼──────────────────────────────────────────────────────┤   │
│ │ 14:30:15   │ TimescaleDB    │ Failed to write batch: Connection refused            │ ▶ │
│ │            │ Storage        │ 100 readings sent to Dead Letter Queue               │   │
│ │            │                │ Action: Check database server is running             │   │
│ ├────────────┼────────────────┼──────────────────────────────────────────────────────┤   │
│ │ 14:08:33   │ LINE3-6051     │ Modbus exception code 02 (Illegal Data Address)      │ ▶ │
│ │            │ ModbusConn     │ Register: 0, Count: 2                                │   │
│ │            │                │ Action: Verify register configuration for device     │   │
│ └────────────┴────────────────┴──────────────────────────────────────────────────────┴───┘
│                                                                                            │
│ WARNINGS (5)                                                         [View Warnings]       │
│ ┌────────────┬────────────────┬──────────────────────────────────────────────────────┐   │
│ │ 14:28:12   │ LINE1-6051 Ch0 │ High rate detected: 1050/sec > 1000/sec limit        │   │
│ │ 14:15:22   │ DeadLetterQueue│ Queue size: 5 items (database write failed)          │   │
│ │ 14:02:56   │ LINE3-6051     │ Slow response: 150ms > 100ms threshold               │   │
│ └────────────┴────────────────┴──────────────────────────────────────────────────────┘   │
│                                                                                            │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

**Why This Works:**
- Technician opens Logs tab → Immediately sees WHAT went wrong
- Errors shown first, not buried in 1000s of INFO logs
- Suggested action for each error
- Can expand for details or jump to full log view

---

## Filter Panel (Advanced)

**Collapsed by default, click to expand:**

```
│ FILTERS  [▼ Show Advanced]                                                                │
│ ┌────────────────────────────────────────────────────────────────────────────────────┐   │
│ │                                                                                     │   │
│ │ Log Level:    [☑ ERROR] [☑ WARN] [☐ INFO] [☐ DEBUG] [☐ TRACE]                     │   │
│ │                                                                                     │   │
│ │ Source:       [☑ ModbusService] [☑ MqttService] [☑ TimescaleStorage]               │   │
│ │               [☑ DeadLetterQueue] [☑ DataProcessor] [☑ ApiKeyAuth]                 │   │
│ │               [☐ Program] [☐ Other]                                                 │   │
│ │                                                                                     │   │
│ │ Device:       [All ▼]  or  [LINE3-6051 ▼] (filter by specific device)             │   │
│ │                                                                                     │   │
│ │ Time Range:   [◉ Last 1 hour   ○ Last 6 hours   ○ Last 24 hours   ○ Custom]       │   │
│ │               From: [2025-10-04 14:00] To: [2025-10-04 15:00]                      │   │
│ │                                                                                     │   │
│ │ Search:       [connection timeout________________] (text search in message)        │   │
│ │               [☑] Case sensitive  [☑] Use regex                                    │   │
│ │                                                                                     │   │
│ │ [Apply Filters]  [Reset to Defaults]                                               │   │
│ │                                                                                     │   │
│ └────────────────────────────────────────────────────────────────────────────────────┘   │
```

---

## Quick Filter Presets (One-Click)

**At top of Logs tab:**

```
│ Quick Filters:                                                                             │
│ [Errors Only] [Errors + Warnings] [Device: LINE3-6051] [Last 10 Minutes] [Database Only]  │
```

**Presets automatically:**
- **Errors Only** = Level: ERROR, Time: Last 24h
- **Errors + Warnings** = Level: ERROR + WARN, Time: Last 24h
- **Device: LINE3-6051** = All logs for this device (auto-populated if clicked from Dashboard)
- **Last 10 Minutes** = Time: Last 10min, All levels
- **Database Only** = Source: TimescaleStorage, DeadLetterQueue

---

## Context-Aware Navigation (From Dashboard to Logs)

**Scenario:** User sees "LINE3-6051 offline" on Dashboard, clicks device

**Dashboard shows:**
```
│ ⚠ OFFLINE - Troubleshooting:                                                              │
│                                                                                            │
│ Last Error: Connection timeout (3000ms)                                                    │
│ Consecutive Failures: 5                                                                    │
│                                                                                            │
│ [View Logs for This Device →]                                                             │
```

**Clicking [View Logs] navigates to Logs tab with pre-filtered view:**
- Device: LINE3-6051
- Time: Last 1 hour
- Level: All
- Auto-expanded errors

**Result:** Technician sees ONLY logs related to this device's problem. No manual filtering needed.

---

## Log Export (For Support Tickets)

```
┌────────────────────────────────────────────────────────────────┐
│ Export Logs                                        [✕ Close]   │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│ EXPORT OPTIONS                                                 │
│                                                                │
│ Time Range:    [Last 1 hour ▼]                                │
│                Custom: From [_______] To [_______]            │
│                                                                │
│ Log Levels:    [☑ ERROR] [☑ WARN] [☑ INFO]                    │
│                [☐ DEBUG] [☐ TRACE]                             │
│                                                                │
│ Format:        [◉ CSV   ○ JSON   ○ Plain Text]                │
│                                                                │
│ Include:       [☑] Timestamp                                  │
│                [☑] Log level                                  │
│                [☑] Source/logger name                         │
│                [☑] Message                                    │
│                [☑] Exception details                          │
│                [☑] Context (JSON metadata)                    │
│                [☐] Stack traces (large file)                  │
│                                                                │
│ Estimated Size: 2.3 MB (1,247 log entries)                    │
│                                                                │
│ [Cancel]                                  [📥 Download Logs]   │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Use Case:**
- Technician encounters strange error
- Exports last 24h of ERROR + WARN logs
- Sends to vendor support or internal IT
- CSV format opens in Excel for analysis

---

## Real-Time Tail Mode (Auto-Scroll)

**Toggle button at top of logs:**

```
│ [Auto-Refresh: ON]  [Auto-Scroll: ON]  [Pause]                                            │
```

**When Auto-Scroll ON:**
- New logs appear at bottom
- Page automatically scrolls to show latest
- Like `tail -f` in Linux

**When Pause clicked:**
- Stops auto-refresh
- Logs freeze at current position
- Technician can read without interruption
- Resume button appears: [▶ Resume Live Tail]

**Use Case:**
- Technician restarting device
- Watches logs in real-time
- Sees "Device connected" appear
- Confirms fix worked

---

## Integration with Dashboard Alerts

**When Dashboard shows critical alert:**

```
│ CRITICAL ALERTS                                                                            │
│ ┌────────────────────────────────────────────────────────────────────────────────────┐   │
│ │ ⚠ LINE3-6051 offline for 5 minutes - Check network cable                           │   │
│ │   Last error: Connection timeout (192.168.1.102:502)                                │   │
│ │   [View Device] [Restart] [View Logs →]                                             │   │
│ └────────────────────────────────────────────────────────────────────────────────────┘   │
```

**Clicking [View Logs →]:**
- Opens Logs tab
- Pre-filtered to LINE3-6051
- Shows last 1 hour
- Errors expanded by default
- Technician sees full error context immediately

---

## Log Retention and Cleanup

**In System Settings tab:**

```
│ LOG MANAGEMENT                                                                             │
│                                                                                            │
│ Retention:       [7__] days  (Logs older than this are deleted)                           │
│ Max Size:        [500_] MB   (Rotate logs when size exceeded)                             │
│                                                                                            │
│ Current Status:  234 MB / 500 MB (47%)                                                     │
│                  Oldest log: 2025-09-27 (7 days ago)                                       │
│                                                                                            │
│ [Clear Logs Older Than 30 Days]  [Clear All Logs]                                         │
```

---

## Backend API Endpoints Needed

**For Logs Tab to Work:**

### `GET /logs`
**Query Parameters:**
- `level` (string) - ERROR, WARN, INFO, DEBUG, TRACE (comma-separated)
- `source` (string) - Logger name (comma-separated)
- `deviceId` (string) - Filter by device ID
- `search` (string) - Text search in message
- `from` (ISO 8601) - Start time
- `to` (ISO 8601) - End time
- `limit` (number) - Max results (default 100)
- `offset` (number) - Pagination offset

**Response:**
```json
{
  "total": 1247,
  "offset": 0,
  "limit": 100,
  "logs": [
    {
      "timestamp": "2025-10-04T14:32:15.345Z",
      "level": "Error",
      "logger": "Industrial.Adam.Logger.Core.Services.ModbusDeviceConnection",
      "message": "Connection timeout attempting to read from device LINE3-6051",
      "exception": {
        "type": "System.TimeoutException",
        "message": "The operation has timed out.",
        "stackTrace": "at Industrial.Adam.Logger.Core..."
      },
      "context": {
        "DeviceId": "LINE3-6051",
        "IpAddress": "192.168.1.102",
        "Port": 502,
        "TimeoutMs": 3000,
        "ConsecutiveFailures": 5
      }
    }
  ]
}
```

### `GET /logs/summary`
**Returns recent error/warning counts:**
```json
{
  "last1Hour": {
    "errors": 3,
    "warnings": 5,
    "info": 234
  },
  "last24Hours": {
    "errors": 12,
    "warnings": 45,
    "info": 5678
  },
  "recentErrors": [
    {
      "timestamp": "2025-10-04T14:32:15Z",
      "deviceId": "LINE3-6051",
      "message": "Connection timeout",
      "source": "ModbusService"
    }
  ]
}
```

### `GET /logs/export`
**Same query params as `/logs`, returns CSV/JSON file**

### `GET /logs/live` (WebSocket or SSE)
**Real-time log streaming for tail mode**

---

## Summary: Logs Tab Design Principles

### 1. **Error-First**
- Errors shown first when tab opens
- Buried INFO logs hidden by default
- Quick filter presets for common tasks

### 2. **Context-Aware Navigation**
- Click "View Logs" from Dashboard → Pre-filtered to that device
- Click error alert → Jump to relevant logs
- No manual filtering needed

### 3. **Actionable Details**
- Expand error → See full context, stack trace, device settings
- Quick actions: View Device, Restart, Copy Error
- Suggested action for common errors

### 4. **Fast Filtering**
- Quick filter presets (1 click)
- Advanced filters (checkboxes, not typing)
- Search for specific errors

### 5. **Export for Support**
- CSV export for Excel analysis
- Include/exclude context, stack traces
- Pre-filtered before export

### 6. **Real-Time Tail**
- Auto-scroll like `tail -f`
- Pause/Resume
- Watch logs during troubleshooting

**Toyota: Dense table, fast filtering, context-aware. No fancy log aggregation UI.**

Like grep + tail, but in a web UI.
