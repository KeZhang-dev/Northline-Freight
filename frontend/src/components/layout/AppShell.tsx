import { useState } from 'react'
import { NavLink, Outlet } from 'react-router'
import { LogOut, PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import { useAuth } from '@/auth/AuthContext'
import { modules } from '@/modules'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'

function Brand() {
  return <img src="/logo.png" alt="Northline Freight" className="h-5 w-auto" data-testid="brand-logo" />
}

const SIDEBAR_KEY = 'northline.sidebarCollapsed'

/** Collapsed state of the sidebar, remembered per browser. */
function useSidebarCollapsed() {
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(SIDEBAR_KEY) === '1'
    } catch {
      return false
    }
  })
  const toggle = () =>
    setCollapsed((c) => {
      try {
        localStorage.setItem(SIDEBAR_KEY, c ? '0' : '1')
      } catch {
        // Storage unavailable: keep the state for this page only.
      }
      return !c
    })
  return [collapsed, toggle] as const
}

function Sidebar({ collapsed }: { collapsed: boolean }) {
  return (
    <aside
      className={cn(
        'sticky top-0 flex h-screen shrink-0 flex-col bg-sidebar text-sidebar-foreground transition-[width] duration-200',
        collapsed ? 'w-16' : 'w-60',
      )}
      data-testid="sidebar"
      data-collapsed={collapsed}
    >
      <div className={cn('flex h-14 items-center border-b border-sidebar-border', collapsed ? 'justify-center' : 'px-4')}>
        {collapsed ? <img src="/favicon.png" alt="Northline Freight" className="size-6" data-testid="brand-logo" /> : <Brand />}
      </div>
      <nav className={cn('flex-1 overflow-y-auto p-2', collapsed ? 'space-y-1' : 'space-y-0.5')} aria-label="Main navigation">
        {modules.map((m) => {
          const link = (
            <NavLink
              key={m.key}
              to={m.path}
              data-testid={`nav-${m.key}`}
              aria-label={collapsed ? m.title : undefined}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 whitespace-nowrap rounded-md text-sm transition-colors',
                  collapsed ? 'justify-center py-2.5' : 'px-3 py-2',
                  'hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                  isActive && 'bg-sidebar-accent font-medium text-sidebar-accent-foreground',
                )
              }
            >
              <m.icon className={cn('shrink-0', collapsed ? 'size-5' : 'size-4')} />
              {!collapsed && <span className="truncate">{m.title}</span>}
            </NavLink>
          )
          // The trigger wraps the link in a div: Radix's Slot would otherwise merge NavLink's
          // className function as a string and drop its styles.
          return collapsed ? (
            <Tooltip key={m.key}>
              <TooltipTrigger asChild>
                <div>{link}</div>
              </TooltipTrigger>
              <TooltipContent side="right">{m.title}</TooltipContent>
            </Tooltip>
          ) : (
            link
          )
        })}
      </nav>
      {!collapsed && (
        <div className="border-t border-sidebar-border p-4 text-[11px] text-sidebar-foreground/60">
          Northline Freight &amp; Logistics Ltd
          <br />
          Auckland, New Zealand
        </div>
      )}
    </aside>
  )
}

export function UserMenu() {
  const { user, logout } = useAuth()
  if (!user) return null
  return (
    <div className="flex items-center gap-3">
      <div className="text-right leading-tight">
        <div className="text-sm font-medium" data-testid="current-user-name">
          {user.fullName}
        </div>
        <div className="text-xs text-muted-foreground">{user.email}</div>
      </div>
      <Badge variant="secondary" data-testid="current-user-role">
        {user.role}
      </Badge>
      <Button variant="ghost" size="sm" onClick={logout} data-testid="sign-out">
        <LogOut className="size-4" />
        <span className="hidden sm:inline">Sign out</span>
      </Button>
    </div>
  )
}

/** Layout for Admin and Dispatcher users: sidebar navigation plus workspace. */
export function AppShell() {
  const [collapsed, toggleSidebar] = useSidebarCollapsed()
  return (
    <div className="flex min-h-screen">
      <Sidebar collapsed={collapsed} />
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-14 items-center justify-between border-b bg-card/95 px-4 backdrop-blur md:px-6">
          <Button
            variant="ghost"
            size="icon"
            onClick={toggleSidebar}
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            data-testid="sidebar-toggle"
          >
            {collapsed ? <PanelLeftOpen className="size-5" /> : <PanelLeftClose className="size-5" />}
          </Button>
          <UserMenu />
        </header>
        <main className="flex-1 p-4 md:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

/** Minimal layout for the Driver role, which has no access to operational modules. */
export function DriverShell() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex h-14 items-center justify-between bg-sidebar px-6">
        <Brand />
        <div className="rounded-md bg-card px-3 py-1">
          <UserMenu />
        </div>
      </header>
      <main className="flex-1 p-4 md:p-8">
        <Outlet />
      </main>
    </div>
  )
}

export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: React.ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight" data-testid="page-title">
          {title}
        </h1>
        {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  )
}
