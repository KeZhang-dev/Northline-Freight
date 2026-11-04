import { Link } from 'react-router'
import { ShieldAlert } from 'lucide-react'
import { homePathFor, useAuth } from '@/auth/AuthContext'
import { Button } from '@/components/ui/button'

export function ForbiddenPage() {
  const { user } = useAuth()
  return (
    <div className="flex flex-col items-center py-24 text-center" data-testid="forbidden">
      <ShieldAlert className="size-10 text-muted-foreground" />
      <h1 className="mt-4 text-xl font-semibold">Access denied</h1>
      <p className="mt-2 max-w-md text-sm text-muted-foreground">
        Your role ({user?.role}) does not have permission to view this page.
      </p>
      <Button asChild variant="outline" className="mt-6">
        <Link to={user ? homePathFor(user.role) : '/login'}>Go to my home page</Link>
      </Button>
    </div>
  )
}
