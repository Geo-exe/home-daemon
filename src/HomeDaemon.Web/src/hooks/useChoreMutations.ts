import { useQueryClient, useMutation } from '@tanstack/react-query'
import { choreQueryKey, choresQueryKey, completeChore, deleteChore, restoreChore, setChoreActive } from '../api/chores'
import type { ChoreDetail } from '../api/types'

/**
 * Chore mutations in one place, so every screen refreshes the same queries after a change.
 * Completing deliberately sends no idempotency key: the server derives one per chore and day,
 * which makes a repeated tap harmless.
 */
function useChoreInvalidation() {
  const queryClient = useQueryClient()

  return async (choreId?: string) => {
    await queryClient.invalidateQueries({ queryKey: choresQueryKey })
    if (choreId) {
      await queryClient.invalidateQueries({ queryKey: choreQueryKey(choreId) })
    }
  }
}

export function useCompleteChore() {
  const invalidate = useChoreInvalidation()

  return useMutation({
    mutationFn: ({ choreId, completedDate }: { choreId: string; completedDate: string | null }) =>
      completeChore(choreId, completedDate),
    onSuccess: (_result, variables) => invalidate(variables.choreId),
  })
}

export function useSetChoreActive() {
  const invalidate = useChoreInvalidation()

  return useMutation({
    mutationFn: ({ choreId, isActive }: { choreId: string; isActive: boolean }) => setChoreActive(choreId, isActive),
    onSuccess: (_result, variables) => invalidate(variables.choreId),
  })
}

export function useDeleteChore() {
  const invalidate = useChoreInvalidation()

  return useMutation({
    mutationFn: (choreId: string) => deleteChore(choreId),
    onSuccess: (_result, choreId) => invalidate(choreId),
  })
}

export function useRestoreChore() {
  const invalidate = useChoreInvalidation()

  return useMutation({
    mutationFn: (choreId: string): Promise<ChoreDetail> => restoreChore(choreId),
    onSuccess: (_result, choreId) => invalidate(choreId),
  })
}
