# Frontend Development Plan - Industrial ADAM Logger (Revised)

## Project Overview

**Goal**: Build a user-focused frontend for commissioning, monitoring, and troubleshooting industrial ADAM devices.

**Design Philosophy**: Toyota + User Empathy
- Simple, robust, dependable
- ONE tool for troubleshooting (no context switching)
- Forms for common operations, download/upload for rare operations
- Use tools team knows (shadcn/ui, Tailwind)
- Don't duplicate existing tools (Grafana for charts, journalctl for logs)

**Tech Stack**:
- React 18 + TypeScript
- Vite (build tool)
- Tailwind CSS (team knows it)
- shadcn/ui (team knows it)
- React Query (server state)
- React Router (routing)
- Recharts (simple live charts only)

**Timeline**: **5 weeks** (reduced from 7)

---

## Development Phases

### Phase 1: Foundation (Week 1)
**Goal**: Basic app structure, routing, API integration

**Tasks**:
1. ✅ Create feature branch (`feature/frontend-dashboard`)
2. ✅ Initialize frontend directory structure
3. ⬜ Initialize Vite + React + TypeScript project
   ```bash
   npm create vite@latest frontend -- --template react-ts
   cd frontend
   npm install
   ```
4. ⬜ Configure Tailwind CSS
   ```bash
   npm install -D tailwindcss postcss autoprefixer
   npx tailwindcss init -p
   ```
5. ⬜ Install and configure shadcn/ui
   ```bash
   npx shadcn-ui@latest init
   npx shadcn-ui@latest add button table dialog input select
   ```
6. ⬜ Install dependencies
   ```bash
   npm install react-router-dom @tanstack/react-query axios date-fns recharts
   npm install -D @types/node
   ```
7. ⬜ Set up React Router (2 routes: Dashboard, Configuration)
8. ⬜ Create API client (axios + TypeScript types)
9. ⬜ Set up React Query for data fetching
10. ⬜ Create basic layout component (header, nav, content area)
11. ⬜ Test API connectivity to backend (`GET /health`)

**Deliverables**:
- Working dev server (`npm run dev`)
- Basic 2-page navigation (Dashboard, Configuration)
- API health check working
- TypeScript configured with strict mode
- Tailwind + shadcn working

**Files Created**:
```
frontend/
├── src/
│   ├── components/
│   │   ├── ui/                    # shadcn components
│   │   └── common/
│   │       └── Layout.tsx         # Main layout
│   ├── lib/
│   │   ├── api.ts                 # Axios client
│   │   ├── types.ts               # TypeScript types
│   │   └── utils.ts
│   ├── App.tsx
│   ├── main.tsx
│   └── index.css
├── package.json
├── tsconfig.json
├── vite.config.ts
└── tailwind.config.js
```

---

### Phase 2: Dashboard - Core Monitoring (Week 2)
**Goal**: Dense device table with real-time status

**Tasks**:
1. ⬜ Create Dashboard page component
2. ⬜ Build device table component
   - Single table for ALL devices (Modbus + MQTT)
   - Columns: Checkbox, Device ID, Status (●/○/⚠), IP/Topic, Ch0, Ch1, Actions
   - Status indicators with colors (green/grey/yellow)
   - Inline actions: [↻ Restart] [✎ Edit]
   - Expand arrow (▶/▼) for troubleshooting panel
3. ⬜ Create system health summary component
   - Database status (●/○)
   - MQTT broker status (●/○)
   - Dead Letter Queue size
   - Service uptime
4. ⬜ Build critical alerts banner
   - Red/yellow banner at top
   - Shows active alerts (device offline, DLQ growing, etc.)
   - Dismiss button
5. ⬜ Implement auto-refresh logic
   - React Query polling with `refetchInterval: 10000` (10s)
   - Pause/Resume button
   - Last update timestamp
