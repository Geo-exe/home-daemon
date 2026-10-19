import { apiDelete, apiGet, apiPost, apiPut } from './client'
import type {
  BudgetCategory,
  BudgetExpense,
  BudgetIncomes,
  BudgetSummary,
  BudgetSubcategory,
  BudgetTransfer,
  BudgetTransferSuggestion,
  CalendarDate,
  CursorPage,
} from './types'

export const budgetYearsQueryKey = ['budget', 'years'] as const
export const budgetCategoriesQueryKey = ['budget', 'categories'] as const

export const budgetSummaryQueryKey = (year: number) => ['budget', 'summary', year] as const
export const budgetIncomesQueryKey = (year: number) => ['budget', 'incomes', year] as const
export const budgetExpensesQueryKey = (filters: BudgetExpenseFilters) => ['budget', 'expenses', filters] as const
export const budgetTransfersQueryKey = (settlementYear: number | null) => ['budget', 'transfers', settlementYear] as const
export const budgetTransferSuggestionsQueryKey = ['budget', 'transfers', 'suggestions'] as const

export interface BudgetExpenseFilters {
  year?: number
  categoryId?: string
  memberId?: string
}

/**
 * Write models use integer minor units, so a parsed form value can be sent without any
 * floating-point rounding on the way to the API.
 */
export interface BudgetExpenseWriteModel {
  date: CalendarDate
  amountMinor: number
  categoryId: string
  subcategoryId: string | null
  paidByMemberId: string
  description: string | null
}

export interface BudgetTransferWriteModel {
  date: CalendarDate
  settlementYear: number
  amountMinor: number
  fromMemberId: string
  toMemberId: string
  description: string | null
}

export interface BudgetCategoryWriteModel {
  name: string
  sortOrder: number
  isArchived: boolean
}

export interface BudgetSubcategoryWriteModel {
  name: string
  sortOrder: number
  isArchived: boolean
}

export interface BudgetImportRow {
  line: number
  date: CalendarDate
  amountMinor: number
  category: string
  subcategory: string | null
  paidByMemberId: string
  description: string | null
}

export interface BudgetImportCategoryMapping {
  sourceName: string
  categoryId: string | null
  create: boolean
}

export interface BudgetImportSubcategoryMapping {
  categorySourceName: string
  sourceName: string
  subcategoryId: string | null
  create: boolean
}

export interface BudgetImportResult {
  imported: number
  skipped: number
}

export function getBudgetYears(signal?: AbortSignal): Promise<number[]> {
  return apiGet<number[]>('/budget/years', signal)
}

export function getBudgetSummary(year: number, signal?: AbortSignal): Promise<BudgetSummary> {
  return apiGet<BudgetSummary>(`/budget/summary?year=${year}`, signal)
}

export function getBudgetIncomes(year: number, signal?: AbortSignal): Promise<BudgetIncomes> {
  return apiGet<BudgetIncomes>(`/budget/incomes?year=${year}`, signal)
}

/** Upserts the single income row for a person, year and month. */
export function upsertBudgetIncome(memberId: string, year: number, month: number, amountMinor: number): Promise<unknown> {
  return apiPut('/budget/incomes', { memberId, year, month, amountMinor })
}

/** Clears a month, returning it to the "not entered" state. */
export function deleteBudgetIncome(incomeId: string): Promise<undefined> {
  return apiDelete(`/budget/incomes/${incomeId}`)
}

export function getBudgetCategories(signal?: AbortSignal): Promise<BudgetCategory[]> {
  return apiGet<BudgetCategory[]>('/budget/categories', signal)
}

export function createBudgetCategory(model: BudgetCategoryWriteModel): Promise<BudgetCategory> {
  return apiPost<BudgetCategory>('/budget/categories', model)
}

export function updateBudgetCategory(categoryId: string, model: BudgetCategoryWriteModel): Promise<BudgetCategory> {
  return apiPut<BudgetCategory>(`/budget/categories/${categoryId}`, model)
}

export function deleteBudgetCategory(categoryId: string): Promise<undefined> {
  return apiDelete(`/budget/categories/${categoryId}`)
}

