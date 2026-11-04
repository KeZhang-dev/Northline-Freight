import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router'
import { AuthProvider, homePathFor, useAuth } from '@/auth/AuthContext'
import { RequireAuth } from '@/auth/RequireAuth'
import { AppShell, DriverShell } from '@/components/layout/AppShell'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { DispatchPage } from '@/features/dispatch/DispatchPage'
import { modules } from '@/modules'
import { DashboardPage } from '@/pages/DashboardPage'
import { LoginPage } from '@/pages/LoginPage'
import { DriverHomePage, NotFoundPage, PlaceholderPage } from '@/pages/SimplePages'
import { ApiError } from '@/lib/api'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 15_000,
      refetchOnWindowFocus: true,
      // Do not retry client errors such as 401/403/404.
      retry: (count, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && count < 2,
    },
  },
})

function RootRedirect() {
  const { user, initialising } = useAuth()
  if (initialising) return null
  return <Navigate to={user ? homePathFor(user.role) : '/login'} replace />
}

const router = createBrowserRouter([
  { path: '/', element: <RootRedirect /> },
  { path: '/login', element: <LoginPage /> },
  {
    element: <RequireAuth roles={['Admin', 'Dispatcher']} />,
    children: [
      {
        element: <AppShell />,
        children: [
          { path: '/dashboard', element: <DashboardPage /> },
          { path: '/dispatch', element: <DispatchPage /> },
          ...modules.filter((m) => !m.implemented).map((m) => ({ path: m.path, element: <PlaceholderPage module={m} /> })),
        ],
      },
    ],
  },
  {
    element: <RequireAuth roles={['Driver']} />,
    children: [{ element: <DriverShell />, children: [{ path: '/driver', element: <DriverHomePage /> }] }],
  },
  { path: '*', element: <NotFoundPage /> },
])

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <TooltipProvider delayDuration={200}>
          <RouterProvider router={router} />
          <Toaster position="top-right" richColors closeButton />
        </TooltipProvider>
      </AuthProvider>
    </QueryClientProvider>
  )
}
