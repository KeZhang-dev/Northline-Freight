import { useEffect, useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ArrowLeft, Loader2 } from 'lucide-react'
import { ApiError, api } from '@/lib/api'
import type { Assignment } from '@/lib/types'
import { formatKg, vehicleTypeLabel } from '@/lib/format'
import { ApiErrorAlert, ErrorState, LoadingState } from '@/components/States'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { CandidatePicker, OrderSummary } from './CandidatePicker'
import { useCandidates, useRefreshDispatchData } from './hooks'

interface ActionDialogProps {
  assignment: Assignment
  open: boolean
  onOpenChange: (open: boolean) => void
  onDone?: (newAssignmentId?: number) => void
}

const REASON_MAX = 500

function useActionMutation<T>(fn: () => Promise<T>, onSuccess: (result: T) => void) {
  const refresh = useRefreshDispatchData()
  return useMutation({
    mutationFn: fn,
    onSuccess: async (result) => {
      await refresh()
      onSuccess(result)
    },
    onError: async (error) => {
      if (error instanceof ApiError && (error.status === 409 || error.status === 422)) await refresh()
    },
  })
}

function ReasonField({ id, value, onChange, required, label }: { id: string; value: string; onChange: (v: string) => void; required: boolean; label: string }) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>
        {label} {required ? <span className="text-red-600">*</span> : <span className="text-muted-foreground">(optional)</span>}
      </Label>
      <Textarea id={id} value={value} maxLength={REASON_MAX} onChange={(e) => onChange(e.target.value)} data-testid={id} />
      <div className="text-right text-xs text-muted-foreground">
        {value.length}/{REASON_MAX}
      </div>
    </div>
  )
}

