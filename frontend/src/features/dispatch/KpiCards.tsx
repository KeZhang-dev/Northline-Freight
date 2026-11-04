import { useQuery } from '@tanstack/react-query'
import { ClipboardList, Route, Truck, Users, type LucideIcon } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ErrorState } from '@/components/States'
import { api } from '@/lib/api'

function Kpi({ icon: Icon, label, value, testId }: { icon: LucideIcon; label: string; value: number; testId: string }) {
  return (
    <Card className="py-5" data-testid={testId}>
      <CardContent className="flex items-center justify-between gap-3 px-5">
        <div>
          <div className="text-sm font-medium uppercase tracking-wide text-muted-foreground">{label}</div>
          <div className="mt-1 text-4xl font-semibold tabular-nums" data-testid={`${testId}-value`}>
            {value}
          </div>
        </div>
        <div className="flex size-12 items-center justify-center rounded-lg bg-accent text-primary">
          <Icon className="size-6" />
        </div>
      </CardContent>
    </Card>
  )
}

export function KpiCards() {
  const { data, isLoading, error, refetch } = useQuery({ queryKey: ['summary'], queryFn: api.summary })

  if (error) return <ErrorState error={error} onRetry={refetch} />
  if (isLoading || !data) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-[112px]" />
        ))}
      </div>
    )
  }

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <Kpi icon={ClipboardList} label="Pending orders" value={data.pendingOrders} testId="kpi-pending-orders" />
      <Kpi icon={Route} label="Assigned jobs" value={data.assignedJobs} testId="kpi-assigned-jobs" />
      <Kpi icon={Users} label="Available drivers" value={data.availableDrivers} testId="kpi-available-drivers" />
      <Kpi icon={Truck} label="Available vehicles" value={data.availableVehicles} testId="kpi-available-vehicles" />
    </div>
  )
}
