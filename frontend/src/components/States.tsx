import { AlertTriangle, Inbox, Loader2 } from 'lucide-react'
import { ApiError } from '@/lib/api'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import type { RuleViolation } from '@/lib/types'

export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-10 text-sm text-muted-foreground" data-testid="loading-state">
      <Loader2 className="size-4 animate-spin" /> {label}
    </div>
  )
}

export function EmptyState({ title, description }: { title: string; description?: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-1 py-10 text-center" data-testid="empty-state">
      <Inbox className="mb-2 size-8 text-muted-foreground/60" />
      <div className="text-sm font-medium">{title}</div>
      {description && <div className="max-w-sm text-sm text-muted-foreground">{description}</div>}
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const message = error instanceof Error ? error.message : 'Something went wrong.'
  return (
    <Alert variant="destructive" className="my-4" data-testid="error-state">
      <AlertTriangle />
      <AlertTitle>Could not load data</AlertTitle>
      <AlertDescription>
        <p>{message}</p>
        {onRetry && (
          <Button variant="outline" size="sm" className="mt-2" onClick={onRetry}>
            Try again
          </Button>
        )}
      </AlertDescription>
    </Alert>
  )
}

export function ViolationList({ violations }: { violations: RuleViolation[] }) {
  if (violations.length === 0) return null
  return (
    <ul className="mt-1 space-y-1" data-testid="violation-list">
      {violations.map((v, i) => (
        <li key={`${v.code}-${i}`} className="flex gap-2 text-sm" data-testid="violation" data-rule={v.rule} data-code={v.code}>
          <span className="shrink-0 font-mono text-xs leading-5 font-semibold">{v.rule}</span>
          <span>{v.message}</span>
        </li>
      ))}
    </ul>
  )
}

/** Displays an API error returned from a mutation, including every business-rule violation. */
export function ApiErrorAlert({ error }: { error: unknown }) {
  if (!error) return null
  const apiError = error instanceof ApiError ? error : null
  const violations = apiError?.violations ?? []
  // A single violation's message is already the detail; list them when there are several or for field errors.
  const listViolations = violations.length > 1 || apiError?.code === 'VALIDATION_FAILED'
  return (
    <Alert variant="destructive" data-testid="api-error" data-code={apiError?.code}>
      <AlertTriangle />
      <AlertTitle>{apiError?.title ?? 'Request failed'}</AlertTitle>
      <AlertDescription>
        <p>{error instanceof Error ? error.message : 'The request failed.'}</p>
        {listViolations && <ViolationList violations={violations} />}
      </AlertDescription>
    </Alert>
  )
}
