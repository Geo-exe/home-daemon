import { useMemo, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { loadCurrentUser, login, logout, sessionQueryKey } from '../api/auth'
import type { CurrentUser } from '../api/types'
import { SessionContext, type SessionValue } from './session-context'

/**
 * Owns the session. The account is read from the server on load and cached; a 401 simply means
 * "not signed in", so it is never retried or shown as an error.
 */
export function SessionProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()

  const session = useQuery({
    queryKey: sessionQueryKey,
    queryFn: ({ signal }) => loadCurrentUser(signal),
    retry: false,
    staleTime: 30_000,
  })

  const value = useMemo<SessionValue>(
    () => ({
      user: session.data ?? null,
      isLoading: session.isPending,
      signIn: async (email, password) => {
        const user: CurrentUser = await login(email, password)
        queryClient.setQueryData(sessionQueryKey, user)
        return user
      },
      signOut: async () => {
        await logout()

        // Drop everything cached for the previous session. The session entry itself is updated
        // rather than cleared: an actively observed query would keep serving its last data, and
        // the app would stay on a protected screen instead of returning to sign-in.
        queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== sessionQueryKey[0] })
        queryClient.setQueryData(sessionQueryKey, null)
      },
      reload: async () => {
        await queryClient.invalidateQueries({ queryKey: sessionQueryKey })
      },
    }),
    [queryClient, session.data, session.isPending],
  )

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}
