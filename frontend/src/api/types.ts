// API Response Types

export interface DeviceHealth {
  deviceId: string
  status: 'Online' | 'Offline' | 'Error'
  lastSeen: string
  errorMessage?: string
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
  database?: {
    connected: boolean
    message?: string
  }
  mqtt?: {
    connected: boolean
    message?: string
  }
}

export interface HealthDetailedResponse extends HealthResponse {
  modbusDevices: DeviceHealth[]
  mqttDevices: DeviceHealth[]
  deadLetterQueue: {
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
