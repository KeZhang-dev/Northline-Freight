import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ArrowDownUp, CheckCircle2, Search, XCircle } from 'lucide-react'
import { api, type OrderListParams } from '@/lib/api'
import { formatKg, formatRange } from '@/lib/format'
import type { OrderPriority, SortDirection } from '@/lib/types'
import { PriorityBadge } from '@/components/StatusBadges'
import { EmptyState, ErrorState, LoadingState } from '@/components/States'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import { useDebouncedValue } from './hooks'

type SortField = NonNullable<OrderListParams['sort']>

export function PendingOrdersPanel({ onAssign, canWrite }: { onAssign: (orderId: number) => void; canWrite: boolean }) {
  const [search, setSearch] = useState('')
  const [priority, setPriority] = useState<OrderPriority | 'all'>('all')
  const [scope, setScope] = useState<'eligible' | 'all'>('all')
  const [sort, setSort] = useState<SortField>('PickupAt')
  const [dir, setDir] = useState<SortDirection>('Asc')
  const debouncedSearch = useDebouncedValue(search)

  const params: OrderListParams = {
    status: 'Pending',
    eligibleOnly: scope === 'eligible' || undefined,
    priority: priority === 'all' ? undefined : priority,
    search: debouncedSearch || undefined,
    sort,
    dir,
    pageSize: 100,
  }
  const { data, isLoading, error, refetch, isFetching } = useQuery({
    queryKey: ['orders', 'pending', params],
    queryFn: () => api.orders(params),
    placeholderData: keepPreviousData,
  })

  return (
    <Card data-testid="pending-orders-panel">
      <CardHeader className="gap-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Pending transport orders</CardTitle>
          {data && (
            <span className="text-xs text-muted-foreground" data-testid="pending-orders-count">
              {data.totalCount} {data.totalCount === 1 ? 'order' : 'orders'}
            </span>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <div className="relative min-w-48 flex-1">
            <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search order, customer, location…"
              className="pl-8"
              data-testid="orders-search"
            />
          </div>
          <Select value={scope} onValueChange={(v) => setScope(v as 'eligible' | 'all')}>
            <SelectTrigger className="w-40" data-testid="orders-scope">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All pending</SelectItem>
              <SelectItem value="eligible">Eligible only</SelectItem>
            </SelectContent>
          </Select>
          <Select value={priority} onValueChange={(v) => setPriority(v as OrderPriority | 'all')}>
            <SelectTrigger className="w-36" data-testid="orders-priority-filter">
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
          <Select value={sort} onValueChange={(v) => setSort(v as SortField)}>
            <SelectTrigger className="w-40" data-testid="orders-sort">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="PickupAt">Sort: Pickup time</SelectItem>
              <SelectItem value="DeliveryDeadline">Sort: Deadline</SelectItem>
              <SelectItem value="Priority">Sort: Priority</SelectItem>
              <SelectItem value="CargoWeightKg">Sort: Weight</SelectItem>
              <SelectItem value="OrderNumber">Sort: Order ID</SelectItem>
              <SelectItem value="CustomerName">Sort: Customer</SelectItem>
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            size="icon"
            onClick={() => setDir(dir === 'Asc' ? 'Desc' : 'Asc')}
            title={dir === 'Asc' ? 'Ascending' : 'Descending'}
            data-testid="orders-sort-direction"
            data-direction={dir}
          >
            <ArrowDownUp className="size-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent className={isFetching && !isLoading ? 'opacity-70 transition-opacity' : undefined}>
        {isLoading && <LoadingState label="Loading orders…" />}
        {error && <ErrorState error={error} onRetry={refetch} />}
        {data && data.items.length === 0 && (
          <EmptyState title="No pending orders" description="No pending orders match the current filters." />
        )}
        {data && data.items.length > 0 && (
          <Table data-testid="pending-orders-table">
            <TableHeader>
              <TableRow>
                <TableHead>Order</TableHead>
                <TableHead>Route</TableHead>
                <TableHead className="text-right">Weight</TableHead>
                <TableHead>Pickup – deadline</TableHead>
                <TableHead>Priority</TableHead>
                <TableHead>Dispatch</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((o) => (
                <TableRow key={o.id} data-testid={`order-row-${o.orderNumber}`} data-eligible={o.isEligible}>
                  <TableCell>
                    <div className="font-mono text-xs font-semibold">{o.orderNumber}</div>
                    <div className="max-w-44 truncate text-xs text-muted-foreground" title={o.customerName}>
                      {o.customerName}
                    </div>
                  </TableCell>
                  <TableCell className="text-xs">
                    <div>{o.pickupLocation}</div>
                    <div className="text-muted-foreground">→ {o.deliveryLocation}</div>
                  </TableCell>
                  <TableCell className="text-right text-xs tabular-nums">{formatKg(o.cargoWeightKg)}</TableCell>
                  <TableCell className="text-xs whitespace-nowrap">{formatRange(o.pickupAt, o.deliveryDeadline)}</TableCell>
                  <TableCell>
                    <PriorityBadge priority={o.priority} />
                  </TableCell>
                  <TableCell>
                    {o.isEligible ? (
                      <span className="inline-flex items-center gap-1 text-xs text-emerald-700" data-testid="order-eligible">
                        <CheckCircle2 className="size-3.5" /> Ready
                      </span>
                    ) : (
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <span className="inline-flex cursor-help items-center gap-1 text-xs text-red-700" data-testid="order-ineligible">
                            <XCircle className="size-3.5" /> Not eligible
                          </span>
                        </TooltipTrigger>
                        <TooltipContent className="max-w-xs">
                          {o.ineligibilityReasons.map((r) => (
                            <div key={r.code}>
                              {r.rule}: {r.message}
                            </div>
                          ))}
                        </TooltipContent>
                      </Tooltip>
                    )}
                  </TableCell>
                  <TableCell className="text-right">
                    {canWrite && (
                      <Button size="sm" variant="outline" disabled={!o.isEligible} onClick={() => onAssign(o.id)} data-testid={`assign-${o.orderNumber}`}>
                        Assign
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  )
}
