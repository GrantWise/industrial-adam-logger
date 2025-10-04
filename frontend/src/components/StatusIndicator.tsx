import { cn } from '@/lib/utils'

export type Status = 'Online' | 'Offline' | 'Error' | 'Unknown'

interface StatusIndicatorProps {
  status: Status
  className?: string
}

export function StatusIndicator({ status, className }: StatusIndicatorProps) {
  const colors = {
    Online: 'bg-green-500',
    Offline: 'bg-gray-400',
    Error: 'bg-red-500',
    Unknown: 'bg-yellow-500',
  }

  const labels = {
    Online: 'Online',
    Offline: 'Offline',
    Error: 'Error',
    Unknown: 'Unknown',
  }

  return (
    <div className={cn('flex items-center gap-2', className)}>
      <div className={cn('w-2 h-2 rounded-full', colors[status])} />
      <span className="text-sm font-medium">{labels[status]}</span>
    </div>
  )
}
