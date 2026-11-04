import { useState } from 'react'
import { ArrowRightLeft, CheckCircle2, RefreshCw, XCircle } from 'lucide-react'
import { useAuth } from '@/auth/AuthContext'
import { cn } from '@/lib/utils'
import { formatDateTime, formatKg, formatRange, vehicleTypeLabel } from '@/lib/format'
import type { Assignment } from '@/lib/types'
import { AssignmentStatusBadge, OrderStatusBadge, PriorityBadge } from '@/components/StatusBadges'
import { ErrorState, LoadingState } from '@/components/States'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { CancelAssignmentDialog, CompleteAssignmentDialog, ReassignDialog } from './AssignmentActions'
import { useAssignmentDetail } from './hooks'

function Field({ label, children, testId }: { label: string; children: React.ReactNode; testId?: string }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm" data-testid={testId}>
        {children}
      </dd>
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-3">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</h3>
      {children}
    </section>
  )
}

function HistoryTimeline({ history, currentId, onSelect }: { history: Assignment[]; currentId: number; onSelect: (id: number) => void }) {
  return (
    <ol className="relative space-y-3 border-l pl-5" data-testid="assignment-history">
      {history.map((h) => (
        <li key={h.id} className="relative" data-testid={`history-${h.assignmentNumber}`}>
          <span
            className={cn(
              'absolute -left-[25px] top-1.5 size-2.5 rounded-full ring-4 ring-card',
              h.status === 'Assigned' ? 'bg-primary' : h.status === 'Completed' ? 'bg-emerald-500' : 'bg-muted-foreground/50',
            )}
          />
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => onSelect(h.id)}
              className={cn('font-mono text-sm', h.id === currentId ? 'font-semibold' : 'text-primary hover:underline')}
              disabled={h.id === currentId}
            >
              {h.assignmentNumber}
            </button>
            <AssignmentStatusBadge status={h.status} />
            {h.id === currentId && <span className="text-xs text-muted-foreground">(viewing)</span>}
          </div>
          <div className="mt-0.5 text-xs text-muted-foreground">
            {h.driver.fullName} · {h.vehicle.registrationNumber} · created {formatDateTime(h.createdAt)} by {h.createdBy.fullName}
          </div>
          {h.closedAt && (
            <div className="text-xs text-muted-foreground">
              {h.status} {formatDateTime(h.closedAt)} by {h.closedBy?.fullName}
              {h.closeReason ? ` — “${h.closeReason}”` : ''}
            </div>
          )}
        </li>
      ))}
    </ol>
  )
}

interface AssignmentDrawerProps {
  assignmentId: number | null
  onClose: () => void
  onNavigate: (id: number) => void
}

