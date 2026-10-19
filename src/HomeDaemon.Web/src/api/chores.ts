import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type {
  AssignmentPolicy,
  ChoreCompletionResult,
  ChoreDetail,
  ChoreListItem,
  ChoreWriteModel,
  CalendarDate,
  RecurrenceFrequency,
  RecurrenceMode,
} from './types'

export const choresQueryKey = ['chores'] as const

export const choreQueryKey = (choreId: string) => ['chores', choreId] as const

export interface ChoreFilters {
  roomId?: string
  isActive?: boolean
}

export function listChores(filters: ChoreFilters, signal?: AbortSignal): Promise<ChoreListItem[]> {
  const search = new URLSearchParams()
  if (filters.roomId) {
    search.set('roomId', filters.roomId)
  }
  if (filters.isActive !== undefined) {
    search.set('isActive', String(filters.isActive))
  }

  const query = search.toString()
  return apiGet<ChoreListItem[]>(`/chores${query ? `?${query}` : ''}`, signal)
}

export function getChore(choreId: string, signal?: AbortSignal): Promise<ChoreDetail> {
  return apiGet<ChoreDetail>(`/chores/${choreId}`, signal)
}

export function createChore(model: ChoreWriteModel): Promise<ChoreDetail> {
  return apiPost<ChoreDetail>('/chores', model)
}

export function updateChore(choreId: string, model: ChoreWriteModel): Promise<ChoreDetail> {
  return apiPut<ChoreDetail>(`/chores/${choreId}`, model)
}

/** Deletion is a soft delete: the chore disappears but its history is kept. */
export function deleteChore(choreId: string): Promise<undefined> {
  return apiDelete(`/chores/${choreId}`)
}

export interface ChoreImportRoomMapping {
  sourceName: string
  roomId: string | null
  create: boolean
}

export interface ChoreImportRow {
  line: number
  sourceId: string
  title: string
  description: string | null
  room: string | null
  estimatedMinutes: number
  isActive: boolean
  effortWeight: number | null
  preferSameRoomGrouping: boolean
  frequency: RecurrenceFrequency
  interval: number
  mode: RecurrenceMode
  anchorDate: CalendarDate | null
  assignmentPolicy: AssignmentPolicy
  fixedAssigneeMemberId: string | null
  syntheticCompletionDate: CalendarDate | null
  completions: CalendarDate[]
}

export interface ChoreImportResult {
  imported: number
  skipped: number
  completionsImported: number
  completionsSkipped: number
  recalculationRunId: string | null
}

/** Administrator-only bulk import of chore definitions and their execution history. */
export function importChores(model: {
  chores: ChoreImportRow[]
  rooms: ChoreImportRoomMapping[]
}): Promise<ChoreImportResult> {
  return apiPost<ChoreImportResult>('/chores/imports', model)
}

export function restoreChore(choreId: string): Promise<ChoreDetail> {
  return apiPost<ChoreDetail>(`/chores/${choreId}/restore`)
}

/** Copies a stored chore into the write model, so a partial change can be sent as a full PUT. */
export function toWriteModel(chore: ChoreDetail, overrides: Partial<ChoreWriteModel> = {}): ChoreWriteModel {
  return {
    title: chore.title,
    description: chore.description,
    roomId: chore.roomId,
    isActive: chore.isActive,
    estimatedMinutes: chore.estimatedMinutes,
    effortWeight: chore.effortWeight,
    preferSameRoomGrouping: chore.preferSameRoomGrouping,
    frequency: chore.frequency,
    interval: chore.interval,
    mode: chore.mode,
    anchorDate: chore.anchorDate,
    assignmentPolicy: chore.assignmentPolicy,
    fixedAssigneeMemberId: chore.fixedAssigneeMemberId,
    ...overrides,
  }
}

/** Pausing and resuming edit one field, but the API replaces the whole definition. */
export async function setChoreActive(choreId: string, isActive: boolean): Promise<ChoreDetail> {
  const chore = await getChore(choreId)
  return updateChore(choreId, toWriteModel(chore, { isActive }))
}

/**
 * Records a completion. The account and its linked household member are resolved by the
 * server from the session, so nothing about the person travels in the request.
 *
 * No idempotency key is sent on purpose: the server derives one per chore and day, which makes
 * a repeated tap harmless instead of creating a second completion.
 */
export function completeChore(choreId: string, completedDate: CalendarDate | null): Promise<ChoreCompletionResult> {
  return apiPost<ChoreCompletionResult>(`/chores/${choreId}/complete`, { completedDate, idempotencyKey: null })
}