6. ⬜ Create status indicator component
   - ● Green = online/good
   - ○ Grey = offline
   - ⚠ Yellow = degraded/warning

**API Integration**:
- `GET /health/detailed`
- `GET /devices`
- `GET /data/latest`
- `GET /data/stats`

**Deliverables**:
- Dashboard shows all devices in one table
- Real-time status updates (10s polling)
- Critical alerts visible at top
- System health summary
- Responsive to window resize

**Files Created**:
```
src/components/dashboard/
├── DashboardPage.tsx
├── DeviceTable.tsx
├── SystemHealth.tsx
├── CriticalAlerts.tsx
└── StatusIndicator.tsx
```

---

### Phase 3: Inline Troubleshooting (Week 3)
**Goal**: Expand device row to show diagnostics (no context switch)

**Tasks**:
1. ⬜ Build inline troubleshooting panel component
   - Expands when user clicks ▶ on device row
   - Shows device status, last seen time
   - Shows recent logs (last 10 for THIS device)
   - Shows test buttons (Ping, Modbus)
   - Shows live chart (60 seconds)
   - Contextual diagnosis messages
2. ⬜ Implement test connection functionality
   - Ping test button → Calls `POST /tools/ping`
   - Modbus test button → Calls `POST /tools/modbus-test`
   - Show results inline (✅/❌ with latency)
3. ⬜ Build recent logs component
   - Table of last 10 log entries for selected device
   - Columns: Timestamp, Level (color-coded), Message
   - Fetches from `GET /logs/device/{deviceId}?limit=10`
4. ⬜ Build 60-second live chart component
   - Simple line chart using Recharts
   - X-axis: Time (last 60 seconds)
   - Y-axis: Counter value
   - Auto-updates every 5 seconds
   - Shows per channel (multi-line if multiple channels)
   - Data from `/data/latest/{deviceId}` (store last 12 readings client-side)
5. ⬜ Add contextual diagnosis logic
   - If ping works + Modbus fails → "Firewall blocking port 502"
   - If ping fails → "Network cable disconnected or device powered off"
   - If both work but high failure rate → "Intermittent network issues"
6. ⬜ Implement restart device functionality
   - [↻ Restart] button calls `POST /devices/{deviceId}/restart`
   - Show loading spinner during restart
   - Show success/error message

**API Integration** (NEW - Backend needs to implement):
- `POST /tools/ping` - Ping test for device IP
- `POST /tools/modbus-test` - Test Modbus read
- `GET /logs/device/{deviceId}?limit=10` - Recent logs for device

**Deliverables**:
- Click device row → Inline panel expands
- See last 10 logs for THIS device
- Test ping + Modbus with one click
- See 60-second live chart (verify counter working)
- Clear diagnosis message
- Restart device from UI

**Files Created**:
```
src/components/dashboard/
├── TroubleshootingPanel.tsx
├── DeviceLogs.tsx
├── LiveChart.tsx
└── TestButtons.tsx
```

---

### Phase 4: Configuration - Device Management (Week 4)
**Goal**: Add/edit devices via forms, manage API keys

**Tasks**:
1. ⬜ Create Configuration page with 3 tabs
   - Devices tab
   - API Keys tab
   - Advanced tab
2. ⬜ Build Devices tab
   - Table of all Modbus devices
   - Table of all MQTT devices
   - [+ Add Modbus] [+ Add MQTT] [🔍 Scan LAN] buttons
3. ⬜ Build Add Modbus Device form modal
   - Basic info (Device ID, Name, Model Type)
   - Network settings (IP, Port, Unit ID)
   - [Test Connection] button
   - Polling settings (Poll Interval, Timeout, Max Retries)
   - Channel configuration (auto-filled based on model)
   - Native HTML5 validation (no Zod)
   - Save triggers `POST /config/devices`
4. ⬜ Build Edit Modbus Device form modal
   - Same as Add, but pre-filled with existing values
   - Delete button (with confirmation)
   - Save triggers `PUT /config/devices/{id}`
