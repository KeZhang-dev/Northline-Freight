import { Link } from 'react-router'
import { useQuery } from '@tanstack/react-query'
import { Construction } from 'lucide-react'
import { homePathFor, useAuth } from '@/auth/AuthContext'
import { PageHeader } from '@/components/layout/AppShell'
import { EmploymentStatusBadge } from '@/components/StatusBadges'
import { ErrorState, LoadingState } from '@/components/States'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { api } from '@/lib/api'
import type { ModuleDefinition } from '@/modules'

/** Professional placeholder for modules outside the V1 scope. Contains no operational controls. */
export function PlaceholderPage({ module }: { module: ModuleDefinition }) {
  return (
    <div data-testid={`placeholder-${module.key}`}>
      <PageHeader title={module.title} description={module.description} />
      <Card>
        <CardContent className="flex flex-col items-center px-6 py-14 text-center">
          <div className="mb-4 flex size-12 items-center justify-center rounded-full bg-muted">
            <module.icon className="size-6 text-muted-foreground" />
          </div>
          <div className="mb-1 flex items-center gap-2 text-base font-semibold">
            <Construction className="size-4 text-amber-600" />
            Module not yet implemented
          </div>
          <p className="max-w-lg text-sm text-muted-foreground" data-testid="placeholder-message">
            {module.title} is planned for a future release of the Northline TMS. In this version only Dispatch Management is
            operational.
          </p>
          {module.planned && (
            <div className="mt-6 w-full max-w-md rounded-md border bg-muted/40 p-4 text-left">
              <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">Planned scope</div>
              <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
                {module.planned.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

export function DriverHomePage() {
  const { data, isLoading, error, refetch } = useQuery({ queryKey: ['driver-profile'], queryFn: api.driverProfile })

  return (
    <div className="mx-auto max-w-2xl" data-testid="driver-home">
      <PageHeader title="Driver home" description="Your Northline account" />
      {isLoading && <LoadingState />}
      {error && <ErrorState error={error} onRetry={refetch} />}
      {data && (
        <Card>
          <CardHeader>
            <CardTitle>Kia ora, {data.user.fullName}</CardTitle>
            <CardDescription>{data.message}</CardDescription>
          </CardHeader>
          <CardContent>
            {data.driver ? (
              <dl className="grid grid-cols-2 gap-4 text-sm" data-testid="driver-profile">
                <div>
                  <dt className="text-muted-foreground">Driver ID</dt>
                  <dd className="font-medium">{data.driver.driverCode}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">Licence class</dt>
                  <dd className="font-medium">Class {data.driver.licenceClass}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">Employment status</dt>
                  <dd>
                    <EmploymentStatusBadge status={data.driver.employmentStatus} />
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">Email</dt>
                  <dd className="font-medium">{data.user.email}</dd>
                </div>
              </dl>
            ) : (
              <p className="text-sm text-muted-foreground">Your account is not linked to a driver record.</p>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  )
}

export function NotFoundPage() {
  const { user } = useAuth()
  return (
    <div className="flex flex-col items-center py-24 text-center" data-testid="not-found">
      <div className="text-5xl font-semibold text-muted-foreground">404</div>
      <p className="mt-2 text-sm text-muted-foreground">The page you requested does not exist.</p>
      <Button asChild variant="outline" className="mt-6">
        <Link to={user ? homePathFor(user.role) : '/login'}>Go to home</Link>
      </Button>
    </div>
  )
}