export function createBudgetSubcategory(
  categoryId: string,
  model: BudgetSubcategoryWriteModel,
): Promise<BudgetSubcategory> {
  return apiPost<BudgetSubcategory>(`/budget/categories/${categoryId}/subcategories`, model)
}

export function updateBudgetSubcategory(
  subcategoryId: string,
  model: BudgetSubcategoryWriteModel,
): Promise<BudgetSubcategory> {
  return apiPut<BudgetSubcategory>(`/budget/subcategories/${subcategoryId}`, model)
}

export function deleteBudgetSubcategory(subcategoryId: string): Promise<undefined> {
  return apiDelete(`/budget/subcategories/${subcategoryId}`)
}

export function listBudgetExpenses(
  filters: BudgetExpenseFilters,
  cursor: string | null,
  pageSize: number,
  signal?: AbortSignal,
): Promise<CursorPage<BudgetExpense>> {
  const search = new URLSearchParams({ pageSize: String(pageSize) })

  if (filters.year !== undefined) {
    search.set('year', String(filters.year))
  }
  if (filters.categoryId) {
    search.set('categoryId', filters.categoryId)
  }
  if (filters.memberId) {
    search.set('memberId', filters.memberId)
  }
  if (cursor) {
    search.set('cursor', cursor)
  }

  return apiGet<CursorPage<BudgetExpense>>(`/budget/expenses?${search.toString()}`, signal)
}

export function getBudgetExpense(expenseId: string, signal?: AbortSignal): Promise<BudgetExpense> {
  return apiGet<BudgetExpense>(`/budget/expenses/${expenseId}`, signal)
}

export function createBudgetExpense(model: BudgetExpenseWriteModel): Promise<BudgetExpense> {
  return apiPost<BudgetExpense>('/budget/expenses', model)
}

export function updateBudgetExpense(expenseId: string, model: BudgetExpenseWriteModel): Promise<BudgetExpense> {
  return apiPut<BudgetExpense>(`/budget/expenses/${expenseId}`, model)
}

/** Cancels an expense. Nothing is soft-deleted: the balance is recomputed without it. */
export function deleteBudgetExpense(expenseId: string): Promise<undefined> {
  return apiDelete(`/budget/expenses/${expenseId}`)
}

/**
 * Imports many expenses in one request. The API is administrator-only and validates the whole
 * batch before writing anything.
 */
export function importBudgetExpenses(model: {
  rows: BudgetImportRow[]
  categories: BudgetImportCategoryMapping[]
  subcategories: BudgetImportSubcategoryMapping[]
}): Promise<BudgetImportResult> {
  return apiPost<BudgetImportResult>('/budget/imports', model)
}

/** Recent distinct transfer directions, used to prefill a new transfer. */
export function getBudgetTransferSuggestions(limit = 2, signal?: AbortSignal): Promise<BudgetTransferSuggestion[]> {
  return apiGet<BudgetTransferSuggestion[]>(`/budget/transfers/suggestions?limit=${limit}`, signal)
}

export function listBudgetTransfers(
  settlementYear: number | null,
  cursor: string | null,
  pageSize: number,
  signal?: AbortSignal,
): Promise<CursorPage<BudgetTransfer>> {
  const search = new URLSearchParams({ pageSize: String(pageSize) })

  if (settlementYear !== null) {
    search.set('settlementYear', String(settlementYear))
  }
  if (cursor) {
    search.set('cursor', cursor)
  }

  return apiGet<CursorPage<BudgetTransfer>>(`/budget/transfers?${search.toString()}`, signal)
}

export function createBudgetTransfer(model: BudgetTransferWriteModel): Promise<BudgetTransfer> {
  return apiPost<BudgetTransfer>('/budget/transfers', model)
}

export function updateBudgetTransfer(transferId: string, model: BudgetTransferWriteModel): Promise<BudgetTransfer> {
  return apiPut<BudgetTransfer>(`/budget/transfers/${transferId}`, model)
}

export function deleteBudgetTransfer(transferId: string): Promise<undefined> {
  return apiDelete(`/budget/transfers/${transferId}`)
}
