import { formatDistanceToNow } from 'date-fns'
import { AlertCircle, CheckCircle, Info, XCircle } from 'lucide-react'

interface RecentLogsProps {
  deviceId: string
}

interface LogEntry {
  timestamp: string
  level: 'info' | 'warning' | 'error' | 'success'
  message: string
}

export function RecentLogs({ deviceId: _deviceId }: RecentLogsProps) {
  // TODO: Fetch from /logs/device/{deviceId}?limit=10 when backend ready
  // For now, show placeholder message
  const logs: LogEntry[] = []

  if (logs.length === 0) {
    return (
      <div className="text-center py-8">
        <p className="text-xs text-gray-500">No recent logs available</p>
        <p className="text-xs text-gray-400 mt-1">Logs will appear when backend endpoint is ready</p>
      </div>
    )
  }

  return (
    <div className="space-y-2 max-h-48 overflow-y-auto">
      {logs.map((log, idx) => (
        <div key={idx} className="flex items-start gap-2 text-xs">
          <LogIcon level={log.level} />
          <div className="flex-1 min-w-0">
            <p className="text-gray-900 break-words">{log.message}</p>
            <p className="text-gray-500 mt-0.5">
              {formatDistanceToNow(new Date(log.timestamp), { addSuffix: true })}
            </p>
          </div>
        </div>
      ))}
    </div>
  )
}

function LogIcon({ level }: { level: LogEntry['level'] }) {
  switch (level) {
    case 'success':
      return <CheckCircle className="w-4 h-4 text-green-600 flex-shrink-0 mt-0.5" />
    case 'error':
      return <XCircle className="w-4 h-4 text-red-600 flex-shrink-0 mt-0.5" />
    case 'warning':
      return <AlertCircle className="w-4 h-4 text-yellow-600 flex-shrink-0 mt-0.5" />
    case 'info':
    default:
      return <Info className="w-4 h-4 text-blue-600 flex-shrink-0 mt-0.5" />
  }
}
