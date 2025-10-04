import { useState } from 'react'
import { Activity, AlertCircle, RotateCw } from 'lucide-react'
import { LiveChart } from './LiveChart'
import { RecentLogs } from './RecentLogs'
import { cn } from '@/lib/utils'

interface DeviceTroubleshootingPanelProps {
  deviceId: string
}

export function DeviceTroubleshootingPanel({ deviceId }: DeviceTroubleshootingPanelProps) {
  const [pingStatus, setPingStatus] = useState<'idle' | 'testing' | 'success' | 'failed'>('idle')
  const [modbusStatus, setModbusStatus] = useState<'idle' | 'testing' | 'success' | 'failed'>('idle')

  const handlePing = async () => {
    setPingStatus('testing')
    // TODO: Call /tools/ping endpoint when backend ready
    setTimeout(() => setPingStatus('success'), 1000)
  }

  const handleModbusTest = async () => {
    setModbusStatus('testing')
    // TODO: Call /tools/modbus-test endpoint when backend ready
    setTimeout(() => setModbusStatus('success'), 1000)
  }

  const handleRestart = async () => {
    // TODO: Call /devices/{id}/restart endpoint
    console.log('Restart device:', deviceId)
  }

  return (
    <div className="p-4 space-y-4">
      {/* Header with Actions */}
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-gray-900">Device Diagnostics</h3>
        <div className="flex items-center gap-2">
          {/* Test Buttons */}
          <button
            onClick={handlePing}
            disabled={pingStatus === 'testing'}
            className={cn(
              'px-3 py-1.5 text-xs font-medium rounded border transition-colors',
              pingStatus === 'success' && 'border-green-300 bg-green-50 text-green-700',
              pingStatus === 'failed' && 'border-red-300 bg-red-50 text-red-700',
              pingStatus === 'idle' && 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50',
              pingStatus === 'testing' && 'border-gray-300 bg-gray-50 text-gray-500 cursor-wait'
            )}
          >
            {pingStatus === 'testing' ? 'Testing...' : 'Test Ping'}
          </button>

          <button
            onClick={handleModbusTest}
            disabled={modbusStatus === 'testing'}
            className={cn(
              'px-3 py-1.5 text-xs font-medium rounded border transition-colors',
              modbusStatus === 'success' && 'border-green-300 bg-green-50 text-green-700',
              modbusStatus === 'failed' && 'border-red-300 bg-red-50 text-red-700',
              modbusStatus === 'idle' && 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50',
              modbusStatus === 'testing' && 'border-gray-300 bg-gray-50 text-gray-500 cursor-wait'
            )}
          >
            {modbusStatus === 'testing' ? 'Testing...' : 'Test Modbus'}
          </button>

          {/* Restart Button */}
          <button
            onClick={handleRestart}
            className="px-3 py-1.5 text-xs font-medium rounded border border-orange-300 bg-orange-50 text-orange-700 hover:bg-orange-100 transition-colors flex items-center gap-1.5"
          >
            <RotateCw className="w-3 h-3" />
            Restart
          </button>
        </div>
      </div>

      {/* Two-Column Layout: Chart + Logs */}
      <div className="grid grid-cols-2 gap-4">
        {/* Live Chart (60s) */}
        <div className="bg-white border border-gray-200 rounded p-3">
          <div className="flex items-center gap-2 mb-3">
            <Activity className="w-4 h-4 text-blue-600" />
            <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wider">
              Live Data (60s)
            </h4>
          </div>
          <LiveChart deviceId={deviceId} />
        </div>

        {/* Recent Logs */}
        <div className="bg-white border border-gray-200 rounded p-3">
          <div className="flex items-center gap-2 mb-3">
            <AlertCircle className="w-4 h-4 text-gray-600" />
            <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wider">
              Recent Logs (Last 10)
            </h4>
          </div>
          <RecentLogs deviceId={deviceId} />
        </div>
      </div>
    </div>
  )
}
