import { NavLink, Outlet } from 'react-router'
import { LogOut } from 'lucide-react'
import { useAuth } from '@/auth/AuthContext'
import { modules } from '@/modules'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

function Brand() {
  return <img src="/logo.png" alt="Northline Freight" className="h-6 w-auto" data-testid="brand-logo" />
}

function Sidebar() {
  return (
    <aside
      className="sticky top-0 flex h-screen w-60 shrink-0 flex-col bg-sidebar text-sidebar-foreground"
      data-testid="sidebar"
    >
      <div className="flex h-14 items-center border-b border-sidebar-border px-4">
        <Brand />
      </div>
      <nav className="flex-1 space-y-0.5 overflow-y-auto p-2" aria-label="Main navigation">
        {modules.map((m) => (
          <NavLink
            key={m.key}
            to={m.path}
            data-testid={`nav-${m.key}`}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 whitespace-nowrap rounded-md px-3 py-2 text-sm transition-colors',
                'hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                isActive && 'bg-sidebar-accent font-medium text-sidebar-accent-foreground',
              )
            }
          >
            <m.icon className="size-4 shrink-0" />
            <span className="truncate">{m.title}</span>
          </NavLink>
        ))}
      </nav>
      <div className="border-t border-sidebar-border p-4 text-[11px] text-sidebar-foreground/60">
        Northline Freight &amp; Logistics Ltd
        <br />
        Auckland, New Zealand
      </div>
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
  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-14 items-center justify-end border-b bg-card/95 px-6 backdrop-blur">
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