5. ⬜ Build Add/Edit MQTT Device form modal
   - Device ID, Name, Enabled
   - Topics (array input with + Add Topic button)
   - Format (JSON/Binary/CSV dropdown)
   - Data Type (UInt32/Int16/Float32 dropdown)
   - JsonPath fields (for JSON format)
6. ⬜ Build Network Scanner modal
   - IP range inputs (from/to)
   - Port input (default 502)
   - [Start Scan] button
   - Progress bar
   - Results table (IP, Model, Response time, Status)
   - Checkboxes to select devices
   - [Add Selected] button
   - Calls `POST /tools/network-scan`
7. ⬜ Build API Keys tab
   - Table of keys (ID, Name, Key masked, Expires, Actions)
   - [+ Add Key] button
   - Edit/Delete buttons
   - Hot-reload indicator (✅ Changes apply automatically)
   - Save triggers `PUT /config/apikeys`
8. ⬜ Build Advanced tab
   - Shows current DB, MQTT broker settings (read-only)
   - [📥 Download Config] button → `GET /config/export`
   - [📤 Upload Config] button → `POST /config/import`
   - Link to Grafana dashboard
   - [📥 Download Logs] button

**API Integration** (NEW - Backend needs to implement):
- `GET /config/devices` - Get device configs
- `POST /config/devices` - Add new device
- `PUT /config/devices/{id}` - Update device
- `DELETE /config/devices/{id}` - Delete device
- `POST /config/test-connection` - Test device connectivity
- `POST /tools/network-scan` - Scan IP range
- `GET /config/apikeys` - Get API keys
- `PUT /config/apikeys` - Update API keys
- `GET /config/export` - Download full config
- `POST /config/import` - Upload config

**Deliverables**:
- Add new Modbus device via form
- Edit existing device
- Delete device with confirmation
- Test connection before saving
- Network scanner finds devices on LAN
- API keys can be managed with hot-reload
- Advanced users can download/upload JSON

**Files Created**:
```
src/components/configuration/
├── ConfigurationPage.tsx
├── DevicesTab.tsx
├── ApiKeysTab.tsx
├── AdvancedTab.tsx
├── AddModbusDeviceModal.tsx
├── EditModbusDeviceModal.tsx
├── AddMqttDeviceModal.tsx
├── NetworkScannerModal.tsx
└── ApiKeyForm.tsx
```

---

### Phase 5: Polish & Testing (Week 5)
**Goal**: Production-ready frontend

**Tasks**:
1. ⬜ Add error boundaries
   - Catch React errors gracefully
   - Show fallback UI (not white screen)
   - Log errors to console
2. ⬜ Add loading states
   - Skeleton loaders for tables
   - Spinners for buttons (Test Connection, Restart, Save)
   - Loading overlay for page transitions
3. ⬜ Add empty states
   - "No devices configured" when no devices
   - "No errors in last hour" when troubleshooting
   - "No critical alerts" banner
4. ⬜ Implement offline mode indicator
   - Detect when API is unreachable
   - Show banner: "⚠ API unavailable. Showing cached data."
   - Show last successful sync time
   - Retry button
5. ⬜ Add retry logic for failed API calls
   - React Query automatic retry (3 attempts with exponential backoff)
   - Manual retry button for failed operations
6. ⬜ Accessibility audit
   - Keyboard navigation works (Tab, Enter, Escape)
   - Focus indicators visible
   - ARIA labels on buttons
   - Screen reader friendly
7. ⬜ Responsive design tweaks
   - Works on 1920x1080 (primary target)
   - Works on 1366x768 (laptop)
   - Tables scroll horizontally if needed
   - Modals fit on screen
8. ⬜ Performance optimization
   - Code splitting (lazy load routes)
   - Memoize expensive components (React.memo)
   - Debounce search inputs
   - Optimize bundle size
