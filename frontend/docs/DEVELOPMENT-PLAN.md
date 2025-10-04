# Frontend Development Plan - Industrial ADAM Logger

## Project Overview

**Goal**: Build a Toyota-grade, information-dense frontend for commissioning, monitoring, and troubleshooting industrial ADAM devices.

**Design Philosophy**:
- Dense, not pretty (like Bloomberg Terminal, not Stripe)
- Forms, not wizards
- Tables everywhere
- Fast, boring, reliable

**Tech Stack**:
- React 18
- TypeScript
- Vite (build tool)
- shadcn/ui (component library)
- Tailwind CSS (utility-first styling)
- React Query (server state)
- React Router (routing)
- Zod (validation)
- Recharts (time-series charts)

---

## Development Phases

### Phase 1: Foundation (Week 1)
**Goal**: Basic app structure, routing, API integration

**Tasks**:
1. ✅ Create feature branch (`feature/frontend-dashboard`)
2. ✅ Initialize frontend directory structure
3. ⬜ Set up Vite + React + TypeScript project
4. ⬜ Configure Tailwind CSS
5. ⬜ Install and configure shadcn/ui
6. ⬜ Set up React Router (3 routes: Dashboard, Configuration, Logs)
7. ⬜ Create API client (axios + TypeScript types)
8. ⬜ Set up React Query for data fetching
9. ⬜ Create basic layout component (header, nav tabs, content area)
10. ⬜ Test API connectivity to backend (`GET /health`)

**Deliverables**:
- Working dev server (`npm run dev`)
- Basic 3-tab navigation (Dashboard, Configuration, Logs)
- API health check working
- TypeScript configured with strict mode
- Tailwind working (test with utility classes)

---

### Phase 2: Dashboard - Core Monitoring (Week 2)
**Goal**: Dense device table with real-time status

**Tasks**:
1. ⬜ Create Dashboard page component
2. ⬜ Build Modbus device table component
   - Dense table layout
   - Status indicators (●/○/⚠)
   - Inline actions ([↻][✎][🗑])
   - Auto-refresh every 5 seconds
3. ⬜ Build MQTT device table component
   - Similar to Modbus table
   - Topic display
   - Message rate stats
4. ⬜ Create system health summary component
   - Database status
   - Service uptime
   - Dead Letter Queue size
5. ⬜ Build events log component (last 20 events)
6. ⬜ Create critical alerts banner
7. ⬜ Implement auto-refresh logic (React Query polling)
8. ⬜ Add pause/resume auto-refresh controls

**API Integration**:
- `GET /health/detailed`
- `GET /devices`
- `GET /data/latest`
- `GET /data/stats`

**Deliverables**:
- Dashboard shows all devices
- Real-time status updates (5s polling)
- Critical alerts visible
- Responsive to window resize

---

### Phase 3: Configuration - Device Management (Week 3)
**Goal**: Form-based device configuration (no raw JSON)

**Tasks**:
1. ⬜ Create Configuration page with 5 sub-tabs
   - Devices
   - Database
   - MQTT Broker
   - API Keys
   - System
2. ⬜ Build Devices tab
   - Table of all Modbus + MQTT devices
   - Add device modal (form-based)
   - Edit device modal (form-based)
   - Delete device with confirmation
   - Enable/disable device toggle
3. ⬜ Build Add/Edit Modbus Device form
   - Basic info (ID, Name, Model, IP, Port)
   - Network settings with validation
   - Test connection button
   - Channel configuration (nested table)
   - Model-based presets (ADAM-6051 → auto-fill channels)
4. ⬜ Build Add/Edit MQTT Device form
   - Topic subscription
   - Format selection (JSON/Binary/CSV)
   - JsonPath field mapping
   - Preview with real message
5. ⬜ Build Database configuration tab
   - Connection settings form
   - Test connection button
   - Performance settings (batch size, etc.)
