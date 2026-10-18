import { apiGet } from './client'
import type { ChoreHistoryItem, CursorPage, CompletionSource, CalendarDate } from './types'

export interface ChoreHistoryFilters {
  choreId?: string
  memberId?: string
  from?: CalendarDate
  to?: CalendarDate
  source?: CompletionSource
}

export const historyQueryKey = (filters: ChoreHistoryFilters) => ['chores', 'history', filters] as const

export function listChoreHistory(
  filters: ChoreHistoryFilters,
  cursor: string | null,
  pageSize: number,
  signal?: AbortSignal,
): Promise<CursorPage<ChoreHistoryItem>> {
  const search = new URLSearchParams({ pageSize: String(pageSize) })

  if (filters.choreId) {
    search.set('choreId', filters.choreId)
  }
  if (filters.memberId) {
    search.set('memberId', filters.memberId)
  }
  if (filters.from) {
    search.set('from', filters.from)
  }
  if (filters.to) {
    search.set('to', filters.to)
  }
  if (filters.source) {
    search.set('source', filters.source)
  }
  if (cursor) {
    search.set('cursor', cursor)
  }

  return apiGet<CursorPage<ChoreHistoryItem>>(`/chores/history?${search.toString()}`, signal)
}
