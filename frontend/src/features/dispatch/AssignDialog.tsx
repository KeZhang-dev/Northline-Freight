import { useEffect, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ArrowLeft, Loader2 } from 'lucide-react'
import { ApiError, api } from '@/lib/api'
import { formatKg, formatRange, vehicleTypeLabel } from '@/lib/format'
import { ApiErrorAlert, ErrorState, LoadingState } from '@/components/States'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { CandidatePicker, OrderSummary } from './CandidatePicker'
import { useCandidates, useRefreshDispatchData } from './hooks'

interface AssignDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Preselected order (when opened from an order row). */
  initialOrderId: number | null
}

export function AssignDialog({ open, onOpenChange, initialOrderId }: AssignDialogProps) {
  const [orderId, setOrderId] = useState<number | null>(initialOrderId)
  const [driverId, setDriverId] = useState<number | null>(null)
  const [vehicleId, setVehicleId] = useState<number | null>(null)
  const [notes, setNotes] = useState('')
  const [step, setStep] = useState<'select' | 'confirm'>('select')
  const refresh = useRefreshDispatchData()

  // Reset whenever the dialog is (re)opened.
  useEffect(() => {
    if (open) {
      setOrderId(initialOrderId)
      setDriverId(null)
      setVehicleId(null)
      setNotes('')
      setStep('select')
    }
  }, [open, initialOrderId])

  const eligibleOrders = useQuery({
    queryKey: ['orders', 'eligible-for-dialog'],
    queryFn: () => api.orders({ eligibleOnly: true, sort: 'PickupAt', pageSize: 100 }),
    enabled: open,
  })

  const candidates = useCandidates(open ? orderId : null, { driverId: driverId ?? undefined, vehicleId: vehicleId ?? undefined })

  const mutation = useMutation({
    mutationFn: () => api.createAssignment({ orderId: orderId!, driverId: driverId!, vehicleId: vehicleId!, notes: notes.trim() || undefined }),
    onSuccess: async (created) => {
      toast.success(`${created.assignmentNumber} created`, {
        description: `${created.order.orderNumber} assigned to ${created.driver.fullName} with ${created.vehicle.registrationNumber}.`,
      })
      await refresh()
      onOpenChange(false)
    },
    onError: async (error) => {
      // The server state differed from what the dialog showed: refresh so the lists reflect reality.
      if (error instanceof ApiError && (error.status === 409 || error.status === 422)) await refresh()
    },
  })

  useEffect(() => {
    if (open) mutation.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, orderId, driverId, vehicleId])

  const data = candidates.data?.order.id === orderId ? candidates.data : undefined
  const selectedDriver = data?.drivers.find((c) => c.driver.id === driverId)
  const selectedVehicle = data?.vehicles.find((c) => c.vehicle.id === vehicleId)
  const canReview = !!data && data.orderEligible && !!selectedDriver?.eligible && !!selectedVehicle?.eligible && !candidates.isFetching

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-4xl" data-testid="assign-dialog">
        <DialogHeader>
          <DialogTitle>{step === 'select' ? 'New dispatch assignment' : 'Confirm assignment'}</DialogTitle>
          <DialogDescription>
            {step === 'select'
              ? 'Select a pending order, then an eligible vehicle and driver. Ineligible resources are shown with the reason.'
              : 'Review the details below. The assignment is validated again by the server when you confirm.'}
          </DialogDescription>
        </DialogHeader>

        {step === 'select' && (
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="assign-order">Transport order</Label>
              <Select
                value={orderId ? String(orderId) : undefined}
                onValueChange={(v) => {
                  setOrderId(Number(v))
                  setDriverId(null)
                  setVehicleId(null)
                }}
              >
                <SelectTrigger id="assign-order" className="w-full" data-testid="assign-order-select">
                  <SelectValue placeholder={eligibleOrders.isLoading ? 'Loading orders…' : 'Select a pending order'} />
                </SelectTrigger>
                <SelectContent>
                  {eligibleOrders.data?.items.map((o) => (
                    <SelectItem key={o.id} value={String(o.id)} data-testid={`assign-order-option-${o.orderNumber}`}>
                      {o.orderNumber} · {o.customerName} · {formatKg(o.cargoWeightKg)} · {o.priority}
                    </SelectItem>
                  ))}
                  {/* Keep a preselected order visible even if it is no longer eligible. */}
                  {data && !eligibleOrders.data?.items.some((o) => o.id === data.order.id) && (
                    <SelectItem value={String(data.order.id)}>
                      {data.order.orderNumber} · {data.order.customerName} (not eligible)
                    </SelectItem>
                  )}
                </SelectContent>
              </Select>
              {eligibleOrders.data?.items.length === 0 && (
                <p className="text-sm text-muted-foreground" data-testid="no-eligible-orders">
                  There are no pending orders eligible for dispatch.
                </p>
              )}
            </div>

            {orderId !== null && candidates.isLoading && <LoadingState label="Checking driver and vehicle eligibility…" />}
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
                  />
                )}
                {data.orderEligible && (
                  <div className="space-y-2">
                    <Label htmlFor="assign-notes">Dispatcher notes (optional)</Label>
                    <Textarea
                      id="assign-notes"
                      value={notes}
                      maxLength={1000}
                      onChange={(e) => setNotes(e.target.value)}
                      placeholder="Instructions for the driver, site contact, etc."
                      data-testid="assign-notes"
                    />
                  </div>
                )}
              </>
            )}
          </div>
        )}

        {step === 'confirm' && data && selectedDriver && selectedVehicle && (
          <div className="space-y-4" data-testid="assign-confirmation">
            <dl className="grid gap-x-6 gap-y-3 rounded-md border p-4 text-sm sm:grid-cols-2">
              <div>
                <dt className="text-muted-foreground">Order</dt>
                <dd className="font-medium">
                  {data.order.orderNumber} · {data.order.customerName}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Window</dt>
                <dd className="font-medium">{formatRange(data.order.pickupAt, data.order.deliveryDeadline)}</dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Route</dt>
                <dd className="font-medium">
                  {data.order.pickupLocation} → {data.order.deliveryLocation}
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Cargo</dt>
                <dd className="font-medium">
                  {data.order.cargoDescription} ({formatKg(data.order.cargoWeightKg)})
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Driver</dt>
                <dd className="font-medium" data-testid="confirm-driver">
                  {selectedDriver.driver.fullName} ({selectedDriver.driver.driverCode}, class {selectedDriver.driver.licenceClass})
                </dd>
              </div>
              <div>
                <dt className="text-muted-foreground">Vehicle</dt>
                <dd className="font-medium" data-testid="confirm-vehicle">
                  {selectedVehicle.vehicle.registrationNumber} ({vehicleTypeLabel[selectedVehicle.vehicle.vehicleType]},{' '}
                  {formatKg(selectedVehicle.vehicle.maxPayloadKg)})
                </dd>
              </div>
              {notes.trim() && (
                <div className="sm:col-span-2">
                  <dt className="text-muted-foreground">Notes</dt>
                  <dd>{notes.trim()}</dd>
                </div>
              )}
            </dl>
            {mutation.error && <ApiErrorAlert error={mutation.error} />}
          </div>
        )}

        <DialogFooter>
          {step === 'select' ? (
            <>
              <Button variant="outline" onClick={() => onOpenChange(false)} data-testid="assign-cancel">
                Cancel
              </Button>
              <Button disabled={!canReview} onClick={() => setStep('confirm')} data-testid="assign-review">
                Review assignment
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={() => setStep('select')} disabled={mutation.isPending} data-testid="assign-back">
                <ArrowLeft className="size-4" /> Back
              </Button>
              <Button onClick={() => mutation.mutate()} disabled={mutation.isPending || mutation.isError} data-testid="assign-confirm">
                {mutation.isPending && <Loader2 className="size-4 animate-spin" />}
                Confirm assignment
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