6. ⬜ Build MQTT Broker configuration tab
   - Broker connection form
   - QoS, keep-alive settings
   - Test connection button
7. ⬜ Build API Keys tab
   - Table of keys
   - Add/Edit/Delete keys
   - Show/hide key toggle
   - Regenerate key button
   - Hot-reload indicator
8. ⬜ Build System Settings tab
   - Log level selector
   - CORS origins management
   - Backup management

**Form Validation**:
- Use Zod schemas for all forms
- Real-time validation (on blur)
- Clear error messages
- Prevent invalid submissions

**API Integration** (NEW - Backend needs to implement):
- `GET /config/devices` - Get device list
- `PUT /config/devices` - Update devices
- `POST /config/devices` - Add device
- `DELETE /config/devices/{id}` - Delete device
- `POST /config/test-connection` - Test device connection
- `GET /config/database` - Get DB config
- `PUT /config/database` - Update DB config
- Similar for MQTT, API Keys, System

**Deliverables**:
- Add Modbus device via form (no JSON editing)
- Edit existing device
- Delete device
- Test connection before saving
- Validation prevents invalid config
- Save triggers service restart (with warning)

---

### Phase 4: Advanced Features - Discovery & Troubleshooting (Week 4)
**Goal**: Network discovery, MQTT topic discovery, inline troubleshooting

**Tasks**:
1. ⬜ Build Network Scanner modal
   - IP range input
   - Scan progress bar
   - Results table (IP, Model, Response time)
   - Select devices to add
   - Bulk add selected devices
