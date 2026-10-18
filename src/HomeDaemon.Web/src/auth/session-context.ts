import { createContext, useContext } from 'react'
import type { CurrentUser } from '../api/types'

export interface SessionValue {
  /** The signed-in account, or null when there is no session. */
  user: CurrentUser | null
  isLoading: boolean
  signIn: (email: string, password: string) => Promise<CurrentUser>
  signOut: () => Promise<void>
  /** Re-reads the account, for example after a password change clears the flag. */
  reload: () => Promise<void>
}

export const SessionContext = createContext<SessionValue | null>(null)

export function useSession(): SessionValue {
  const value = useContext(SessionContext)
  if (value === null) {
    throw new Error('useSession must be used inside a SessionProvider.')
  }

  return value
}

/** Whether the account still has to change its password before using the rest of the app. */
export function needsPasswordChange(user: CurrentUser | null): boolean {
  return user?.mustChangePassword === true
}
