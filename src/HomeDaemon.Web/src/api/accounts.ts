import { apiGet, apiPost, apiPut } from './client'
import type { CursorPage } from './types'

/**
 * Account administration. Only administrators can call these endpoints; accounts are
 * deactivated rather than deleted so completion history keeps its references.
 */
export interface UserAccount {
  id: string
  email: string
  displayName: string | null
  isActive: boolean
  mustChangePassword: boolean
  householdMemberId: string | null
  roles: string[]
}

export interface TemporaryPassword {
  user: UserAccount
  temporaryPassword: string
}

export interface AccountWriteModel {
  email: string
  displayName: string | null
  householdMemberId: string | null
  isAdmin: boolean
}

export const accountsQueryKey = ['admin', 'users'] as const

export function listAccounts(cursor: string | null, pageSize: number, signal?: AbortSignal): Promise<CursorPage<UserAccount>> {
  const search = new URLSearchParams({ pageSize: String(pageSize) })
  if (cursor) {
    search.set('cursor', cursor)
  }

  return apiGet<CursorPage<UserAccount>>(`/admin/users?${search.toString()}`, signal)
}

export function createAccount(model: AccountWriteModel): Promise<TemporaryPassword> {
  return apiPost<TemporaryPassword>('/admin/users', model)
}

export function updateAccount(userId: string, model: AccountWriteModel): Promise<UserAccount> {
  return apiPut<UserAccount>(`/admin/users/${userId}`, model)
}

export function deactivateAccount(userId: string): Promise<UserAccount> {
  return apiPost<UserAccount>(`/admin/users/${userId}/deactivate`)
}

export function reactivateAccount(userId: string): Promise<UserAccount> {
  return apiPost<UserAccount>(`/admin/users/${userId}/reactivate`)
}

/** Issues a new one-time password and forces a change at the next sign-in. */
export function resetAccountPassword(userId: string): Promise<TemporaryPassword> {
  return apiPost<TemporaryPassword>(`/admin/users/${userId}/reset-password`)
}
