import { Navigate, Outlet, useLocation } from 'react-router'
import { Loader2 } from 'lucide-react'
import { useAuth } from './AuthContext'
import type { UserRole } from '@/lib/types'
import { ForbiddenPage } from '@/pages/ForbiddenPage'

/**
 * Route guard. Unauthenticated users are sent to the login page; authenticated users
 * without one of the required roles see the 403 page. The API enforces the same rules.
 */
export function RequireAuth({ roles }: { roles?: UserRole[] }) {
  const { user, initialising, hasRole } = useAuth()
  const location = useLocation()

  if (initialising) {
    return (
      <div className="flex h-screen items-center justify-center text-muted-foreground" data-testid="auth-loading">
        <Loader2 className="mr-2 size-5 animate-spin" /> Checking your session…
      </div>
    )
  }
  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }
  if (roles && !hasRole(...roles)) {
    return <ForbiddenPage />
  }
  return <Outlet />
}
