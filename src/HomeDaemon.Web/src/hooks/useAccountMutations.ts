import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  accountsQueryKey,
  createAccount,
  deactivateAccount,
  reactivateAccount,
  resetAccountPassword,
  updateAccount,
  type AccountWriteModel,
} from '../api/accounts'

export function useCreateAccount() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (model: AccountWriteModel) => createAccount(model),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: accountsQueryKey })
    },
  })
}

export function useUpdateAccount() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ userId, model }: { userId: string; model: AccountWriteModel }) => updateAccount(userId, model),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: accountsQueryKey })
    },
  })
}

export function useSetAccountActive() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ userId, isActive }: { userId: string; isActive: boolean }) =>
      isActive ? reactivateAccount(userId) : deactivateAccount(userId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: accountsQueryKey })
    },
  })
}

export function useResetAccountPassword() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (userId: string) => resetAccountPassword(userId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: accountsQueryKey })
    },
  })
}
