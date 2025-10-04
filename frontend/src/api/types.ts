// API Response Types

export interface DeviceHealth {
  deviceId: string
  isConnected: boolean
  lastSuccessfulRead: string | null
  consecutiveFailures: number
  lastError: string | null
  totalReads: number
  successfulReads: number
  successRate: number
  isOffline: boolean
}

export interface DeviceReading {
  deviceId: string
  channelNumber: number
  timestamp: string
  value: number
  quality: 'Good' | 'Uncertain' | 'Bad' | 'Unavailable'
  unit?: string
}

export interface Device {
  deviceId: string
  ipAddress?: string
  port?: number
  pollIntervalMs?: number
  channels?: DeviceChannel[]
  topics?: string[]
  format?: 'Json' | 'Binary' | 'Csv'
  dataType?: string
}

export interface DeviceChannel {
  channelNumber: number
  name?: string
  startRegister: number
  registerCount: number
  registerType?: string
  dataType: string
  scaleFactor?: number
  unit?: string
}

export interface HealthResponse {
  status: string
  timestamp: string
  components?: {
    database?: {
      status: string
      connected: boolean
    }
    mqtt?: {
      status: string
      connected: boolean
    }
    service?: {
      status: string
      isRunning: boolean
      startTime: string
      uptime: string
    }
    devices?: {
      status: string
      total: number
      connected: number
      details: Record<string, DeviceHealthDetail>
    }
  }
}

export interface DeviceHealthDetail {
  deviceId: string
  isConnected: boolean
  lastSuccessfulRead: string | null
  consecutiveFailures: number
  lastError: string | null
  totalReads: number
  successfulReads: number
  successRate: number
  isOffline: boolean
}

export interface HealthDetailedResponse extends HealthResponse {
  modbusDevices?: DeviceHealth[]
  mqttDevices?: DeviceHealth[]
  deadLetterQueue?: {
    pendingCount: number
    oldestTimestamp?: string
  }
}

export interface DataStatsResponse {
  totalReadings: number
  readingsToday: number
  activeDevices: number
  errorRate: number
}
