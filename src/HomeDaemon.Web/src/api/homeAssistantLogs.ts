import { apiGet } from './client'
import type { CursorPage, HomeAssistantCallLog, HomeAssistantCallStatus } from './types'

/**
 * Administrator diagnostics for outbound Home Assistant calls. Only administrators can call
 * the endpoint; the page also hides itself from other roles.
 */
export interface HomeAssistantLogFilters {
  status?: HomeAssistantCallStatus
  operation?: string
  entityId?: string
}

export const homeAssistantLogsQueryKey = (filters: HomeAssistantLogFilters) =>
  ['admin', 'home-assistant-logs', filters] as const

export function listHomeAssistantCallLogs(
  filters: HomeAssistantLogFilters,
  cursor: string | null,
  pageSize: number,
  signal?: AbortSignal,
): Promise<CursorPage<HomeAssistantCallLog>> {
  const search = new URLSearchParams({ pageSize: String(pageSize) })

  if (filters.status) {
    search.set('status', filters.status)
  }
  if (filters.operation) {
    search.set('operation', filters.operation)
  }
  if (filters.entityId) {
    search.set('entityId', filters.entityId)
  }
  if (cursor) {
    search.set('cursor', cursor)
  }

  return apiGet<CursorPage<HomeAssistantCallLog>>(`/admin/home-assistant-logs?${search.toString()}`, signal)
}