9. ⬜ Testing
   - Manual testing all workflows
   - Test on factory laptop (poor WiFi, Windows)
   - Test all forms with validation
   - Test error scenarios
10. ⬜ Documentation
    - Update README with frontend setup instructions
    - Add environment variables documentation
    - Add screenshots to docs
    - Document API integration

**Deliverables**:
- No runtime errors in console
- Fast page loads (< 2 seconds)
- Accessible (keyboard navigation works)
- Works offline (shows cached data)
- Graceful error handling
- Professional loading/empty states

**Files Created**:
```
src/components/common/
├── ErrorBoundary.tsx
├── LoadingSpinner.tsx
├── EmptyState.tsx
└── OfflineBanner.tsx

frontend/README.md
```

---

## File Structure (Final)

```
frontend/
├── public/
│   └── favicon.ico
├── src/
│   ├── components/
│   │   ├── ui/                      # shadcn components
│   │   │   ├── button.tsx
│   │   │   ├── table.tsx
│   │   │   ├── dialog.tsx
│   │   │   ├── input.tsx
│   │   │   ├── select.tsx
│   │   │   └── ...
│   │   ├── common/
│   │   │   ├── Layout.tsx
│   │   │   ├── StatusIndicator.tsx
│   │   │   ├── ErrorBoundary.tsx
│   │   │   ├── LoadingSpinner.tsx
│   │   │   ├── EmptyState.tsx
│   │   │   └── OfflineBanner.tsx
│   │   ├── dashboard/
│   │   │   ├── DashboardPage.tsx
│   │   │   ├── DeviceTable.tsx
│   │   │   ├── SystemHealth.tsx
│   │   │   ├── CriticalAlerts.tsx
│   │   │   ├── TroubleshootingPanel.tsx
│   │   │   ├── DeviceLogs.tsx
│   │   │   ├── LiveChart.tsx
│   │   │   └── TestButtons.tsx
│   │   └── configuration/
│   │       ├── ConfigurationPage.tsx
│   │       ├── DevicesTab.tsx
│   │       ├── ApiKeysTab.tsx
│   │       ├── AdvancedTab.tsx
│   │       ├── AddModbusDeviceModal.tsx
│   │       ├── EditModbusDeviceModal.tsx
│   │       ├── AddMqttDeviceModal.tsx
│   │       ├── NetworkScannerModal.tsx
│   │       └── ApiKeyForm.tsx
│   ├── hooks/
│   │   ├── useDevices.ts
│   │   ├── useHealth.ts
│   │   ├── useConfig.ts
│   │   └── useDeviceLogs.ts
│   ├── lib/
│   │   ├── api.ts                   # Axios instance + API functions
│   │   ├── types.ts                 # TypeScript types
│   │   └── utils.ts
│   ├── App.tsx
│   ├── main.tsx
│   └── index.css
├── docs/
│   ├── DEVELOPMENT-PLAN-REVISED.md
│   └── PROGRESS.md
├── .gitignore
├── .env.development
├── .env.production
├── package.json
├── tsconfig.json
├── vite.config.ts
├── tailwind.config.js
├── postcss.config.js
└── README.md
```

---

## Backend API Requirements

### Phase 1-2 (Ready - Existing Endpoints)
- ✅ `GET /health`
- ✅ `GET /health/detailed`
- ✅ `GET /devices`
- ✅ `GET /devices/{id}`
- ✅ `POST /devices/{id}/restart`
- ✅ `GET /data/latest`
- ✅ `GET /data/latest/{deviceId}`
- ✅ `GET /data/stats`

### Phase 3 (Need to Implement)
- ⬜ `POST /tools/ping` - Ping test
- ⬜ `POST /tools/modbus-test` - Test Modbus read
- ⬜ `GET /logs/device/{deviceId}?limit=10` - Recent logs for device

