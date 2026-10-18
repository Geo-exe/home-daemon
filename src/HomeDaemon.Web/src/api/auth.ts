import { ApiError, apiGet, apiPost, resetAntiforgeryToken } from './client'
import type { CurrentUser } from './types'

export const sessionQueryKey = ['session'] as const

export async function getCurrentUser(signal?: AbortSignal): Promise<CurrentUser> {
  return apiGet<CurrentUser>('/auth/me', signal)
}

/**
 * Resolves to null when there is no session: a 401 here is the normal answer for "signed out",
 * not a failure worth surfacing or retrying.
 */
export async function loadCurrentUser(signal?: AbortSignal): Promise<CurrentUser | null> {
  try {
    return await getCurrentUser(signal)
  } catch (error) {
    if (error instanceof ApiError && error.isUnauthorized) {
      return null
    }

    throw error
  }
}

export async function login(email: string, password: string): Promise<CurrentUser> {
  const user = await apiPost<CurrentUser>('/auth/login', { email, password })
  // A new session may rotate the antiforgery pairing; start from a clean slate.
  resetAntiforgeryToken()
  return user
}

export async function logout(): Promise<void> {
  await apiPost<void>('/auth/logout')
  resetAntiforgeryToken()
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  await apiPost<void>('/auth/change-password', { currentPassword, newPassword })
}
