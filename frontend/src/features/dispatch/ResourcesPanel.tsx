import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { formatKg, formatRange, vehicleTypeLabel } from '@/lib/format'
import type { AssignmentRef } from '@/lib/types'
import { AvailabilityBadge, EmploymentStatusBadge, VehicleStatusBadge } from '@/components/StatusBadges'
import { EmptyState, ErrorState, LoadingState } from '@/components/States'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'

function Schedule({ current, next, onOpen }: { current: AssignmentRef | null; next: AssignmentRef | null; onOpen: (id: number) => void }) {
  if (!current && !next) return <div className="text-sm text-muted-foreground">No active assignments</div>
  return (
    <div className="space-y-0.5 text-sm">
      {current && (
        <div>
          <span className="text-muted-foreground">On job: </span>
          <button className="font-mono text-primary hover:underline" onClick={() => onOpen(current.id)}>
            {current.assignmentNumber}
          </button>
        </div>
      )}
      {next && (
        <div>
          <span className="text-muted-foreground">Next: </span>
          <button className="font-mono text-primary hover:underline" onClick={() => onOpen(next.id)}>
            {next.assignmentNumber}
          </button>
          <span className="text-muted-foreground"> · {formatRange(next.startAt, next.endAt)}</span>
        </div>
      )}
    </div>
  )
}

/** Driver and vehicle availability, derived from operational status and active assignments. */
export function ResourcesPanel({ onOpenAssignment }: { onOpenAssignment: (id: number) => void }) {
  const drivers = useQuery({ queryKey: ['drivers'], queryFn: api.drivers })
  const vehicles = useQuery({ queryKey: ['vehicles'], queryFn: api.vehicles })

  return (
    <Card data-testid="resources-panel">
      <CardHeader>
        <CardTitle className="text-lg">Resources</CardTitle>
      </CardHeader>
      <CardContent>
        <Tabs defaultValue="drivers">
          <TabsList className="w-full">
            <TabsTrigger value="drivers" data-testid="resources-tab-drivers">
              Drivers {drivers.data && `(${drivers.data.filter((d) => d.isAvailableNow).length} available)`}
            </TabsTrigger>
            <TabsTrigger value="vehicles" data-testid="resources-tab-vehicles">
              Vehicles {vehicles.data && `(${vehicles.data.filter((v) => v.isAvailableNow).length} available)`}
            </TabsTrigger>
          </TabsList>

          <TabsContent value="drivers" className="mt-3">
            {drivers.isLoading && <LoadingState />}
            {drivers.error && <ErrorState error={drivers.error} onRetry={drivers.refetch} />}
            {drivers.data?.length === 0 && <EmptyState title="No drivers" />}
            <ul className="divide-y" data-testid="drivers-list">
              {drivers.data?.map((d) => (
                <li key={d.id} className="py-2.5" data-testid={`driver-row-${d.driverCode}`}>
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <div className="text-base font-medium">{d.fullName}</div>
                      <div className="text-sm text-muted-foreground">
                        {d.driverCode} · Class {d.licenceClass} · {d.phone}
                      </div>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <EmploymentStatusBadge status={d.employmentStatus} />
                      {d.employmentStatus === 'Active' && <AvailabilityBadge available={d.isAvailableNow} />}
                    </div>
                  </div>
                  <div className="mt-1">
                    <Schedule current={d.currentAssignment} next={d.nextAssignment} onOpen={onOpenAssignment} />
                  </div>
                </li>
              ))}
            </ul>
          </TabsContent>

          <TabsContent value="vehicles" className="mt-3">
            {vehicles.isLoading && <LoadingState />}
            {vehicles.error && <ErrorState error={vehicles.error} onRetry={vehicles.refetch} />}
            {vehicles.data?.length === 0 && <EmptyState title="No vehicles" />}
            <ul className="divide-y" data-testid="vehicles-list">
              {vehicles.data?.map((v) => (
                <li key={v.id} className="py-2.5" data-testid={`vehicle-row-${v.vehicleCode}`}>
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <div className="text-base font-medium">
                        {v.registrationNumber} <span className="font-mono text-sm font-normal text-muted-foreground">{v.vehicleCode}</span>
                      </div>
                      <div className="text-sm text-muted-foreground">
                        {vehicleTypeLabel[v.vehicleType]} · {formatKg(v.maxPayloadKg)} · Class {v.minimumLicenceClass}+
                      </div>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <VehicleStatusBadge status={v.vehicleStatus} />
                      {v.vehicleStatus === 'Active' && <AvailabilityBadge available={v.isAvailableNow} />}
                    </div>
                  </div>
                  <div className="mt-1">
                    <Schedule current={v.currentAssignment} next={v.nextAssignment} onOpen={onOpenAssignment} />
                  </div>
                </li>
              ))}
            </ul>
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  )
}
