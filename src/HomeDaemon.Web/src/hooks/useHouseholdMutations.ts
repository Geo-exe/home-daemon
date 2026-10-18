import { useMutation, useQueryClient } from '@tanstack/react-query'
import { choresQueryKey } from '../api/chores'
import {
  createHousehold,
  createMember,
  createRoom,
  deleteMember,
  deleteRoom,
  householdQueryKey,
  membersQueryKey,
  roomsQueryKey,
  updateHousehold,
  updateMember,
  updateRoom,
  type HouseholdCreateModel,
  type HouseholdWriteModel,
  type MemberWriteModel,
  type RoomWriteModel,
} from '../api/household'
import { plannerQueryKey } from '../api/planner'

function useInvalidate(keys: readonly (readonly unknown[])[]) {
  const queryClient = useQueryClient()

  return async () => {
    for (const key of keys) {
      await queryClient.invalidateQueries({ queryKey: key })
    }
  }
}

export function useCreateHousehold() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (model: HouseholdCreateModel) => createHousehold(model),
    onSuccess: (household) => {
      // Publish the created household right away, so the first-run gate stops showing its
      // setup screen without waiting for a refetch.
      queryClient.setQueryData(householdQueryKey, household)
      void queryClient.invalidateQueries({ queryKey: membersQueryKey })
      void queryClient.invalidateQueries({ queryKey: roomsQueryKey })
    },
  })
}

export function useUpdateHousehold() {
  const invalidate = useInvalidate([householdQueryKey, plannerQueryKey])

  return useMutation({
    mutationFn: (model: HouseholdWriteModel) => updateHousehold(model),
    onSuccess: invalidate,
  })
}

export function useCreateRoom() {
  const invalidate = useInvalidate([roomsQueryKey, choresQueryKey, plannerQueryKey])

  return useMutation({
    mutationFn: (model: RoomWriteModel) => createRoom(model),
    onSuccess: invalidate,
  })
}

export function useUpdateRoom() {
  const invalidate = useInvalidate([roomsQueryKey, choresQueryKey, plannerQueryKey])

  return useMutation({
    mutationFn: ({ roomId, model }: { roomId: string; model: RoomWriteModel }) => updateRoom(roomId, model),
    onSuccess: invalidate,
  })
}

export function useDeleteRoom() {
  const invalidate = useInvalidate([roomsQueryKey, choresQueryKey, plannerQueryKey])

  return useMutation({
    mutationFn: (roomId: string) => deleteRoom(roomId),
    onSuccess: invalidate,
  })
}

export function useCreateMember() {
  const invalidate = useInvalidate([membersQueryKey, choresQueryKey, plannerQueryKey])

  return useMutation({
    mutationFn: (model: MemberWriteModel) => createMember(model),
    onSuccess: invalidate,
  })
}

export function useUpdateMember() {
  const invalidate = useInvalidate([membersQueryKey, choresQueryKey, plannerQueryKey])

  return useMutation({
    mutationFn: ({ memberId, model }: { memberId: string; model: MemberWriteModel }) => updateMember(memberId, model),
    onSuccess: invalidate,
  })
}

export function useDeleteMember() {
  const invalidate = useInvalidate([membersQueryKey, choresQueryKey, plannerQueryKey])

  return useMutation({
    mutationFn: (memberId: string) => deleteMember(memberId),
    onSuccess: invalidate,
  })
}