export function AssignmentDrawer({ assignmentId, onClose, onNavigate }: AssignmentDrawerProps) {
  const { hasRole } = useAuth()
  const canWrite = hasRole('Admin', 'Dispatcher')
  const { data, isLoading, error, refetch, isFetching } = useAssignmentDetail(assignmentId)
  const [dialog, setDialog] = useState<'reassign' | 'cancel' | 'complete' | null>(null)
  const a = data?.assignment

  return (
    <Sheet open={assignmentId !== null} onOpenChange={(open) => !open && onClose()}>
      <SheetContent className="w-full overflow-y-auto sm:max-w-xl" data-testid="assignment-drawer">
        <SheetHeader className="border-b">
          <SheetTitle className="flex items-center gap-3">
            <span data-testid="drawer-assignment-number">{a?.assignmentNumber ?? 'Assignment'}</span>
            {a && <AssignmentStatusBadge status={a.status} />}
          </SheetTitle>
          <SheetDescription>
            {a ? `${a.order.orderNumber} · ${a.order.customerName}` : 'Loading assignment details'}
          </SheetDescription>
        </SheetHeader>

        <div className="space-y-6 px-4 pb-6">
          {isLoading && <LoadingState />}
          {error && <ErrorState error={error} onRetry={refetch} />}

          {a && data && (
            <>
              {canWrite && a.allowedActions.length > 0 && (
                <div className="flex flex-wrap gap-2" data-testid="assignment-actions">
                  {a.allowedActions.includes('reassign') && (
                    <Button size="sm" onClick={() => setDialog('reassign')} data-testid="action-reassign">
                      <ArrowRightLeft className="size-4" /> Reassign
                    </Button>
                  )}
                  {a.allowedActions.includes('complete') && (
                    <Button size="sm" variant="outline" onClick={() => setDialog('complete')} data-testid="action-complete">
                      <CheckCircle2 className="size-4" /> Mark completed
                    </Button>
                  )}
                  {a.allowedActions.includes('cancel') && (
                    <Button size="sm" variant="outline" className="text-red-700 hover:text-red-800" onClick={() => setDialog('cancel')} data-testid="action-cancel">
                      <XCircle className="size-4" /> Cancel assignment
                    </Button>
                  )}
                  <Button size="sm" variant="ghost" onClick={() => refetch()} disabled={isFetching} title="Reload">
                    <RefreshCw className={cn('size-4', isFetching && 'animate-spin')} />
                  </Button>
                </div>
              )}
              {a.status !== 'Assigned' && (
                <p className="rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground" data-testid="closed-notice">
                  This assignment is {a.status.toLowerCase()} and can no longer be changed.
                  {a.replacedByAssignmentId && (
                    <>
                      {' '}
                      It was replaced by{' '}
                      <button className="font-mono text-primary hover:underline" onClick={() => onNavigate(a.replacedByAssignmentId!)}>
                        {a.replacedByAssignmentNumber}
                      </button>
                      .
                    </>
                  )}
                </p>
              )}

              <Section title="Job">
                <dl className="grid grid-cols-2 gap-4">
                  <Field label="Window">{formatRange(a.startAt, a.endAt)}</Field>
                  <Field label="Priority">
                    <PriorityBadge priority={a.order.priority} />
                  </Field>
                  <Field label="Pickup">{a.order.pickupLocation}</Field>
                  <Field label="Delivery">{a.order.deliveryLocation}</Field>
                  <Field label="Cargo weight">{formatKg(a.order.cargoWeightKg)}</Field>
                  <Field label="Order status" testId="drawer-order-status">
                    <OrderStatusBadge status={a.order.status} />
                  </Field>
                </dl>
              </Section>
              <Separator />
              <Section title="Resources">
                <dl className="grid grid-cols-2 gap-4">
                  <Field label="Driver" testId="drawer-driver">
                    {a.driver.fullName}
                    <div className="text-xs text-muted-foreground">
                      {a.driver.driverCode} · Licence class {a.driver.licenceClass}
                    </div>
                  </Field>
                  <Field label="Vehicle" testId="drawer-vehicle">
                    {a.vehicle.registrationNumber}
                    <div className="text-xs text-muted-foreground">
                      {a.vehicle.vehicleCode} · {vehicleTypeLabel[a.vehicle.vehicleType]} · {formatKg(a.vehicle.maxPayloadKg)}
                    </div>
                  </Field>
                </dl>
                {a.notes && <Field label="Dispatcher notes">{a.notes}</Field>}
              </Section>
              <Separator />
              <Section title="Audit">
                <dl className="grid grid-cols-2 gap-4">
                  <Field label="Created">
                    {formatDateTime(a.createdAt)}
                    <div className="text-xs text-muted-foreground">by {a.createdBy.fullName}</div>
                  </Field>
                  <Field label="Closed">
                    {a.closedAt ? (
                      <>
                        {formatDateTime(a.closedAt)}
                        <div className="text-xs text-muted-foreground">by {a.closedBy?.fullName}</div>
                      </>
                    ) : (
                      '—'
                    )}
                  </Field>
                  {a.closeReason && (
                    <div className="col-span-2">
                      <Field label="Close reason" testId="drawer-close-reason">
                        {a.closeReason}
                      </Field>
                    </div>
                  )}
                  {a.previousAssignmentId && (
                    <Field label="Replaces">
                      <button className="font-mono text-primary hover:underline" onClick={() => onNavigate(a.previousAssignmentId!)}>
                        {a.previousAssignmentNumber}
                      </button>
                    </Field>
                  )}
                </dl>
              </Section>
              <Separator />
              <Section title={`Order assignment history (${data.history.length})`}>
                <HistoryTimeline history={data.history} currentId={a.id} onSelect={onNavigate} />
              </Section>

              <ReassignDialog
                assignment={a}
                open={dialog === 'reassign'}
                onOpenChange={(o) => setDialog(o ? 'reassign' : null)}
                onDone={(newId) => newId && onNavigate(newId)}
              />
              <CancelAssignmentDialog assignment={a} open={dialog === 'cancel'} onOpenChange={(o) => setDialog(o ? 'cancel' : null)} />
              <CompleteAssignmentDialog assignment={a} open={dialog === 'complete'} onOpenChange={(o) => setDialog(o ? 'complete' : null)} />
            </>
          )}
        </div>
      </SheetContent>
    </Sheet>
  )
}
