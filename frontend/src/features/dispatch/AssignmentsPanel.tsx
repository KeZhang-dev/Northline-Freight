import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ArrowDownUp, ChevronLeft, ChevronRight, Search } from 'lucide-react'
import { api, type AssignmentListParams } from '@/lib/api'
import { formatRange } from '@/lib/format'
import type { AssignmentStatus, OrderPriority, SortDirection } from '@/lib/types'
import { AssignmentStatusBadge, PriorityBadge } from '@/components/StatusBadges'
import { EmptyState, ErrorState, LoadingState } from '@/components/States'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useDebouncedValue } from './hooks'

type SortField = NonNullable<AssignmentListParams['sort']>
const PAGE_SIZE = 10

export function AssignmentsPanel({ onOpen }: { onOpen: (id: number) => void }) {
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<AssignmentStatus | 'all'>('Assigned')
  const [priority, setPriority] = useState<OrderPriority | 'all'>('all')
  const [sort, setSort] = useState<SortField>('StartAt')
  const [dir, setDir] = useState<SortDirection>('Asc')
  const [page, setPage] = useState(1)
  const debouncedSearch = useDebouncedValue(search)

  const params: AssignmentListParams = {
    status: status === 'all' ? undefined : status,
    priority: priority === 'all' ? undefined : priority,
    search: debouncedSearch || undefined,
    sort,
    dir,
    page,
    pageSize: PAGE_SIZE,
  }
  const { data, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ['assignments', params],
    queryFn: () => api.assignments(params),
    placeholderData: keepPreviousData,
  })

  // Any filter change returns to the first page.
  function update<T>(setter: (v: T) => void) {
    return (v: T) => {
      setter(v)
      setPage(1)
    }
  }

  return (
    <Card data-testid="assignments-panel">
      <CardHeader className="gap-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Dispatch assignments</CardTitle>
          {data && (
            <span className="text-xs text-muted-foreground" data-testid="assignments-count">
              {data.totalCount} {data.totalCount === 1 ? 'assignment' : 'assignments'}
            </span>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <div className="relative min-w-56 flex-1">
            <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(e) => update(setSearch)(e.target.value)}
              placeholder="Search assignment, order, customer, driver, rego…"
              className="pl-8"
              data-testid="assignments-search"
            />
          </div>
          <Select value={status} onValueChange={update((v: string) => setStatus(v as AssignmentStatus | 'all'))}>
            <SelectTrigger className="w-40" data-testid="assignments-status-filter">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              <SelectItem value="Assigned">Assigned</SelectItem>
              <SelectItem value="Reassigned">Reassigned</SelectItem>
              <SelectItem value="Cancelled">Cancelled</SelectItem>
              <SelectItem value="Completed">Completed</SelectItem>
            </SelectContent>
          </Select>
          <Select value={priority} onValueChange={update((v: string) => setPriority(v as OrderPriority | 'all'))}>
            <SelectTrigger className="w-36" data-testid="assignments-priority-filter">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All priorities</SelectItem>
              <SelectItem value="Urgent">Urgent</SelectItem>
              <SelectItem value="High">High</SelectItem>
              <SelectItem value="Normal">Normal</SelectItem>
              <SelectItem value="Low">Low</SelectItem>
            </SelectContent>
          </Select>
          <Select value={sort} onValueChange={update((v: string) => setSort(v as SortField))}>
            <SelectTrigger className="w-44" data-testid="assignments-sort">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="StartAt">Sort: Start time</SelectItem>
              <SelectItem value="CreatedAt">Sort: Created</SelectItem>
              <SelectItem value="AssignmentNumber">Sort: Assignment ID</SelectItem>
              <SelectItem value="OrderNumber">Sort: Order ID</SelectItem>
              <SelectItem value="Priority">Sort: Priority</SelectItem>
              <SelectItem value="Status">Sort: Status</SelectItem>
              <SelectItem value="DriverName">Sort: Driver</SelectItem>
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            size="icon"
            onClick={() => update(setDir)(dir === 'Asc' ? 'Desc' : 'Asc')}
            title={dir === 'Asc' ? 'Ascending' : 'Descending'}
            data-testid="assignments-sort-direction"
            data-direction={dir}
          >
            <ArrowDownUp className="size-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent className={isFetching && !isLoading ? 'opacity-70 transition-opacity' : undefined}>
        {isLoading && <LoadingState label="Loading assignments…" />}
        {error && <ErrorState error={error} onRetry={refetch} />}
        {data && data.items.length === 0 && <EmptyState title="No assignments" description="No assignments match the current filters." />}
        {data && data.items.length > 0 && (
          <>
            <Table data-testid="assignments-table">
              <TableHeader>
                <TableRow>
                  <TableHead>Assignment</TableHead>
                  <TableHead>Order</TableHead>
                  <TableHead>Driver</TableHead>
                  <TableHead>Vehicle</TableHead>
                  <TableHead>Window</TableHead>
                  <TableHead>Priority</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((a) => (
                  <TableRow
                    key={a.id}
                    className="cursor-pointer"
                    onClick={() => onOpen(a.id)}
                    data-testid={`assignment-row-${a.assignmentNumber}`}
                    tabIndex={0}
                    onKeyDown={(e) => e.key === 'Enter' && onOpen(a.id)}
                  >
                    <TableCell className="font-mono text-xs font-semibold">{a.assignmentNumber}</TableCell>
                    <TableCell className="text-xs">
                      <div className="font-mono">{a.order.orderNumber}</div>
                      <div className="max-w-40 truncate text-muted-foreground">{a.order.customerName}</div>
                    </TableCell>
                    <TableCell className="text-xs">
                      <div>{a.driver.fullName}</div>
                      <div className="text-muted-foreground">{a.driver.driverCode}</div>
                    </TableCell>
                    <TableCell className="text-xs">
                      <div>{a.vehicle.registrationNumber}</div>
                      <div className="text-muted-foreground">{a.vehicle.vehicleCode}</div>
                    </TableCell>
                    <TableCell className="text-xs whitespace-nowrap">{formatRange(a.startAt, a.endAt)}</TableCell>
                    <TableCell>
                      <PriorityBadge priority={a.order.priority} />
                    </TableCell>
                    <TableCell>
                      <AssignmentStatusBadge status={a.status} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <div className="mt-3 flex items-center justify-between text-xs text-muted-foreground">
              <span data-testid="assignments-page-info">
                Page {data.page} of {Math.max(data.totalPages, 1)}
              </span>
              <div className="flex gap-1">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)} data-testid="assignments-prev-page">
                  <ChevronLeft className="size-4" /> Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= data.totalPages}
                  onClick={() => setPage(page + 1)}
                  data-testid="assignments-next-page"
                >
                  Next <ChevronRight className="size-4" />
                </Button>
              </div>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  )
}