2. ⬜ Build MQTT Topic Discovery modal
   - Listen mode (subscribe to #)
   - Live message viewer
   - Topic list with format detection
   - Message rate display
   - One-click add device from topic
3. ⬜ Build Inline troubleshooting panel (Dashboard)
   - Expand device row when offline
   - Show last error, consecutive failures
   - Ping test button
   - Modbus test button
   - Contextual diagnosis ("Ping works, Modbus fails → Firewall")
4. ⬜ Build Device Details modal
   - Overview tab (status, uptime, success rate)
   - Diagnostics tab (ping, traceroute, register dump)
   - Connection history
5. ⬜ Build Import/Export modals
   - Export selected devices to JSON/CSV
   - Import from JSON/CSV
   - Preview before import
   - Validation of imported config

**API Integration** (NEW - Backend needs to implement):
- `POST /tools/network-scan` - Scan IP range for Modbus devices
- `POST /tools/mqtt-discover` - Listen to MQTT topics (WebSocket?)
- `POST /tools/ping` - Ping test for device
- `POST /tools/modbus-test` - Test Modbus read
- `POST /config/import` - Validate and import config
- `GET /config/export` - Export current config

**Deliverables**:
- Scan network, find 10 devices, add all in < 1 minute
- Discover MQTT topics, add devices with auto-config
- Diagnose offline device in < 30 seconds
- Import config from CSV/JSON

---

### Phase 5: Logs & Diagnostics (Week 5)
**Goal**: Error-first log viewer with context-aware navigation

**Tasks**:
1. ⬜ Create Logs page component
2. ⬜ Build error-first summary view
   - Recent errors (last hour)
   - Recent warnings
   - Suggested actions
3. ⬜ Build full logs table
   - Dense table layout
   - Color-coded levels (ERROR=red, WARN=yellow)
   - Expand arrow for details
   - Pagination (load more)
4. ⬜ Build expandable log entry
   - Full error message
   - Stack trace
   - Context object (device ID, IP, settings)
   - Quick actions (View Device, Restart, Copy)
5. ⬜ Build filter panel
   - Level checkboxes (ERROR, WARN, INFO, etc.)
   - Source checkboxes (ModbusService, TimescaleStorage, etc.)
   - Device dropdown
   - Time range picker
   - Text search
6. ⬜ Build quick filter presets
   - Errors Only
   - Device: {deviceId} (pre-filled from Dashboard)
   - Last 10 Minutes
   - Database Only
7. ⬜ Implement context-aware navigation
   - Click "View Logs" from Dashboard → Pre-filter to device
   - Click error alert → Jump to relevant logs
8. ⬜ Build real-time tail mode
   - Auto-scroll toggle
   - Pause/Resume
   - WebSocket or polling
9. ⬜ Build export logs modal
   - Format selection (CSV/JSON/Text)
   - Include/exclude options
   - Download file

**API Integration**:
- `GET /logs` - Query logs with filters
- `GET /logs/summary` - Recent error/warning counts
- `GET /logs/export` - Export logs
- `GET /logs/live` - WebSocket/SSE for tail mode

**Deliverables**:
- Open Logs tab → See errors first
- Click device on Dashboard → Logs pre-filtered
- Expand error → See full context
- Export last 24h of errors to CSV
- Real-time tail mode works

---

### Phase 6: Charts & Data Visualization (Week 6)
**Goal**: Time-series charts for historical data

**Tasks**:
1. ⬜ Build time-series chart component (Recharts)
   - Line chart for counter values
   - Multi-channel support (multiple lines)
   - Zoom/pan controls
   - Tooltip with exact values
   - Data quality indicators
2. ⬜ Add chart to Device Details modal
   - Last 1h/6h/24h selector
   - One line per channel
   - Export to CSV button
3. ⬜ Add mini chart preview to Dashboard
   - Sparkline-style small chart
   - Click to expand to full modal
4. ⬜ Build historical data query
   - Date range picker
   - Channel selector
   - Aggregation options (raw, avg, min, max)

**API Integration** (NEW - Backend needs to implement):
- `GET /data/history/{deviceId}?from=&to=&channels=` - Historical time-series data
- Return format: `[{ timestamp, channel, value, quality }]`

**Critical Requirement**:
- NEVER show synthetic data without warning
- Display data quality indicators (Good/Degraded/Bad/Unavailable)
- Show "No Data" instead of fake values

**Deliverables**:
- View last 24h of counter data in chart
- Zoom to specific time range
- Export chart data to CSV
- Clear quality indicators

---

### Phase 7: Polish & Testing (Week 7)
**Goal**: Production-ready frontend

**Tasks**:
1. ⬜ Error boundaries (catch React errors gracefully)
2. ⬜ Loading states (skeletons, spinners)
3. ⬜ Empty states ("No devices configured")
4. ⬜ Offline mode (show cached data, warn API unavailable)
5. ⬜ Retry failed API calls (3 attempts with backoff)
6. ⬜ Accessibility audit
   - Keyboard navigation
   - Screen reader friendly
   - High contrast mode
7. ⬜ Responsive design tweaks
   - Works on 1920x1080 (primary)
   - Works on 1366x768 (laptop)
   - Tables scroll horizontally if needed
8. ⬜ Performance optimization
   - Code splitting (lazy load routes)
   - Memoize expensive components
   - Debounce search inputs
9. ⬜ E2E testing (Playwright)
   - Add device workflow
   - Edit device workflow
   - View logs workflow
10. ⬜ Documentation
    - Update README with frontend setup
    - Add screenshots to docs
    - Document environment variables

**Deliverables**:
- No runtime errors
- Fast page loads (< 2s)
- Accessible (keyboard nav works)
- Works offline (shows cached data)
- E2E tests pass

---

## File Structure

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
│   │   │   └── ...
│   │   ├── layout/
│   │   │   ├── Layout.tsx           # Main layout with tabs
│   │   │   ├── Header.tsx
│   │   │   └── NavTabs.tsx
│   │   ├── dashboard/
│   │   │   ├── ModbusDeviceTable.tsx
│   │   │   ├── MqttDeviceTable.tsx
│   │   │   ├── SystemHealth.tsx
│   │   │   ├── EventsLog.tsx
│   │   │   ├── CriticalAlerts.tsx
│   │   │   └── TroubleshootingPanel.tsx
│   │   ├── configuration/
│   │   │   ├── ConfigurationPage.tsx
│   │   │   ├── DevicesTab.tsx
│   │   │   ├── DatabaseTab.tsx
│   │   │   ├── MqttBrokerTab.tsx
│   │   │   ├── ApiKeysTab.tsx
│   │   │   ├── SystemTab.tsx
│   │   │   ├── AddModbusDeviceModal.tsx
│   │   │   ├── EditModbusDeviceModal.tsx
│   │   │   ├── AddMqttDeviceModal.tsx
│   │   │   ├── NetworkScannerModal.tsx
│   │   │   └── MqttDiscoveryModal.tsx
│   │   ├── logs/
│   │   │   ├── LogsPage.tsx
│   │   │   ├── LogsTable.tsx
│   │   │   ├── LogEntry.tsx
│   │   │   ├── FilterPanel.tsx
│   │   │   ├── QuickFilters.tsx
│   │   │   └── ExportLogsModal.tsx
│   │   ├── charts/
│   │   │   ├── TimeSeriesChart.tsx
│   │   │   └── MiniChart.tsx
│   │   └── common/
│   │       ├── StatusIndicator.tsx   # ●/○/⚠
│   │       ├── DeviceCard.tsx
│   │       ├── ErrorBoundary.tsx
│   │       └── LoadingSpinner.tsx
│   ├── pages/
│   │   ├── DashboardPage.tsx
│   │   ├── ConfigurationPage.tsx
│   │   └── LogsPage.tsx
│   ├── hooks/
│   │   ├── useDevices.ts            # React Query hook
│   │   ├── useHealth.ts
│   │   ├── useLogs.ts
│   │   ├── useConfig.ts
│   │   └── useWebSocket.ts
│   ├── lib/
│   │   ├── api.ts                   # Axios instance + API functions
│   │   ├── types.ts                 # TypeScript types
│   │   ├── schemas.ts               # Zod schemas
│   │   └── utils.ts                 # Utility functions
│   ├── App.tsx
│   ├── main.tsx
│   └── index.css                    # Tailwind imports
├── docs/
│   ├── DEVELOPMENT-PLAN.md          # This file
│   ├── PROGRESS.md                  # Progress tracking
│   └── API-INTEGRATION.md           # API endpoints reference
├── .gitignore
├── package.json
├── tsconfig.json
├── vite.config.ts
├── tailwind.config.js
├── postcss.config.js
└── README.md
```

---

## TypeScript Type Definitions

**Core types** (from backend API):

```typescript
// Device types
type DeviceStatus = 'online' | 'offline' | 'degraded';
type DataQuality = 'Good' | 'Degraded' | 'Bad' | 'Unavailable';

interface DeviceReading {
  deviceId: string;
  channel: number;
  rawValue: number;
  timestamp: string;
  processedValue: number;
  rate: number | null;
  quality: DataQuality;
  unit: string;
  tags: Record<string, string>;
}

interface DeviceHealth {
  deviceId: string;
  isConnected: boolean;
  lastSuccessfulRead: string | null;
  consecutiveFailures: number;
  lastError: string | null;
  totalReads: number;
  successfulReads: number;
  successRate: number;
  isOffline: boolean;
}

interface ModbusDeviceConfig {
  deviceId: string;
  name: string;
  modelType?: string;
  ipAddress: string;
  port: number;
  unitId: number;
  enabled: boolean;
  pollIntervalMs: number;
  timeoutMs: number;
  maxRetries: number;
  keepAlive: boolean;
  channels: ChannelConfig[];
}

interface ChannelConfig {
  channelNumber: number;
  name: string;
  startRegister: number;
  registerCount: 1 | 2;
  registerType: 'HoldingRegister' | 'InputRegister';
  dataType: 'UInt32Counter' | 'Int16' | 'UInt16' | 'Float32' | 'Int32';
  enabled: boolean;
  scaleFactor: number;
  offset: number;
  unit: string;
  minValue?: number;
  maxValue?: number;
  maxChangeRate?: number;
  rateWindowSeconds?: number;
  tags?: Record<string, string>;
}

interface MqttDeviceConfig {
  deviceId: string;
  name: string;
  modelType?: string;
  enabled: boolean;
  topics: string[];
  format: 'Json' | 'Binary' | 'Csv';
  dataType: 'UInt32' | 'Int16' | 'UInt16' | 'Float32' | 'Float64';
  qosLevel?: 0 | 1 | 2;
  deviceIdJsonPath?: string;
  channelJsonPath?: string;
  valueJsonPath?: string;
  timestampJsonPath?: string;
  scaleFactor?: number;
  unit?: string;
}

// Log types
type LogLevel = 'Trace' | 'Debug' | 'Information' | 'Warning' | 'Error' | 'Critical';

interface LogEntry {
  timestamp: string;
  level: LogLevel;
  logger: string;
  message: string;
  exception?: {
    type: string;
    message: string;
    stackTrace: string;
  };
  context?: Record<string, any>;
}
```

---

## API Client Setup

**Base configuration** (`src/lib/api.ts`):

```typescript
import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';
const API_KEY = import.meta.env.VITE_API_KEY || '';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
    'X-API-Key': API_KEY,
  },
  timeout: 10000,
});

