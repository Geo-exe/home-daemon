import { useMutation, useQueryClient } from '@tanstack/react-query'
import { choresQueryKey } from '../api/chores'
import { plannerQueryKey, unlockOccurrence, updateOccurrence } from '../api/planner'
import { getScheduleRun, startRecalculation } from '../api/scheduling'
import type { CalendarDate, ScheduleRun } from '../api/types'

const pollIntervalMs = 1_000
const maxPolls = 45

function usePlannerInvalidation() {
  const queryClient = useQueryClient()

  return async () => {
    await queryClient.invalidateQueries({ queryKey: plannerQueryKey })
    await queryClient.invalidateQueries({ queryKey: choresQueryKey })
  }
}

export function useUpdateOccurrence() {
  const invalidate = usePlannerInvalidation()

  return useMutation({
    mutationFn: ({ choreId, date, memberId }: { choreId: string; date: CalendarDate; memberId: string }) =>
      updateOccurrence(choreId, date, memberId),
    onSuccess: invalidate,
  })
}

export function useUnlockOccurrence() {
  const invalidate = usePlannerInvalidation()

  return useMutation({
    mutationFn: (choreId: string) => unlockOccurrence(choreId),
    onSuccess: invalidate,
  })
}

/**
 * Queues a recalculation and waits for the background run to finish, so the caller can report
 * the outcome and refresh the plan. Polling stops on the first non-running status.
 */
export function useRecalculate() {
  const invalidate = usePlannerInvalidation()

  return useMutation({
    mutationFn: async (): Promise<ScheduleRun | null> => {
      const accepted = await startRecalculation()

      for (let attempt = 0; attempt < maxPolls; attempt++) {
        await new Promise((resolve) => setTimeout(resolve, pollIntervalMs))
        const run = await getScheduleRun(accepted.runId)
        if (run.status !== 'Running') {
          return run
        }
      }

      // Still running: the worker will finish on its own, the plan simply refreshes later.
      return null
    },
    onSuccess: invalidate,
  })
}