### Phase 4 (Need to Implement)
- ⬜ `GET /config/devices` - Get device configs
- ⬜ `POST /config/devices` - Add device
- ⬜ `PUT /config/devices/{id}` - Update device
- ⬜ `DELETE /config/devices/{id}` - Delete device
- ⬜ `POST /config/test-connection` - Test connection
- ⬜ `POST /tools/network-scan` - Scan network
- ⬜ `GET /config/apikeys` - Get API keys
- ⬜ `PUT /config/apikeys` - Update API keys
- ⬜ `GET /config/export` - Download config
- ⬜ `POST /config/import` - Upload config
- ⬜ `GET /logs/download` - Download log file

**Total New Endpoints**: 13

---

## Development Workflow

### Daily Development
1. Pull latest from `feature/frontend-dashboard`
2. Work on specific task from current phase
3. Test locally (`npm run dev`)
4. Commit with descriptive message
5. Push to feature branch

### Before Moving to Next Phase
- All tasks in current phase complete
- Manual testing passes
- No console errors
- TypeScript compiles without errors
- Commit all changes

### Before Merge to Main
1. All 5 phases complete
2. Backend endpoints implemented
3. Manual testing complete
4. Build succeeds (`npm run build`)
5. Documentation updated
6. PR created and reviewed
7. Squash and merge

---

## Success Criteria

### Phase 1-2 (Dashboard MVP)
- ✅ Dashboard shows all devices in one table
- ✅ Real-time updates (10s polling)
- ✅ Status indicators visible (●/○/⚠)
- ✅ Critical alerts displayed
- ✅ System health summary accurate

### Phase 3 (Inline Troubleshooting)
- ✅ Click device → Troubleshooting panel expands inline
- ✅ Recent logs show last 10 entries for THIS device
- ✅ Ping + Modbus test work with clear results
- ✅ Contextual diagnosis messages displayed
- ✅ 60-second live chart shows counter incrementing
- ✅ Restart device works

### Phase 4 (Configuration)
- ✅ Add Modbus device via form in < 2 minutes
- ✅ Edit existing device
- ✅ Delete device with confirmation
- ✅ Test connection before saving
- ✅ Network scanner finds devices
- ✅ API keys can be rotated (hot-reload works)
- ✅ Download/upload config for advanced users

### Phase 5 (Production Ready)
- ✅ No runtime errors
- ✅ Fast page load (< 2 seconds)
- ✅ Works offline (cached data shown)
- ✅ Accessible (keyboard nav works)
- ✅ Professional looking (shadcn/Tailwind)

---

## Timeline

- **Week 1 (Phase 1)**: Foundation (routing, API, layout)
- **Week 2 (Phase 2)**: Dashboard (device tables, health, alerts)
- **Week 3 (Phase 3)**: Inline troubleshooting (logs, tests, chart)
- **Week 4 (Phase 4)**: Configuration (forms, API keys, advanced)
- **Week 5 (Phase 5)**: Polish (testing, docs, performance)

**Total: 5 weeks to production-ready frontend**

---

## Blockers & Dependencies

### Current Blockers
- None (Phase 1-2 can use existing endpoints)

### Future Blockers
- **Phase 3**: Need `/tools/ping`, `/tools/modbus-test`, `/logs/device/{id}` endpoints
- **Phase 4**: Need `/config/*` endpoints (13 total)

### Mitigation Strategy
- Frontend can mock responses during Phase 1-2
- Backend team implements endpoints during Week 2-3
- Integration testing in Week 4-5

---

## Notes

- **Removed**: Full logs tab, historical charts, MQTT discovery modal, complex config forms
- **Kept**: Inline troubleshooting, 60s live chart, device forms, network scanner
- **Balanced**: Use team's existing skills (shadcn/Tailwind) while avoiding over-engineering
- **User-focused**: One tool for troubleshooting, no context switching

**This is Toyota + User Empathy: Simple features done exceptionally well, focused on real user workflows.**