// Add request interceptor for auth
apiClient.interceptors.request.use((config) => {
  // Add API key if not present
  if (!config.headers['X-API-Key']) {
    config.headers['X-API-Key'] = API_KEY;
  }
  return config;
});

// Add response interceptor for errors
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Handle unauthorized (redirect to login or show error)
      console.error('API key invalid or missing');
    }
    return Promise.reject(error);
  }
);
```

**API functions** (`src/lib/api.ts`):

```typescript
// Health endpoints
export const getHealth = () => apiClient.get('/health');
export const getDetailedHealth = () => apiClient.get('/health/detailed');

// Device endpoints
export const getDevices = () => apiClient.get<Record<string, DeviceHealth>>('/devices');
export const getDevice = (deviceId: string) => apiClient.get<DeviceHealth>(`/devices/${deviceId}`);
export const restartDevice = (deviceId: string) => apiClient.post(`/devices/${deviceId}/restart`);

// Data endpoints
export const getLatestData = () => apiClient.get<LatestDataResponse>('/data/latest');
export const getLatestDataForDevice = (deviceId: string) => apiClient.get(`/data/latest/${deviceId}`);
export const getDataStats = () => apiClient.get<DataStatsResponse>('/data/stats');

// Logs endpoints
export const getLogs = (params: LogQueryParams) => apiClient.get<LogsResponse>('/logs', { params });
export const getLogsSummary = () => apiClient.get<LogsSummaryResponse>('/logs/summary');
export const exportLogs = (params: LogQueryParams) => apiClient.get('/logs/export', { params });

