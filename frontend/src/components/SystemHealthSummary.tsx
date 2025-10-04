import { useHealthDetailed } from '@/api/hooks'
import { StatusIndicator } from './StatusIndicator'
import { Clock, Database, Activity, AlertCircle } from 'lucide-react'
import { formatDistanceToNow } from 'date-fns'

export function SystemHealthSummary() {
  const { data: health, isLoading, error } = useHealthDetailed(10000) // 10s refresh

  if (isLoading) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-4">
        <p className="text-gray-500">Loading system health...</p>
      </div>
    )
  }

  if (error || !health) {
    return (
      <div className="bg-red-50 rounded-lg border border-red-200 p-4">
        <div className="flex items-center gap-2 text-red-800">
          <AlertCircle className="w-5 h-5" />
          <span className="font-semibold">Cannot connect to backend</span>
        </div>
      </div>
    )
  }

  const dbStatus = health.components?.database?.connected ? 'Online' : 'Offline'
  const mqttStatus = health.components?.mqtt?.connected ? 'Online' : 'Offline'
  const dlqCount = health.deadLetterQueue?.pendingCount || 0
  const totalDevices = health.components?.devices?.total || 0
  const onlineDevices = health.components?.devices?.connected || 0

  return (
    <div className="bg-white rounded-lg border border-gray-200 p-4">
      <h2 className="text-sm font-semibold text-gray-900 mb-3">System Health</h2>
      <div className="grid grid-cols-4 gap-4">
        {/* Database */}
        <div className="flex items-center gap-3">
          <Database className="w-5 h-5 text-gray-400" />
          <div>
            <div className="text-xs text-gray-500">Database</div>
            <StatusIndicator status={dbStatus} />
          </div>
        </div>

        {/* MQTT Broker */}
        <div className="flex items-center gap-3">
          <Activity className="w-5 h-5 text-gray-400" />
          <div>
            <div className="text-xs text-gray-500">MQTT Broker</div>
            <StatusIndicator status={mqttStatus} />
          </div>
        </div>

        {/* Dead Letter Queue */}
        <div className="flex items-center gap-3">
          <AlertCircle className="w-5 h-5 text-gray-400" />
          <div>
            <div className="text-xs text-gray-500">Dead Letter Queue</div>
            <div className="text-sm font-medium">
              {dlqCount > 0 ? (
                <span className="text-red-600">{dlqCount} pending</span>
              ) : (
                <span className="text-green-600">0 pending</span>
              )}
            </div>
          </div>
        </div>

        {/* Uptime */}
        <div className="flex items-center gap-3">
          <Clock className="w-5 h-5 text-gray-400" />
          <div>
            <div className="text-xs text-gray-500">Devices</div>
            <div className="text-sm font-medium">
              {onlineDevices}/{totalDevices} online
            </div>
          </div>
        </div>
      </div>

      {/* Last updated */}
      <div className="mt-3 pt-3 border-t border-gray-100 text-xs text-gray-500">
        Updated {formatDistanceToNow(new Date(health.timestamp), { addSuffix: true })}
      </div>
    </div>
  )
}
