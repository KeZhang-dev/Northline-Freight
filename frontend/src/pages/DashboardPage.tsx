import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { ArrowRight, Info } from 'lucide-react'
import { PageHeader } from '@/components/layout/AppShell'
import { ErrorState, LoadingState } from '@/components/States'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { KpiCards } from '@/features/dispatch/KpiCards'
import { api } from '@/lib/api'
import type { CountByLabel } from '@/lib/types'

const labels: Record<string, string> = { OnHold: 'On hold', OnLeave: 'On leave' }

function CountList({ title, rows, testId }: { title: string; rows: CountByLabel[]; testId: string }) {
  return (
    <Card data-testid={testId}>
      <CardHeader>
        <CardTitle className="text-sm">{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <dl className="divide-y text-sm">
          {rows.map((r) => (
            <div key={r.label} className="flex justify-between py-1.5">
              <dt className="text-muted-foreground">{labels[r.label] ?? r.label}</dt>
              <dd className="font-medium tabular-nums">{r.count}</dd>
            </div>
          ))}
        </dl>
      </CardContent>
    </Card>
  )
}

export function DashboardPage() {
  const { data, isLoading, error, refetch } = useQuery({ queryKey: ['dashboard'], queryFn: api.dashboardStats })

  return (
    <div>
      <PageHeader
        title="Dashboard"
        description="Operational overview for Northline Freight & Logistics."
        actions={
          <Button asChild data-testid="go-to-dispatch">
            <Link to="/dispatch">
              Open Dispatch Management <ArrowRight className="size-4" />
            </Link>
          </Button>
        }
      />
      <div className="mb-4 flex items-center gap-2 rounded-md border border-blue-200 bg-blue-50 px-3 py-2 text-sm text-blue-800" data-testid="sample-data-notice">
        <Info className="size-4 shrink-0" />
        {data?.dataNotice ?? 'Figures are calculated from the seeded development dataset and are for demonstration only.'}
      </div>
      <KpiCards />
      {isLoading && <LoadingState />}
      {error && <ErrorState error={error} onRetry={refetch} />}
      {data && (
        <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <CountList title="Orders by status" rows={data.ordersByStatus} testId="stats-orders" />
          <CountList title="Assignments by status" rows={data.assignmentsByStatus} testId="stats-assignments" />
          <CountList title="Drivers by employment status" rows={data.driversByEmploymentStatus} testId="stats-drivers" />
          <CountList title="Vehicles by status" rows={data.vehiclesByStatus} testId="stats-vehicles" />
          <Card className="md:col-span-2 xl:col-span-4">
            <CardContent className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Active jobs starting in the next 7 days</span>
              <span className="text-lg font-semibold tabular-nums" data-testid="stats-next-7-days">
                {data.jobsScheduledNext7Days}
              </span>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  )
}
