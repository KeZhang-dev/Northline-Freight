import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { Loader2, Truck } from 'lucide-react'
import { homePathFor, useAuth } from '@/auth/AuthContext'
import { ApiErrorAlert } from '@/components/States'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false)

  if (user) {
    return <Navigate to={homePathFor(user.role)} replace />
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const signedIn = await login(email, password)
      const from = (location.state as { from?: string } | null)?.from
      navigate(from && from !== '/login' ? from : homePathFor(signedIn.role), { replace: true })
    } catch (err) {
      setError(err)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="hidden flex-col justify-between bg-sidebar p-10 text-sidebar-foreground lg:flex">
        <div className="flex items-center gap-3">
          <div className="flex size-10 items-center justify-center rounded-md bg-sidebar-primary text-sidebar-primary-foreground">
            <Truck className="size-5" />
          </div>
          <div>
            <div className="font-semibold text-sidebar-accent-foreground">Northline Freight &amp; Logistics</div>
            <div className="text-sm">Transport Management System</div>
          </div>
        </div>
        <div className="max-w-md">
          <h2 className="text-2xl font-semibold text-sidebar-accent-foreground">Road freight operations, Auckland and the upper North Island.</h2>
          <p className="mt-3 text-sm">Plan daily dispatch, allocate drivers and vehicles, and keep every job accountable.</p>
        </div>
        <div className="text-xs text-sidebar-foreground/60">Internal system. Authorised users only.</div>
      </div>

      <div className="flex items-center justify-center p-6">
        <Card className="w-full max-w-sm">
          <CardHeader>
            <CardTitle className="text-xl">Sign in</CardTitle>
            <CardDescription>Use your Northline account to continue.</CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={onSubmit} className="space-y-4" data-testid="login-form" noValidate>
              <div className="space-y-2">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  autoComplete="username"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  data-testid="login-email"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  data-testid="login-password"
                />
              </div>
              {error !== null && <ApiErrorAlert error={error} />}
              <Button type="submit" className="w-full" disabled={submitting || !email || !password} data-testid="login-submit">
                {submitting && <Loader2 className="size-4 animate-spin" />}
                Sign in
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
