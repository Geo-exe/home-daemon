import { apiGet, apiPost, resetAntiforgeryToken } from './client'
import type { CurrentUser } from './types'

export const sessionQueryKey = ['session'] as const

export async function getCurrentUser(signal?: AbortSignal): Promise<CurrentUser> {
  return apiGet<CurrentUser>('/auth/me', signal)
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
