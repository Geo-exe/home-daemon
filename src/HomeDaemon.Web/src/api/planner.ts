import { apiGet, apiPatch, apiPost } from './client'
import type { CalendarDate, Planner } from './types'

export const plannerQueryKey = ['planner'] as const

export function getPlanner(from: CalendarDate, to: CalendarDate, signal?: AbortSignal): Promise<Planner> {
  const search = new URLSearchParams({ from, to })
  return apiGet<Planner>(`/planner?${search.toString()}`, signal)
}

/**
 * Moves an occurrence to another day and/or person. The server records this as a manual
 * override, which locks the occurrence until it is completed or explicitly unlocked.
 */
export function updateOccurrence(choreId: string, date: CalendarDate, memberId: string): Promise<unknown> {
  return apiPatch(`/planner/occurrences/${choreId}`, { date, memberId })
}

/** Releases a manual override so the next recalculation can place the chore again. */
export function unlockOccurrence(choreId: string): Promise<unknown> {
  return apiPost(`/planner/occurrences/${choreId}/unlock`)
}
