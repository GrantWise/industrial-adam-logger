import { useHealth } from '@/api/hooks'

export default function Dashboard() {
  const { data: health, isLoading, error } = useHealth()

  return (
    <div>
      <h1 className="text-2xl font-bold mb-4">Dashboard</h1>
      <p className="text-gray-600 mb-6">Device monitoring and status</p>

      <div className="bg-white rounded-lg shadow p-6">
        <h2 className="text-lg font-semibold mb-4">API Connection Test</h2>

        {isLoading && <p className="text-gray-500">Connecting to API...</p>}

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-800 p-4 rounded">
            <p className="font-semibold">Connection Failed</p>
            <p className="text-sm mt-1">
              Cannot connect to backend API. Make sure the backend is running on port 5000.
            </p>
            <p className="text-xs mt-2 text-red-600">
              Error: {error instanceof Error ? error.message : 'Unknown error'}
            </p>
          </div>
        )}

        {health && (
          <div className="bg-green-50 border border-green-200 text-green-800 p-4 rounded">
            <p className="font-semibold">✓ API Connected</p>
            <div className="text-sm mt-2 space-y-1">
              <p>Status: {health.status}</p>
              <p>Timestamp: {new Date(health.timestamp).toLocaleString()}</p>
              {health.database && (
                <p>
                  Database: {health.database.connected ? '✓ Connected' : '✗ Disconnected'}
                </p>
              )}
              {health.mqtt && (
                <p>MQTT: {health.mqtt.connected ? '✓ Connected' : '✗ Disconnected'}</p>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