/** BR-12: cancel an active assignment. Reason is mandatory. */
export function CancelAssignmentDialog({ assignment, open, onOpenChange, onDone }: ActionDialogProps) {
  const [reason, setReason] = useState('')
  const mutation = useActionMutation(
    () => api.cancel(assignment.id, { version: assignment.version, reason: reason.trim() }),
    (result) => {
      toast.success(`${result.assignmentNumber} cancelled`, { description: `${result.order.orderNumber} has returned to Pending.` })
      onOpenChange(false)
      onDone?.()
    },
  )
  useEffect(() => {
    if (open) {
      setReason('')
      mutation.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent data-testid="cancel-dialog">
        <DialogHeader>
          <DialogTitle>Cancel {assignment.assignmentNumber}?</DialogTitle>
          <DialogDescription>
            The assignment is closed as Cancelled and kept in the history. Order {assignment.order.orderNumber} returns to Pending, and{' '}
            {assignment.driver.fullName} and {assignment.vehicle.registrationNumber} are released for this time window.
          </DialogDescription>
        </DialogHeader>
        <ReasonField id="cancel-reason" label="Cancellation reason" value={reason} onChange={setReason} required />
        {mutation.error && <ApiErrorAlert error={mutation.error} />}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
            Keep assignment
          </Button>
          <Button
            variant="destructive"
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending || reason.trim().length === 0}
            data-testid="cancel-confirm"
          >
            {mutation.isPending && <Loader2 className="size-4 animate-spin" />}
            Cancel assignment
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** Mark an active assignment as completed. */
export function CompleteAssignmentDialog({ assignment, open, onOpenChange, onDone }: ActionDialogProps) {
  const [reason, setReason] = useState('')
  const mutation = useActionMutation(
    () => api.complete(assignment.id, { version: assignment.version, reason: reason.trim() || undefined }),
    (result) => {
      toast.success(`${result.assignmentNumber} completed`, { description: `${result.order.orderNumber} is now Completed.` })
      onOpenChange(false)
      onDone?.()
    },
  )
  useEffect(() => {
    if (open) {
      setReason('')
      mutation.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent data-testid="complete-dialog">
        <DialogHeader>
          <DialogTitle>Mark {assignment.assignmentNumber} as completed?</DialogTitle>
          <DialogDescription>
            The assignment is closed as Completed and order {assignment.order.orderNumber} is marked Completed. This cannot be undone.
          </DialogDescription>
        </DialogHeader>
        <ReasonField id="complete-note" label="Completion note" value={reason} onChange={setReason} required={false} />
        {mutation.error && <ApiErrorAlert error={mutation.error} />}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
            Go back
          </Button>
          <Button onClick={() => mutation.mutate()} disabled={mutation.isPending} data-testid="complete-confirm">
            {mutation.isPending && <Loader2 className="size-4 animate-spin" />}
            Mark completed
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/** BR-11: move an active assignment to a different driver and/or vehicle. */
export function ReassignDialog({ assignment, open, onOpenChange, onDone }: ActionDialogProps) {
  const [driverId, setDriverId] = useState(assignment.driver.id)
  const [vehicleId, setVehicleId] = useState(assignment.vehicle.id)
  const [reason, setReason] = useState('')
  const [step, setStep] = useState<'select' | 'confirm'>('select')

  useEffect(() => {
    if (open) {
      setDriverId(assignment.driver.id)
      setVehicleId(assignment.vehicle.id)
      setReason('')
      setStep('select')
    }
  }, [open, assignment])

  const candidates = useCandidates(open ? assignment.order.id : null, {
    driverId,
    vehicleId,
    excludeAssignmentId: assignment.id,
  })

  const mutation = useActionMutation(
    () =>
      api.reassign(assignment.id, {
        version: assignment.version,
        driverId: driverId !== assignment.driver.id ? driverId : undefined,
        vehicleId: vehicleId !== assignment.vehicle.id ? vehicleId : undefined,
        reason: reason.trim(),
      }),
    (result) => {
      toast.success(`${result.previous.assignmentNumber} reassigned`, {
        description: `Replaced by ${result.current.assignmentNumber}: ${result.current.driver.fullName} with ${result.current.vehicle.registrationNumber}.`,
      })
      onOpenChange(false)
      onDone?.(result.current.id)
    },
  )
  useEffect(() => {
    if (open) mutation.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, driverId, vehicleId])

  const data = candidates.data
  const selectedDriver = data?.drivers.find((c) => c.driver.id === driverId)
  const selectedVehicle = data?.vehicles.find((c) => c.vehicle.id === vehicleId)
  const changed = driverId !== assignment.driver.id || vehicleId !== assignment.vehicle.id
  const canReview =
    !!data && data.orderEligible && changed && !!selectedDriver?.eligible && !!selectedVehicle?.eligible && reason.trim().length > 0 && !candidates.isFetching

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-4xl" data-testid="reassign-dialog">
        <DialogHeader>
          <DialogTitle>
            {step === 'select' ? `Reassign ${assignment.assignmentNumber}` : `Confirm reassignment of ${assignment.assignmentNumber}`}
          </DialogTitle>
          <DialogDescription>
            The current assignment is kept as Reassigned, and a new assignment is created for the selected driver and vehicle.
          </DialogDescription>
        </DialogHeader>

        {step === 'select' && (
          <div className="space-y-4">
            {candidates.isLoading && <LoadingState label="Checking driver and vehicle eligibility…" />}
            {candidates.error && <ErrorState error={candidates.error} onRetry={candidates.refetch} />}
            {data && (
              <>
                <OrderSummary candidates={data} />
                {data.orderEligible && (
                  <CandidatePicker
                    candidates={data}
                    isFetching={candidates.isFetching}
                    selectedDriverId={driverId}
                    selectedVehicleId={vehicleId}
                    onSelectDriver={setDriverId}
                    onSelectVehicle={setVehicleId}
                    currentDriverId={assignment.driver.id}
                    currentVehicleId={assignment.vehicle.id}
                  />
                )}
                {!changed && (
                  <p className="text-sm text-muted-foreground" data-testid="reassign-no-change-hint">
                    Select a different driver, vehicle, or both.
                  </p>
                )}
                <ReasonField id="reassign-reason" label="Reason for reassignment" value={reason} onChange={setReason} required />
              </>
            )}
          </div>
        )}

        {step === 'confirm' && selectedDriver && selectedVehicle && (
          <div className="space-y-4" data-testid="reassign-confirmation">
            <div className="grid gap-4 rounded-md border p-4 text-sm sm:grid-cols-2">
              <div>
                <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Current</div>
                <div>{assignment.driver.fullName} ({assignment.driver.driverCode})</div>
                <div>
                  {assignment.vehicle.registrationNumber} · {vehicleTypeLabel[assignment.vehicle.vehicleType]}
                </div>
              </div>
              <div>
                <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-muted-foreground">New</div>
                <div className={driverId !== assignment.driver.id ? 'font-semibold text-primary' : ''} data-testid="confirm-driver">
                  {selectedDriver.driver.fullName} ({selectedDriver.driver.driverCode})
                </div>
                <div className={vehicleId !== assignment.vehicle.id ? 'font-semibold text-primary' : ''} data-testid="confirm-vehicle">
                  {selectedVehicle.vehicle.registrationNumber} · {vehicleTypeLabel[selectedVehicle.vehicle.vehicleType]} ·{' '}
                  {formatKg(selectedVehicle.vehicle.maxPayloadKg)}
                </div>
              </div>
              <div className="sm:col-span-2">
                <div className="text-muted-foreground">Reason</div>
                <div>{reason.trim()}</div>
              </div>
            </div>
            {mutation.error && <ApiErrorAlert error={mutation.error} />}
          </div>
        )}

        <DialogFooter>
          {step === 'select' ? (
            <>
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                Close
              </Button>
              <Button disabled={!canReview} onClick={() => setStep('confirm')} data-testid="reassign-review">
                Review reassignment
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={() => setStep('select')} disabled={mutation.isPending}>
                <ArrowLeft className="size-4" /> Back
              </Button>
              <Button onClick={() => mutation.mutate()} disabled={mutation.isPending || mutation.isError} data-testid="reassign-confirm">
                {mutation.isPending && <Loader2 className="size-4 animate-spin" />}
                Confirm reassignment
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
