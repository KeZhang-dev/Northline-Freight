import type { AssignmentStatus, EmploymentStatus, OrderStatus, VehicleStatus, VehicleType } from './types'

/** All times are displayed in New Zealand local time, whatever the browser's time zone. */
export const NZ_TIME_ZONE = 'Pacific/Auckland'

const dateTime = new Intl.DateTimeFormat('en-NZ', {
  timeZone: NZ_TIME_ZONE,
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

const timeOnly = new Intl.DateTimeFormat('en-NZ', {
  timeZone: NZ_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

const dayKey = new Intl.DateTimeFormat('en-CA', { timeZone: NZ_TIME_ZONE, year: 'numeric', month: '2-digit', day: '2-digit' })

export function formatDateTime(iso: string | null | undefined) {
  return iso ? dateTime.format(new Date(iso)) : '—'
}

/** "Thu 5 Nov 06:00 – 14:00", or both full date-times if the range spans days. */
export function formatRange(startIso: string, endIso: string) {
  const start = new Date(startIso)
  const end = new Date(endIso)
  if (dayKey.format(start) === dayKey.format(end)) {
    return `${dateTime.format(start)} – ${timeOnly.format(end)}`
  }
  return `${dateTime.format(start)} – ${dateTime.format(end)}`
}

export function formatKg(kg: number) {
  return `${new Intl.NumberFormat('en-NZ', { maximumFractionDigits: 2 }).format(kg)} kg`
}

export const vehicleTypeLabel: Record<VehicleType, string> = {
  LightCommercial: 'Light Commercial Vehicle',
  MediumRigid: 'Medium Rigid Truck',
  HeavyRigid: 'Heavy Rigid Truck',
  HeavyCombination: 'Heavy Combination Vehicle',
}

export const orderStatusLabel: Record<OrderStatus, string> = {
  Pending: 'Pending',
  OnHold: 'On hold',
  Assigned: 'Assigned',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
}

export const employmentStatusLabel: Record<EmploymentStatus, string> = {
  Active: 'Active',
  OnLeave: 'On leave',
  Inactive: 'Inactive',
}

export const vehicleStatusLabel: Record<VehicleStatus, string> = {
  Active: 'Active',
  Maintenance: 'Maintenance',
  Inactive: 'Inactive',
}

export const assignmentStatusLabel: Record<AssignmentStatus, string> = {
  Assigned: 'Assigned',
  Reassigned: 'Reassigned',
  Cancelled: 'Cancelled',
  Completed: 'Completed',
}
