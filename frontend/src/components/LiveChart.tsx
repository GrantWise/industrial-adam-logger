import { useEffect, useState } from 'react'
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import { useLatestData } from '@/api/hooks'
import { format } from 'date-fns'

interface LiveChartProps {
  deviceId: string
}

interface DataPoint {
  timestamp: string
  value: number
  quality: string
}

export function LiveChart({ deviceId }: LiveChartProps) {
  const { data: latestData } = useLatestData(10000) // 10s refresh
  const [chartData, setChartData] = useState<DataPoint[]>([])

  useEffect(() => {
    if (!latestData) return

    const deviceReading = latestData.find((r) => r.deviceId === deviceId)
    if (!deviceReading) return

    const now = new Date()
    const newPoint: DataPoint = {
      timestamp: format(now, 'HH:mm:ss'),
      value: deviceReading.value,
      quality: deviceReading.quality,
    }

    setChartData((prev) => {
      const updated = [...prev, newPoint]
      // Keep only last 60 seconds (6 data points at 10s refresh)
      const maxPoints = 6
      return updated.slice(-maxPoints)
    })
  }, [latestData, deviceId])

  if (chartData.length === 0) {
    return (
      <div className="h-40 flex items-center justify-center">
        <p className="text-xs text-gray-500">Waiting for data...</p>
      </div>
    )
  }

  return (
    <ResponsiveContainer width="100%" height={160}>
      <LineChart data={chartData} margin={{ top: 5, right: 5, left: -20, bottom: 5 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
        <XAxis
          dataKey="timestamp"
          tick={{ fontSize: 10, fill: '#6b7280' }}
          stroke="#9ca3af"
        />
        <YAxis
          tick={{ fontSize: 10, fill: '#6b7280' }}
          stroke="#9ca3af"
        />
        <Tooltip
          contentStyle={{
            fontSize: '12px',
            backgroundColor: 'white',
            border: '1px solid #e5e7eb',
            borderRadius: '4px',
          }}
        />
        <Line
          type="monotone"
          dataKey="value"
          stroke="#3b82f6"
          strokeWidth={2}
          dot={{ r: 3, fill: '#3b82f6' }}
          activeDot={{ r: 5 }}
        />
      </LineChart>
    </ResponsiveContainer>
  )
}
