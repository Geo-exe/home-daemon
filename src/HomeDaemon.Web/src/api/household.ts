import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type { Household, HouseholdMember, Room } from './types'

export const householdQueryKey = ['household'] as const
export const roomsQueryKey = ['household', 'rooms'] as const
export const membersQueryKey = ['household', 'members'] as const

export interface HouseholdWriteModel {
  name: string
  timeZoneId: string
  planningHorizonDays: number
  /** `HH:mm`; the API expects a time value. */
  dailyWindowStart: string
  dailyWindowEnd: string
  maxChoreMinutesPerWeekday: number | null
  maxChoreMinutesPerWeekendDay: number | null
}

export interface RoomWriteModel {
  name: string
  sortOrder: number
}

export interface MemberWriteModel {
  name: string
  color: string | null
  isAssignable: boolean
  todoListEntityId: string | null
  personEntityId: string | null
  calendarEntityIds: string[]
}

export function getHousehold(signal?: AbortSignal): Promise<Household> {
  return apiGet<Household>('/household', signal)
}

export function updateHousehold(model: HouseholdWriteModel): Promise<Household> {
  return apiPut<Household>('/household', {
    name: model.name,
    timeZoneId: model.timeZoneId,
    planningHorizonDays: model.planningHorizonDays,
    // The API binds TimeOnly, which round-trips seconds.
    dailyWindowStart: `${model.dailyWindowStart}:00`,
    dailyWindowEnd: `${model.dailyWindowEnd}:00`,
    maxChoreMinutesPerWeekday: model.maxChoreMinutesPerWeekday,
    maxChoreMinutesPerWeekendDay: model.maxChoreMinutesPerWeekendDay,
  })
}

export function getRooms(signal?: AbortSignal): Promise<Room[]> {
  return apiGet<Room[]>('/household/rooms', signal)
}

export function createRoom(model: RoomWriteModel): Promise<Room> {
  return apiPost<Room>('/household/rooms', model)
}

export function updateRoom(roomId: string, model: RoomWriteModel): Promise<Room> {
  return apiPut<Room>(`/household/rooms/${roomId}`, model)
}

/** Refused with a conflict when a chore still uses the room. */
export function deleteRoom(roomId: string): Promise<undefined> {
  return apiDelete(`/household/rooms/${roomId}`)
}

export function getMembers(signal?: AbortSignal): Promise<HouseholdMember[]> {
  return apiGet<HouseholdMember[]>('/household/members', signal)
}

export function createMember(model: MemberWriteModel): Promise<HouseholdMember> {
  return apiPost<HouseholdMember>('/household/members', model)
}

export function updateMember(memberId: string, model: MemberWriteModel): Promise<HouseholdMember> {
  return apiPut<HouseholdMember>(`/household/members/${memberId}`, model)
}

/** Refused with a conflict when the person appears in chores or in the completion history. */
export function deleteMember(memberId: string): Promise<undefined> {
  return apiDelete(`/household/members/${memberId}`)
}
