import { useHealthDetailed } from '@/api/hooks'
import { AlertTriangle, XCircle } from 'lucide-react'

export function CriticalAlertsBanner() {
  const { data: health } = useHealthDetailed(10000) // 10s refresh

  if (!health) return null

  const alerts: Array<{ type: 'error' | 'warning'; message: string }> = []

  // Database offline
  if (health.database && !health.database.connected) {
    alerts.push({
      type: 'error',
      message: `Database offline: ${health.database.message || 'Cannot connect to TimescaleDB'}`,
    })
  }

  // MQTT broker offline
  if (health.mqtt && !health.mqtt.connected) {
    alerts.push({
      type: 'warning',
      message: `MQTT broker offline: ${health.mqtt.message || 'Cannot connect to broker'}`,
    })
  }

  // Dead letter queue has pending items
  const dlqCount = health.deadLetterQueue?.pendingCount || 0
  if (dlqCount > 0) {
    alerts.push({
      type: 'warning',
      message: `Dead Letter Queue has ${dlqCount} pending item(s) - data not saved to database`,
    })
  }

  // Offline devices
  const offlineModbus = health.modbusDevices?.filter((d) => d.status === 'Offline') || []
  const offlineMqtt = health.mqttDevices?.filter((d) => d.status === 'Offline') || []
  const offlineDevices = [...offlineModbus, ...offlineMqtt]

  if (offlineDevices.length > 0) {
    alerts.push({
      type: 'warning',
      message: `${offlineDevices.length} device(s) offline: ${offlineDevices
        .slice(0, 3)
        .map((d) => d.deviceId)
        .join(', ')}${offlineDevices.length > 3 ? '...' : ''}`,
    })
  }

  // Error devices
  const errorModbus = health.modbusDevices?.filter((d) => d.status === 'Error') || []
  const errorMqtt = health.mqttDevices?.filter((d) => d.status === 'Error') || []
  const errorDevices = [...errorModbus, ...errorMqtt]

  if (errorDevices.length > 0) {
    alerts.push({
      type: 'error',
      message: `${errorDevices.length} device(s) in error state: ${errorDevices
        .slice(0, 3)
        .map((d) => d.deviceId)
        .join(', ')}${errorDevices.length > 3 ? '...' : ''}`,
    })
  }

  if (alerts.length === 0) return null

  return (
    <div className="space-y-2">
      {alerts.map((alert, idx) => (
        <div
          key={idx}
          className={`flex items-start gap-3 p-3 rounded-lg border ${
            alert.type === 'error'
              ? 'bg-red-50 border-red-200 text-red-800'
              : 'bg-yellow-50 border-yellow-200 text-yellow-800'
          }`}
        >
          {alert.type === 'error' ? (
            <XCircle className="w-5 h-5 flex-shrink-0 mt-0.5" />
          ) : (
            <AlertTriangle className="w-5 h-5 flex-shrink-0 mt-0.5" />
          )}
          <span className="text-sm font-medium">{alert.message}</span>
        </div>
      ))}
    </div>
  )
}
