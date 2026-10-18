import { apiGet, apiPost } from './client'
import type { RecalculationAccepted, ScheduleRun } from './types'

export const scheduleRunQueryKey = (runId: string) => ['schedule-run', runId] as const

/** Queues a recalculation. The work happens in a background worker, so the call returns at once. */
export function startRecalculation(): Promise<RecalculationAccepted> {
  return apiPost<RecalculationAccepted>('/scheduling/recalculations')
}

export function getScheduleRun(runId: string, signal?: AbortSignal): Promise<ScheduleRun> {
  return apiGet<ScheduleRun>(`/scheduling/recalculations/${runId}`, signal)
}
