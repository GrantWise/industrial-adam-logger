import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { healthApi, devicesApi, dataApi } from './client'

// Query keys
export const queryKeys = {
  health: ['health'] as const,
  healthDetailed: ['health', 'detailed'] as const,
  devices: ['devices'] as const,
  device: (id: string) => ['devices', id] as const,
  dataLatest: ['data', 'latest'] as const,
  dataLatestByDevice: (deviceId: string) => ['data', 'latest', deviceId] as const,
  dataStats: ['data', 'stats'] as const,
}

// Health hooks
export const useHealth = () =>
  useQuery({
    queryKey: queryKeys.health,
    queryFn: async () => {
      const response = await healthApi.getHealth()
      return response.data
    },
  })

export const useHealthDetailed = (refetchInterval?: number) =>
  useQuery({
    queryKey: queryKeys.healthDetailed,
    queryFn: async () => {
      const response = await healthApi.getDetailedHealth()
      return response.data
    },
    refetchInterval,
  })

// Device hooks
export const useDevices = () =>
  useQuery({
    queryKey: queryKeys.devices,
    queryFn: async () => {
      const response = await devicesApi.getDevices()
      return response.data
    },
  })

export const useDevice = (id: string) =>
  useQuery({
    queryKey: queryKeys.device(id),
    queryFn: async () => {
      const response = await devicesApi.getDevice(id)
      return response.data
    },
    enabled: !!id,
  })

export const useRestartDevice = () => {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (deviceId: string) => devicesApi.restartDevice(deviceId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.devices })
      queryClient.invalidateQueries({ queryKey: queryKeys.healthDetailed })
    },
  })
}

// Data hooks
export const useLatestData = (refetchInterval?: number) =>
  useQuery({
    queryKey: queryKeys.dataLatest,
    queryFn: async () => {
      const response = await dataApi.getLatest()
      return response.data
    },
    refetchInterval,
  })

export const useLatestDataByDevice = (deviceId: string, refetchInterval?: number) =>
  useQuery({
    queryKey: queryKeys.dataLatestByDevice(deviceId),
    queryFn: async () => {
      const response = await dataApi.getLatestByDevice(deviceId)
      return response.data
    },
    enabled: !!deviceId,
    refetchInterval,
  })

export const useDataStats = () =>
  useQuery({
    queryKey: queryKeys.dataStats,
    queryFn: async () => {
      const response = await dataApi.getStats()
      return response.data
    },
  })
