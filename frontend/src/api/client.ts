import axios from 'axios'
import type {
  HealthResponse,
  HealthDetailedResponse,
  Device,
  DeviceReading,
  DataStatsResponse,
} from './types'

const API_BASE_URL = import.meta.env.VITE_API_URL || '/api'

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Health endpoints
export const healthApi = {
  getHealth: () => apiClient.get<HealthResponse>('/health'),
  getDetailedHealth: () => apiClient.get<HealthDetailedResponse>('/health/detailed'),
}

// Device endpoints
export const devicesApi = {
  getDevices: () => apiClient.get<Device[]>('/devices'),
  getDevice: (id: string) => apiClient.get<Device>(`/devices/${id}`),
  restartDevice: (id: string) => apiClient.post(`/devices/${id}/restart`),
}

// Data endpoints
export const dataApi = {
  getLatest: () => apiClient.get<DeviceReading[]>('/data/latest'),
  getLatestByDevice: (deviceId: string) =>
    apiClient.get<DeviceReading[]>(`/data/latest/${deviceId}`),
  getStats: () => apiClient.get<DataStatsResponse>('/data/stats'),
}

export default apiClient
