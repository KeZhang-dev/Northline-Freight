// Mirrors the backend DTOs (backend/src/Northline.Api/Dtos). Enums are serialised as strings.

export type UserRole = 'Admin' | 'Dispatcher' | 'Driver'
export type OrderPriority = 'Low' | 'Normal' | 'High' | 'Urgent'
export type OrderStatus = 'Pending' | 'OnHold' | 'Assigned' | 'Completed' | 'Cancelled'
export type EmploymentStatus = 'Active' | 'OnLeave' | 'Inactive'
export type VehicleType = 'LightCommercial' | 'MediumRigid' | 'HeavyRigid' | 'HeavyCombination'
export type VehicleStatus = 'Active' | 'Maintenance' | 'Inactive'
export type AssignmentStatus = 'Assigned' | 'Reassigned' | 'Cancelled' | 'Completed'
export type AssignmentAction = 'reassign' | 'cancel' | 'complete'
export type SortDirection = 'Asc' | 'Desc'

export interface RuleViolation {
  rule: string
  code: string
  message: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface User {
  id: number
  email: string
  fullName: string
  role: UserRole
  driverId: number | null
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: User
}

export interface UserRef {
  id: number
  fullName: string
}

export interface AssignmentRef {
  id: number
  assignmentNumber: string
  orderId: number
  orderNumber: string
  startAt: string
  endAt: string
}

export interface OrderRef {
  id: number
  orderNumber: string
  customerName: string
  pickupLocation: string
  deliveryLocation: string
  cargoWeightKg: number
  pickupAt: string
  deliveryDeadline: string
  priority: OrderPriority
  status: OrderStatus
}

export interface DriverRef {
  id: number
  driverCode: string
  fullName: string
  licenceClass: number
  employmentStatus: EmploymentStatus
}

export interface VehicleRef {
  id: number
  vehicleCode: string
  registrationNumber: string
  vehicleType: VehicleType
  maxPayloadKg: number
  vehicleStatus: VehicleStatus
}

export interface Order {
  id: number
  orderNumber: string
  customerName: string
  pickupLocation: string
  deliveryLocation: string
  cargoDescription: string
  cargoWeightKg: number
  pickupAt: string
  deliveryDeadline: string
  priority: OrderPriority
  status: OrderStatus
  isEligible: boolean
  ineligibilityReasons: RuleViolation[]
  activeAssignment: AssignmentRef | null
  createdAt: string
  updatedAt: string
}

export interface Driver {
  id: number
  driverCode: string
  firstName: string
  lastName: string
  fullName: string
  licenceClass: number
  permittedVehicleTypes: VehicleType[]
  employmentStatus: EmploymentStatus
  phone: string
  email: string
  isAvailableNow: boolean
  currentAssignment: AssignmentRef | null
  nextAssignment: AssignmentRef | null
  upcomingAssignmentCount: number
}

export interface Vehicle {
  id: number
  vehicleCode: string
  registrationNumber: string
  vehicleType: VehicleType
  maxPayloadKg: number
  minimumLicenceClass: number
  vehicleStatus: VehicleStatus
  isAvailableNow: boolean
  currentAssignment: AssignmentRef | null
  nextAssignment: AssignmentRef | null
  upcomingAssignmentCount: number
}

export interface Assignment {
  id: number
  assignmentNumber: string
  status: AssignmentStatus
  version: number
  startAt: string
  endAt: string
  order: OrderRef
  driver: DriverRef
  vehicle: VehicleRef
  previousAssignmentId: number | null
  previousAssignmentNumber: string | null
  replacedByAssignmentId: number | null
  replacedByAssignmentNumber: string | null
  notes: string | null
  createdAt: string
  createdBy: UserRef
  closedAt: string | null
  closedBy: UserRef | null
  closeReason: string | null
  allowedActions: AssignmentAction[]
}

export interface AssignmentDetail {
  assignment: Assignment
  history: Assignment[]
}

export interface ReassignmentResult {
  previous: Assignment
  current: Assignment
}

export interface DriverCandidate {
  driver: Driver
  eligible: boolean
  reasons: RuleViolation[]
}

export interface VehicleCandidate {
  vehicle: Vehicle
  eligible: boolean
  reasons: RuleViolation[]
}

export interface Candidates {
  order: Order
  mode: 'assign' | 'reassign'
  excludedAssignmentId: number | null
  orderEligible: boolean
  orderViolations: RuleViolation[]
  selectedDriverId: number | null
  selectedVehicleId: number | null
  drivers: DriverCandidate[]
  vehicles: VehicleCandidate[]
}

export interface DispatchSummary {
  asOf: string
  pendingOrders: number
  eligiblePendingOrders: number
  urgentPendingOrders: number
  assignedJobs: number
  availableDrivers: number
  activeDrivers: number
  totalDrivers: number
  availableVehicles: number
  activeVehicles: number
  totalVehicles: number
}

export interface CountByLabel {
  label: string
  count: number
}

export interface DashboardStats {
  asOf: string
  dataNotice: string
  ordersByStatus: CountByLabel[]
  assignmentsByStatus: CountByLabel[]
  driversByEmploymentStatus: CountByLabel[]
  vehiclesByStatus: CountByLabel[]
  jobsScheduledNext7Days: number
}

export interface DriverProfile {
  user: User
  driver: DriverRef | null
  message: string
}
