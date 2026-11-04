import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, setUnauthorizedHandler, tokenStore } from '@/lib/api'
import type { User, UserRole } from '@/lib/types'

interface AuthState {
  user: User | null
  /** True while an existing session token is being validated on page load. */
  initialising: boolean
  login: (email: string, password: string) => Promise<User>
  logout: () => void
  hasRole: (...roles: UserRole[]) => boolean
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<User | null>(null)
  const [initialising, setInitialising] = useState(() => tokenStore.get() !== null)

  const logout = useCallback(() => {
    tokenStore.clear()
    setUser(null)
    queryClient.clear()
  }, [queryClient])

  useEffect(() => {
    setUnauthorizedHandler(logout)
    return () => setUnauthorizedHandler(null)
  }, [logout])

  useEffect(() => {
    if (!tokenStore.get()) return
    api
      .me()
      .then(setUser)
      .catch(() => tokenStore.clear())
      .finally(() => setInitialising(false))
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const result = await api.login(email, password)
    tokenStore.set(result.token)
    setUser(result.user)
    return result.user
  }, [])

  const value = useMemo<AuthState>(
    () => ({
      user,
      initialising,
      login,
      logout,
      hasRole: (...roles) => user !== null && roles.includes(user.role),
    }),
    [user, initialising, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}

/** Where each role lands after signing in. */
// eslint-disable-next-line react-refresh/only-export-components
export function homePathFor(role: UserRole) {
  return role === 'Driver' ? '/driver' : '/dashboard'
}