// Configuration endpoints (NEW - backend needs to implement)
export const getDevicesConfig = () => apiClient.get('/config/devices');
export const updateDevicesConfig = (config: any) => apiClient.put('/config/devices', config);
export const testConnection = (config: any) => apiClient.post('/config/test-connection', config);
```

---

## Environment Variables

**`.env.development`**:
```env
VITE_API_BASE_URL=http://localhost:5000
VITE_API_KEY=IND-ADAM-DEV-2024-abc123def456ghi789
```

**`.env.production`**:
```env
VITE_API_BASE_URL=https://adam-logger.example.com
VITE_API_KEY=your-production-api-key
```

---

## Backend API Endpoints Needed (NEW)

These endpoints DO NOT currently exist in the backend. They need to be implemented:

### Configuration Management
- `GET /config/devices` - Get all device configs
- `PUT /config/devices` - Update all device configs
- `POST /config/devices` - Add new device
- `DELETE /config/devices/{id}` - Delete device
- `POST /config/test-connection` - Test device connection
- `GET /config/database` - Get database config
- `PUT /config/database` - Update database config
- `GET /config/mqtt` - Get MQTT broker config
- `PUT /config/mqtt` - Update MQTT broker config
- `GET /config/apikeys` - Get API keys (masked)
- `PUT /config/apikeys` - Update API keys
- `GET /config/system` - Get system settings
- `PUT /config/system` - Update system settings

### Tools/Utilities
- `POST /tools/network-scan` - Scan IP range for Modbus devices
- `POST /tools/mqtt-discover` - Discover MQTT topics
- `POST /tools/ping` - Ping test
- `POST /tools/modbus-test` - Test Modbus read

### Logs
- `GET /logs` - Query logs (already planned)
- `GET /logs/summary` - Recent error counts
- `GET /logs/export` - Export logs
- `GET /logs/live` - WebSocket/SSE for tail mode

### Historical Data
- `GET /data/history/{deviceId}` - Time-series historical data

**Note**: Phase 1-2 can proceed with existing endpoints. Configuration endpoints needed for Phase 3.

---

## Development Workflow

### Daily Development
1. Pull latest from `feature/frontend-dashboard`
2. Create sub-branch for specific feature (e.g., `feature/frontend-dashboard-device-table`)
3. Develop feature
4. Test locally (`npm run dev`)
5. Commit with descriptive message
6. Push to sub-branch
7. Merge sub-branch back to `feature/frontend-dashboard`

### Testing
- Manual testing in browser (primary)
- TypeScript compilation (`npm run build`)
- Lint (`npm run lint`)
- E2E tests (Playwright) - Phase 7

### Before Merge to Main
1. All phases complete
2. E2E tests pass
3. Build succeeds (`npm run build`)
4. Documentation updated
5. Screenshots added to docs
6. PR reviewed
7. Squash and merge

---

## Success Criteria

**Phase 1-2 (Dashboard MVP)**:
- ✅ Dashboard shows all devices in table
- ✅ Real-time updates (5s polling)
- ✅ Status indicators visible (●/○/⚠)
- ✅ Critical alerts displayed
- ✅ Technician can see system health at a glance

**Phase 3 (Configuration MVP)**:
- ✅ Add Modbus device via form (not JSON)
- ✅ Edit existing device
- ✅ Delete device
- ✅ Test connection before saving
- ✅ Validation prevents invalid config

**Phase 4-5 (Advanced Features)**:
- ✅ Network scan finds devices automatically
- ✅ MQTT topic discovery works
- ✅ Inline troubleshooting guides to solution
- ✅ Logs show errors first
- ✅ Context-aware navigation (Dashboard → Logs for device)

**Phase 6-7 (Production Ready)**:
- ✅ Charts show historical data
- ✅ No runtime errors
- ✅ Works offline (cached data)
- ✅ Accessible (keyboard nav)
- ✅ Fast (< 2s page load)

---

## Timeline

- **Week 1**: Foundation (routing, API, layout)
- **Week 2**: Dashboard (device tables, health, events)
- **Week 3**: Configuration (forms for devices, DB, MQTT)
- **Week 4**: Advanced (network scan, MQTT discovery, troubleshooting)
- **Week 5**: Logs (error-first viewer, filters, export)
- **Week 6**: Charts (time-series, historical data)
- **Week 7**: Polish (testing, docs, performance)

**Total: 7 weeks to production-ready frontend**

---

## Notes

- Backend API endpoints for configuration management DO NOT exist yet
- Need to implement `/config/*` endpoints during Phase 3
- Phase 1-2 can proceed with existing `/health`, `/devices`, `/data` endpoints
- MQTT topic discovery may require WebSocket support
- Historical data endpoint needs TimescaleDB query implementation

**This is a Toyota build: Simple features, done exceptionally well, one phase at a time.**
