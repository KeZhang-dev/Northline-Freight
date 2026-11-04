import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { Plus } from 'lucide-react'
import { useAuth } from '@/auth/AuthContext'
import { PageHeader } from '@/components/layout/AppShell'
import { Button } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { AssignDialog } from './AssignDialog'
import { AssignmentDrawer } from './AssignmentDrawer'
import { AssignmentsPanel } from './AssignmentsPanel'
import { KpiCards } from './KpiCards'
import { PendingOrdersPanel } from './PendingOrdersPanel'
import { ResourcesPanel } from './ResourcesPanel'

export function DispatchPage() {
  const { hasRole } = useAuth()
  const canWrite = hasRole('Admin', 'Dispatcher')
  const [searchParams, setSearchParams] = useSearchParams()
  const tab = searchParams.get('tab') === 'assignments' ? 'assignments' : 'board'
  const openAssignmentId = searchParams.get('assignment') ? Number(searchParams.get('assignment')) : null

  const [assignOpen, setAssignOpen] = useState(false)
  const [assignOrderId, setAssignOrderId] = useState<number | null>(null)

  // Tab and open assignment are kept in the URL so views can be bookmarked and shared.
  function setParam(key: string, value: string | null) {
    const next = new URLSearchParams(searchParams)
    if (value === null) next.delete(key)
    else next.set(key, value)
    setSearchParams(next, { replace: key === 'assignment' })
  }

  function openAssign(orderId: number | null) {
    setAssignOrderId(orderId)
    setAssignOpen(true)
  }

  return (
    <div data-testid="dispatch-page">
      <PageHeader
        title="Dispatch Management"
        description="Allocate drivers and vehicles to pending transport orders and manage active assignments."
        actions={
          canWrite && (
            <Button onClick={() => openAssign(null)} data-testid="new-assignment">
              <Plus className="size-4" /> New assignment
            </Button>
          )
        }
      />

      <KpiCards />

      <Tabs value={tab} onValueChange={(v) => setParam('tab', v === 'board' ? null : v)} className="mt-6">
        <TabsList>
          <TabsTrigger value="board" data-testid="tab-board">
            Dispatch board
          </TabsTrigger>
          <TabsTrigger value="assignments" data-testid="tab-assignments">
            Assignments
          </TabsTrigger>
        </TabsList>
        <TabsContent value="board" className="mt-4">
          <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_380px]">
            <PendingOrdersPanel onAssign={openAssign} canWrite={canWrite} />
            <ResourcesPanel onOpenAssignment={(id) => setParam('assignment', String(id))} />
          </div>
        </TabsContent>
        <TabsContent value="assignments" className="mt-4">
          <AssignmentsPanel onOpen={(id) => setParam('assignment', String(id))} />
        </TabsContent>
      </Tabs>

      <AssignDialog open={assignOpen} onOpenChange={setAssignOpen} initialOrderId={assignOrderId} />
      <AssignmentDrawer
        assignmentId={openAssignmentId}
        onClose={() => setParam('assignment', null)}
        onNavigate={(id) => setParam('assignment', String(id))}
      />
    </div>
  )
}
