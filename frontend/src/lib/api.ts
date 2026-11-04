import type {
  Assignment,
  AssignmentDetail,
  AssignmentStatus,
  Candidates,
  DashboardStats,
  DispatchSummary,
  Driver,
  DriverProfile,
  LoginResponse,
  Order,
  OrderPriority,
  OrderStatus,
  PagedResult,
  ReassignmentResult,
  RuleViolation,
  SortDirection,
  User,
  Vehicle,
} from './types'

const TOKEN_KEY = 'northline.token'

/**
 * The token lives in sessionStorage, so each browser tab has its own session.
 * This lets two dispatchers work side by side in separate tabs.
 */
export const tokenStore = {
  get: () => sessionStorage.getItem(TOKEN_KEY),
  set: (token: string) => sessionStorage.setItem(TOKEN_KEY, token),
  clear: () => sessionStorage.removeItem(TOKEN_KEY),
}

/** Error returned by the API as an RFC 7807 problem response. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly title: string
  readonly violations: RuleViolation[]
  readonly traceId?: string

  constructor(status: number, code: string, title: string, detail: string, violations: RuleViolation[], traceId?: string) {
    super(detail)
    this.status = status
    this.code = code
    this.title = title
    this.violations = violations
    this.traceId = traceId
  }
}

let onUnauthorized: (() => void) | null = null
export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler
}

type Query = Record<string, string | number | boolean | null | undefined>

function buildUrl(path: string, query?: Query) {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  }
  const qs = params.toString()
  return `/api${path}${qs ? `?${qs}` : ''}`
}

async function request<T>(method: string, path: string, options: { query?: Query; body?: unknown } = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const token = tokenStore.get()
  if (token) headers.Authorization = `Bearer ${token}`
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'

  let response: Response
  try {
    response = await fetch(buildUrl(path, options.query), {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    })
  } catch {
    throw new ApiError(0, 'NETWORK_ERROR', 'Network error', 'The server could not be reached. Check that the API is running.', [])
  }

  if (response.status === 401 && path !== '/auth/login') {
    onUnauthorized?.()
  }

  const text = await response.text()
  const data = text ? JSON.parse(text) : null
  if (!response.ok) {
    throw new ApiError(
      response.status,
      data?.code ?? `HTTP_${response.status}`,
      data?.title ?? response.statusText,
      data?.detail ?? 'The request failed.',
      data?.violations ?? [],
      data?.traceId,
    )
  }
  return data as T
}

// ---------- Endpoints ----------

export interface OrderListParams {
  status?: OrderStatus
  priority?: OrderPriority
  eligibleOnly?: boolean
  search?: string
  sort?: 'PickupAt' | 'DeliveryDeadline' | 'Priority' | 'CargoWeightKg' | 'OrderNumber' | 'CustomerName'
  dir?: SortDirection
  page?: number
  pageSize?: number
}

export interface AssignmentListParams {
  status?: AssignmentStatus
  priority?: OrderPriority
  driverId?: number
  vehicleId?: number
  orderId?: number
  search?: string
  sort?: 'StartAt' | 'CreatedAt' | 'AssignmentNumber' | 'Status' | 'Priority' | 'OrderNumber' | 'DriverName'
  dir?: SortDirection
  page?: number
  pageSize?: number
}

export interface CandidateParams {
  driverId?: number
  vehicleId?: number
  excludeAssignmentId?: number
}

export const api = {
  login: (email: string, password: string) => request<LoginResponse>('POST', '/auth/login', { body: { email, password } }),
  me: () => request<User>('GET', '/auth/me'),

  summary: () => request<DispatchSummary>('GET', '/dispatch/summary'),
  dashboardStats: () => request<DashboardStats>('GET', '/dashboard/stats'),
  driverProfile: () => request<DriverProfile>('GET', '/driver/me'),

  orders: (params: OrderListParams) => request<PagedResult<Order>>('GET', '/orders', { query: { ...params } }),
  candidates: (orderId: number, params: CandidateParams) =>
    request<Candidates>('GET', `/orders/${orderId}/candidates`, { query: { ...params } }),

  drivers: () => request<Driver[]>('GET', '/drivers'),
  vehicles: () => request<Vehicle[]>('GET', '/vehicles'),

  assignments: (params: AssignmentListParams) =>
    request<PagedResult<Assignment>>('GET', '/assignments', { query: { ...params } }),
  assignment: (id: number) => request<AssignmentDetail>('GET', `/assignments/${id}`),

  createAssignment: (body: { orderId: number; driverId: number; vehicleId: number; notes?: string }) =>
    request<Assignment>('POST', '/assignments', { body }),
  reassign: (id: number, body: { version: number; driverId?: number; vehicleId?: number; reason: string }) =>
    request<ReassignmentResult>('POST', `/assignments/${id}/reassign`, { body }),
  cancel: (id: number, body: { version: number; reason: string }) =>
    request<Assignment>('POST', `/assignments/${id}/cancel`, { body }),
  complete: (id: number, body: { version: number; reason?: string }) =>
    request<Assignment>('POST', `/assignments/${id}/complete`, { body }),
}
