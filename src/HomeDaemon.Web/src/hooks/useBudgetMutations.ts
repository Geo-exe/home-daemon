import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  budgetIncomesQueryKey,
  budgetSummaryQueryKey,
  createBudgetCategory,
  createBudgetExpense,
  createBudgetSubcategory,
  createBudgetTransfer,
  deleteBudgetCategory,
  deleteBudgetExpense,
  deleteBudgetIncome,
  deleteBudgetSubcategory,
  deleteBudgetTransfer,
  updateBudgetCategory,
  updateBudgetExpense,
  updateBudgetSubcategory,
  updateBudgetTransfer,
  upsertBudgetIncome,
  type BudgetCategoryWriteModel,
  type BudgetExpenseWriteModel,
  type BudgetSubcategoryWriteModel,
  type BudgetTransferWriteModel,
} from '../api/budget'

/**
 * The income of a year re-splits the whole year, so every income change invalidates the
 * summary and the year lists, not just the grid the user is editing.
 */
function useInvalidateBudget() {
  const queryClient = useQueryClient()

  return async (year?: number) => {
    await queryClient.invalidateQueries({ queryKey: ['budget'] })

    if (year !== undefined) {
      await queryClient.invalidateQueries({ queryKey: budgetSummaryQueryKey(year) })
      await queryClient.invalidateQueries({ queryKey: budgetIncomesQueryKey(year) })
    }
  }
}

export function useUpsertBudgetIncome() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: ({ memberId, year, month, amountMinor }: { memberId: string; year: number; month: number; amountMinor: number }) =>
      upsertBudgetIncome(memberId, year, month, amountMinor),
    onSuccess: (_data, variables) => invalidate(variables.year),
  })
}

export function useDeleteBudgetIncome() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: ({ incomeId }: { incomeId: string; year: number }) => deleteBudgetIncome(incomeId),
    onSuccess: (_data, variables) => invalidate(variables.year),
  })
}

export function useCreateBudgetExpense() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: (model: BudgetExpenseWriteModel) => createBudgetExpense(model),
    onSuccess: () => invalidate(),
  })
}

export function useUpdateBudgetExpense() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: ({ expenseId, model }: { expenseId: string; model: BudgetExpenseWriteModel }) =>
      updateBudgetExpense(expenseId, model),
    onSuccess: () => invalidate(),
  })
}

export function useDeleteBudgetExpense() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: (expenseId: string) => deleteBudgetExpense(expenseId),
    onSuccess: () => invalidate(),
  })
}

export function useCreateBudgetTransfer() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: (model: BudgetTransferWriteModel) => createBudgetTransfer(model),
    onSuccess: () => invalidate(),
  })
}

export function useUpdateBudgetTransfer() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: ({ transferId, model }: { transferId: string; model: BudgetTransferWriteModel }) =>
      updateBudgetTransfer(transferId, model),
    onSuccess: () => invalidate(),
  })
}

export function useDeleteBudgetTransfer() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: (transferId: string) => deleteBudgetTransfer(transferId),
    onSuccess: () => invalidate(),
  })
}

export function useCreateBudgetCategory() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: (model: BudgetCategoryWriteModel) => createBudgetCategory(model),
    onSuccess: () => invalidate(),
  })
}

export function useUpdateBudgetCategory() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: ({ categoryId, model }: { categoryId: string; model: BudgetCategoryWriteModel }) =>
      updateBudgetCategory(categoryId, model),
    onSuccess: () => invalidate(),
  })
}

export function useDeleteBudgetCategory() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: (categoryId: string) => deleteBudgetCategory(categoryId),
    onSuccess: () => invalidate(),
  })
}

export function useCreateBudgetSubcategory() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: ({ categoryId, model }: { categoryId: string; model: BudgetSubcategoryWriteModel }) =>
      createBudgetSubcategory(categoryId, model),
    onSuccess: () => invalidate(),
  })
}

export function useUpdateBudgetSubcategory() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: ({ subcategoryId, model }: { subcategoryId: string; model: BudgetSubcategoryWriteModel }) =>
      updateBudgetSubcategory(subcategoryId, model),
    onSuccess: () => invalidate(),
  })
}

export function useDeleteBudgetSubcategory() {
  const invalidate = useInvalidateBudget()

  return useMutation({
    mutationFn: (subcategoryId: string) => deleteBudgetSubcategory(subcategoryId),
    onSuccess: () => invalidate(),
  })
}
