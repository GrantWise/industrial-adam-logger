import { useHealthDetailed, useLatestData } from '@/api/hooks'
import { StatusIndicator } from './StatusIndicator'
import type { DeviceReading } from '@/api/types'
import { formatDistanceToNow } from 'date-fns'
import { Router, Wifi } from 'lucide-react'

export function DeviceTable() {
  const { data: health } = useHealthDetailed(10000) // 10s refresh
  const { data: latestData } = useLatestData(10000) // 10s refresh

  if (!health) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-8 text-center">
        <p className="text-gray-500">Loading devices...</p>
      </div>
    )
  }

  // Get devices from health.components.devices.details
  const deviceDetails = health.components?.devices?.details || {}
  const allDevices = Object.values(deviceDetails).map((d) => ({
    deviceId: d.deviceId,
    status: d.isOffline ? ('Offline' as const) : d.isConnected ? ('Online' as const) : ('Error' as const),
    lastSeen: d.lastSuccessfulRead,
    errorMessage: d.lastError || undefined,
    type: 'Modbus' as const, // For now, assume all are Modbus (can be enhanced later)
  }))

  if (allDevices.length === 0) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-8 text-center">
        <p className="text-gray-500">No devices configured</p>
      </div>
    )
  }

  // Get latest reading for a device
  const getLatestReading = (deviceId: string): DeviceReading | undefined => {
    return latestData?.find((r) => r.deviceId === deviceId)
  }

  return (
    <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
      <table className="w-full">
        <thead className="bg-gray-50 border-b border-gray-200">
          <tr>
            <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
              Type
            </th>
            <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
              Device ID
            </th>
            <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
              Status
            </th>
            <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
              Last Seen
            </th>
            <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
              Latest Value
            </th>
            <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 uppercase tracking-wider">
              Data Quality
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {allDevices.map((device) => {
            const latestReading = getLatestReading(device.deviceId)
            return (
              <tr
                key={device.deviceId}
                className="hover:bg-gray-50 transition-colors cursor-pointer"
              >
                {/* Type */}
                <td className="px-4 py-3 whitespace-nowrap">
                  <div className="flex items-center gap-2">
                    {device.type === 'Modbus' ? (
                      <Router className="w-4 h-4 text-blue-600" />
                    ) : (
                      <Wifi className="w-4 h-4 text-purple-600" />
                    )}
                    <span className="text-sm font-medium text-gray-700">{device.type}</span>
                  </div>
                </td>

                {/* Device ID */}
                <td className="px-4 py-3 whitespace-nowrap">
                  <span className="text-sm font-mono text-gray-900">{device.deviceId}</span>
                </td>

                {/* Status */}
                <td className="px-4 py-3 whitespace-nowrap">
                  <StatusIndicator status={device.status} />
                </td>

                {/* Last Seen */}
                <td className="px-4 py-3 whitespace-nowrap">
                  <span className="text-sm text-gray-600">
                    {device.lastSeen
                      ? formatDistanceToNow(new Date(device.lastSeen), { addSuffix: true })
                      : 'Never'}
                  </span>
                </td>

                {/* Latest Value */}
                <td className="px-4 py-3 whitespace-nowrap">
                  {latestReading ? (
                    <div className="text-sm">
                      <span className="font-medium text-gray-900">{latestReading.value}</span>
                      {latestReading.unit && (
                        <span className="text-gray-500 ml-1">{latestReading.unit}</span>
                      )}
                    </div>
                  ) : (
                    <span className="text-sm text-gray-400">No data</span>
                  )}
                </td>

                {/* Data Quality */}
                <td className="px-4 py-3 whitespace-nowrap">
                  {latestReading ? (
                    <QualityBadge quality={latestReading.quality} />
                  ) : (
                    <span className="text-sm text-gray-400">—</span>
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

function QualityBadge({ quality }: { quality: string }) {
  const styles = {
    Good: 'bg-green-100 text-green-800 border-green-200',
    Uncertain: 'bg-yellow-100 text-yellow-800 border-yellow-200',
    Bad: 'bg-red-100 text-red-800 border-red-200',
    Unavailable: 'bg-gray-100 text-gray-800 border-gray-200',
  }

  const style = styles[quality as keyof typeof styles] || styles.Unavailable

  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border ${style}`}>
      {quality}
    </span>
  )
}
