import { CriticalAlertsBanner } from '@/components/CriticalAlertsBanner'
import { SystemHealthSummary } from '@/components/SystemHealthSummary'
import { DeviceTable } from '@/components/DeviceTable'

export default function Dashboard() {
  return (
    <div className="space-y-4">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
        <p className="text-sm text-gray-600 mt-1">
          Real-time device monitoring and system status
        </p>
      </div>

      {/* Critical Alerts */}
      <CriticalAlertsBanner />

      {/* System Health Summary */}
      <SystemHealthSummary />

      {/* Device Table */}
      <div>
        <h2 className="text-lg font-semibold text-gray-900 mb-3">Devices</h2>
        <DeviceTable />
      </div>
    </div>
  )
}
