import { CheckCircle2, Circle, Loader2, XCircle } from 'lucide-react'
import { cn } from '@/lib/utils'
import { formatKg, formatRange, vehicleTypeLabel } from '@/lib/format'
import type { Candidates, DriverCandidate, RuleViolation, VehicleCandidate } from '@/lib/types'
import { EmploymentStatusBadge, VehicleStatusBadge } from '@/components/StatusBadges'

interface CandidatePickerProps {
  candidates: Candidates
  isFetching: boolean
  selectedDriverId: number | null
  selectedVehicleId: number | null
  onSelectDriver: (id: number) => void
  onSelectVehicle: (id: number) => void
  /** Reassignment mode: highlights the resources on the current assignment. */
  currentDriverId?: number
  currentVehicleId?: number
}

function Reasons({ reasons }: { reasons: RuleViolation[] }) {
  if (reasons.length === 0) return null
  return (
    <ul className="mt-1.5 space-y-0.5" data-testid="candidate-reasons">
      {reasons.map((r, i) => (
        <li key={`${r.code}-${i}`} className="flex gap-1.5 text-xs text-red-700" data-code={r.code}>
          <span className="font-mono font-semibold">{r.rule}</span>
          <span>{r.message}</span>
        </li>
      ))}
    </ul>
  )
}

function OptionRow({
  selected,
  eligible,
  onSelect,
  testId,
  children,
}: {
  selected: boolean
  eligible: boolean
  onSelect: () => void
  testId: string
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      aria-disabled={!eligible}
      disabled={!eligible && !selected}
      onClick={onSelect}
      data-testid={testId}
      data-eligible={eligible}
      data-selected={selected}
      className={cn(
        'flex w-full items-start gap-3 rounded-md border px-3 py-2.5 text-left transition-colors',
        eligible && 'hover:border-primary/50 hover:bg-accent/50',
        selected && eligible && 'border-primary bg-accent ring-1 ring-primary',
        selected && !eligible && 'border-red-300 bg-red-50',
        !eligible && !selected && 'cursor-not-allowed bg-muted/40 opacity-75',
      )}
    >
      <span className="mt-0.5 shrink-0">
        {!eligible ? (
          <XCircle className="size-4 text-red-500" />
        ) : selected ? (
          <CheckCircle2 className="size-4 text-primary" />
        ) : (
          <Circle className="size-4 text-muted-foreground" />
        )}
      </span>
      <span className="min-w-0 flex-1">{children}</span>
    </button>
  )
}

function DriverOption({ c, selected, current, onSelect }: { c: DriverCandidate; selected: boolean; current: boolean; onSelect: () => void }) {
  const d = c.driver
  return (
    <OptionRow selected={selected} eligible={c.eligible} onSelect={onSelect} testId={`driver-option-${d.driverCode}`}>
      <span className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium">{d.fullName}</span>
        <span className="font-mono text-xs text-muted-foreground">{d.driverCode}</span>
        {current && <span className="rounded bg-primary/10 px-1.5 text-[11px] font-medium text-primary">Current</span>}
      </span>
      <span className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
        <span>Licence class {d.licenceClass}</span>
        <EmploymentStatusBadge status={d.employmentStatus} />
        {d.nextAssignment && <span>Next: {d.nextAssignment.assignmentNumber}</span>}
      </span>
      <Reasons reasons={c.reasons} />
    </OptionRow>
  )
}

