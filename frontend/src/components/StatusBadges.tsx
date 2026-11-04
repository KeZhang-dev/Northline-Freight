import { cn } from '@/lib/utils'
import {
  assignmentStatusLabel,
  employmentStatusLabel,
  orderStatusLabel,
  vehicleStatusLabel,
} from '@/lib/format'
import type { AssignmentStatus, EmploymentStatus, OrderPriority, OrderStatus, VehicleStatus } from '@/lib/types'

type Tone = 'neutral' | 'blue' | 'green' | 'amber' | 'red' | 'slate' | 'violet'

const tones: Record<Tone, string> = {
  neutral: 'bg-muted text-muted-foreground ring-border',
  blue: 'bg-blue-50 text-blue-700 ring-blue-200',
  green: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  amber: 'bg-amber-50 text-amber-800 ring-amber-200',
  red: 'bg-red-50 text-red-700 ring-red-200',
  slate: 'bg-slate-100 text-slate-600 ring-slate-200',
  violet: 'bg-violet-50 text-violet-700 ring-violet-200',
}

function Pill({ tone, children, testId }: { tone: Tone; children: React.ReactNode; testId?: string }) {
  return (
    <span
      className={cn('inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset', tones[tone])}
      data-testid={testId}
    >
      {children}
    </span>
  )
}

const priorityTone: Record<OrderPriority, Tone> = { Urgent: 'red', High: 'amber', Normal: 'blue', Low: 'slate' }

export function PriorityBadge({ priority }: { priority: OrderPriority }) {
  return (
    <Pill tone={priorityTone[priority]} testId="priority-badge">
      <span
        className={cn('mr-1.5 size-1.5 rounded-full', {
          'bg-red-600': priority === 'Urgent',
          'bg-amber-500': priority === 'High',
          'bg-blue-500': priority === 'Normal',
          'bg-slate-400': priority === 'Low',
        })}
      />
      {priority}
    </Pill>
  )
}

const orderTone: Record<OrderStatus, Tone> = {
  Pending: 'amber',
  OnHold: 'slate',
  Assigned: 'blue',
  Completed: 'green',
  Cancelled: 'neutral',
}

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  return (
    <Pill tone={orderTone[status]} testId="order-status-badge">
      {orderStatusLabel[status]}
    </Pill>
  )
}

const assignmentTone: Record<AssignmentStatus, Tone> = {
  Assigned: 'blue',
  Reassigned: 'violet',
  Cancelled: 'neutral',
  Completed: 'green',
}

export function AssignmentStatusBadge({ status }: { status: AssignmentStatus }) {
  return (
    <Pill tone={assignmentTone[status]} testId="assignment-status-badge">
      {assignmentStatusLabel[status]}
    </Pill>
  )
}

export function EmploymentStatusBadge({ status }: { status: EmploymentStatus }) {
  const tone: Tone = status === 'Active' ? 'green' : status === 'OnLeave' ? 'amber' : 'neutral'
  return (
    <Pill tone={tone} testId="employment-status-badge">
      {employmentStatusLabel[status]}
    </Pill>
  )
}

export function VehicleStatusBadge({ status }: { status: VehicleStatus }) {
  const tone: Tone = status === 'Active' ? 'green' : status === 'Maintenance' ? 'amber' : 'neutral'
  return (
    <Pill tone={tone} testId="vehicle-status-badge">
      {vehicleStatusLabel[status]}
    </Pill>
  )
}

export function AvailabilityBadge({ available }: { available: boolean }) {
  return (
    <Pill tone={available ? 'green' : 'slate'} testId="availability-badge">
      {available ? 'Available now' : 'Not available now'}
    </Pill>
  )
}