function VehicleOption({ c, selected, current, onSelect }: { c: VehicleCandidate; selected: boolean; current: boolean; onSelect: () => void }) {
  const v = c.vehicle
  return (
    <OptionRow selected={selected} eligible={c.eligible} onSelect={onSelect} testId={`vehicle-option-${v.vehicleCode}`}>
      <span className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium">{v.registrationNumber}</span>
        <span className="font-mono text-xs text-muted-foreground">{v.vehicleCode}</span>
        {current && <span className="rounded bg-primary/10 px-1.5 text-[11px] font-medium text-primary">Current</span>}
      </span>
      <span className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
        <span>{vehicleTypeLabel[v.vehicleType]}</span>
        <span>· {formatKg(v.maxPayloadKg)}</span>
        <span>· Class {v.minimumLicenceClass}+</span>
        <VehicleStatusBadge status={v.vehicleStatus} />
      </span>
      <Reasons reasons={c.reasons} />
    </OptionRow>
  )
}

/**
 * Lists every driver and vehicle with its eligibility for the order. Ineligible options are shown
 * with their rejection reasons and cannot be selected. This is guidance only: the backend validates
 * every submitted assignment independently.
 */
export function CandidatePicker(props: CandidatePickerProps) {
  const { candidates, isFetching, selectedDriverId, selectedVehicleId } = props
  const eligibleDrivers = candidates.drivers.filter((c) => c.eligible).length
  const eligibleVehicles = candidates.vehicles.filter((c) => c.eligible).length

  return (
    <div className="grid gap-4 md:grid-cols-2" data-testid="candidate-picker">
      <section aria-label="Vehicles">
        <div className="mb-2 flex items-center justify-between">
          <h3 className="text-sm font-semibold">Vehicle</h3>
          <span className="text-xs text-muted-foreground" data-testid="eligible-vehicle-count">
            {eligibleVehicles} of {candidates.vehicles.length} eligible
          </span>
        </div>
        <div role="radiogroup" aria-label="Vehicle" className="max-h-[340px] space-y-1.5 overflow-y-auto pr-1">
          {candidates.vehicles.map((c) => (
            <VehicleOption
              key={c.vehicle.id}
              c={c}
              selected={c.vehicle.id === selectedVehicleId}
              current={c.vehicle.id === props.currentVehicleId}
              onSelect={() => c.eligible && props.onSelectVehicle(c.vehicle.id)}
            />
          ))}
        </div>
      </section>
      <section aria-label="Drivers">
        <div className="mb-2 flex items-center justify-between">
          <h3 className="flex items-center gap-2 text-sm font-semibold">
            Driver {isFetching && <Loader2 className="size-3.5 animate-spin text-muted-foreground" />}
          </h3>
          <span className="text-xs text-muted-foreground" data-testid="eligible-driver-count">
            {eligibleDrivers} of {candidates.drivers.length} eligible
          </span>
        </div>
        <div role="radiogroup" aria-label="Driver" className="max-h-[340px] space-y-1.5 overflow-y-auto pr-1">
          {candidates.drivers.map((c) => (
            <DriverOption
              key={c.driver.id}
              c={c}
              selected={c.driver.id === selectedDriverId}
              current={c.driver.id === props.currentDriverId}
              onSelect={() => c.eligible && props.onSelectDriver(c.driver.id)}
            />
          ))}
        </div>
      </section>
    </div>
  )
}

export function OrderSummary({ candidates }: { candidates: Candidates }) {
  const o = candidates.order
  return (
    <div className="rounded-md border bg-muted/30 p-3 text-sm" data-testid="order-summary">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="font-semibold">
          {o.orderNumber} · {o.customerName}
        </span>
        <span className="text-muted-foreground">{formatKg(o.cargoWeightKg)}</span>
      </div>
      <div className="mt-1 text-muted-foreground">
        {o.pickupLocation} → {o.deliveryLocation}
      </div>
      <div className="mt-0.5 text-muted-foreground">{formatRange(o.pickupAt, o.deliveryDeadline)}</div>
      {!candidates.orderEligible && (
        <div className="mt-2 rounded border border-red-200 bg-red-50 p-2" data-testid="order-violations">
          <div className="text-xs font-semibold text-red-800">This order cannot be dispatched</div>
          <Reasons reasons={candidates.orderViolations} />
        </div>
      )}
    </div>
  )
}
