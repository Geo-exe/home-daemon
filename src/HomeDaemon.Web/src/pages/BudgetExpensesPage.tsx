import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  budgetCategoriesQueryKey,
  budgetExpensesQueryKey,
  budgetYearsQueryKey,
  getBudgetCategories,
  getBudgetYears,
  listBudgetExpenses,
  type BudgetExpenseFilters,
} from '../api/budget'
import { getHousehold, getMembers, householdQueryKey, membersQueryKey } from '../api/household'
import type { BudgetExpense } from '../api/types'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { EmptyState } from '../components/EmptyState'
import { ErrorState } from '../components/ErrorState'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { useDeleteBudgetExpense } from '../hooks/useBudgetMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { errorMessageKey } from '../lib/errors'
import { formatCalendarDate } from '../lib/format'
import { formatMinor } from '../lib/money'

const pageSize = 50

export function BudgetExpensesPage() {
  const { t, i18n } = useTranslation()
  const isOnline = useOnlineStatus()
  const [searchParams] = useSearchParams()

  const initialYear = Number(searchParams.get('year'))
  const [filters, setFilters] = useState<BudgetExpenseFilters>(
    Number.isFinite(initialYear) && initialYear > 0 ? { year: initialYear } : {},
  )
  const [pendingDelete, setPendingDelete] = useState<BudgetExpense | null>(null)
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const household = useQuery({ queryKey: householdQueryKey, queryFn: ({ signal }) => getHousehold(signal) })
  const categories = useQuery({ queryKey: budgetCategoriesQueryKey, queryFn: ({ signal }) => getBudgetCategories(signal) })
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })
  const years = useQuery({ queryKey: budgetYearsQueryKey, queryFn: ({ signal }) => getBudgetYears(signal) })
  const remove = useDeleteBudgetExpense()

  const expenses = useInfiniteQuery({
    queryKey: budgetExpensesQueryKey(filters),
    queryFn: ({ pageParam, signal }) => listBudgetExpenses(filters, pageParam, pageSize, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
  })

  const participants = useMemo(
    () => (members.data ?? []).filter((member) => member.isBudgetParticipant),
    [members.data],
  )
  const items = useMemo(() => expenses.data?.pages.flatMap((page) => page.items) ?? [], [expenses.data])
  const currency = household.data?.currency ?? 'EUR'

  const handleDelete = async () => {
    if (pendingDelete === null) {
      return
    }

    setErrorKey(null)
    try {
      await remove.mutateAsync(pendingDelete.id)
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    } finally {
      setPendingDelete(null)
    }
  }

  const yearOptions = useMemo(() => {
    const options = new Set(years.data ?? [])
    options.add(new Date().getFullYear())
    if (filters.year !== undefined) {
      options.add(filters.year)
    }

    return [...options].sort((left, right) => right - left)
  }, [years.data, filters.year])

  return (
    <>
      <PageHeader
        title={t('budget.expenses.title')}
        subtitle={t('budget.expenses.subtitle')}
        actions={
          <Link className="button button--primary" to="/budget/expenses/new">
            {t('budget.actions.addExpense')}
          </Link>
        }
      />

      {errorKey ? (
        <p className="notice notice--error" role="alert">
          {t(errorKey)}
        </p>
      ) : null}

      <div className="filters filters--wrap">
        <div className="filters__group">
          <label className="visually-hidden" htmlFor="expense-year">
            {t('budget.field.year')}
          </label>
          <select
            id="expense-year"
            className="input"
            value={filters.year ?? ''}
            onChange={(event) =>
              setFilters((current) => ({ ...current, year: event.target.value === '' ? undefined : Number(event.target.value) }))
            }
          >
            <option value="">{t('budget.filters.allYears')}</option>
            {yearOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>

          <label className="visually-hidden" htmlFor="expense-category">
            {t('budget.field.category')}
          </label>
          <select
            id="expense-category"
            className="input"
            value={filters.categoryId ?? ''}
            onChange={(event) => setFilters((current) => ({ ...current, categoryId: event.target.value || undefined }))}
          >
            <option value="">{t('budget.filters.allCategories')}</option>
            {(categories.data ?? []).map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>

          <label className="visually-hidden" htmlFor="expense-member">
            {t('budget.field.paidBy')}
          </label>
          <select
            id="expense-member"
            className="input"
            value={filters.memberId ?? ''}
            onChange={(event) => setFilters((current) => ({ ...current, memberId: event.target.value || undefined }))}
          >
            <option value="">{t('budget.filters.allPeople')}</option>
            {participants.map((member) => (
              <option key={member.id} value={member.id}>
                {member.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {expenses.isPending ? <Spinner label={t('app.loading')} /> : null}
      {expenses.error ? <ErrorState error={expenses.error} onRetry={() => void expenses.refetch()} /> : null}

      {!expenses.isPending && items.length === 0 ? (
        <EmptyState
          message={t('budget.expenses.empty')}
          action={
            <Link className="button button--primary" to="/budget/expenses/new">
              {t('budget.actions.addExpense')}
            </Link>
          }
        />
      ) : null}

      {items.length > 0 ? (
        <ul className="settings-list">
          {items.map((expense) => (
            <li className="settings-row" key={expense.id}>
              <div className="settings-row__main">
                <Link className="settings-row__title" to={`/budget/expenses/${expense.id}`}>
                  {expense.categoryName}
                  {expense.subcategoryName ? ` · ${expense.subcategoryName}` : ''}
                </Link>
                <span className="settings-row__meta">
                  {formatCalendarDate(expense.date, i18n.language)} · {t('budget.expenses.paidBy')} {expense.paidByMemberName}
                  {expense.description ? ` · ${expense.description}` : ''}
                </span>
              </div>
              <div className="settings-row__actions">
                <span className="budget-amount">{formatMinor(expense.amountMinor, currency, i18n.language)}</span>
                <button
                  type="button"
                  className="button button--danger"
                  onClick={() => setPendingDelete(expense)}
                  disabled={!isOnline}
                >
                  {t('budget.actions.cancel')}
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}

      {expenses.hasNextPage ? (
        <button
          type="button"
          className="button"
          onClick={() => void expenses.fetchNextPage()}
          disabled={expenses.isFetchingNextPage}
        >
          {expenses.isFetchingNextPage ? t('app.loading') : t('common.loadMore')}
        </button>
      ) : null}

      <ConfirmDialog
        open={pendingDelete !== null}
        title={t('budget.expenses.cancelTitle')}
        body={<p>{t('budget.expenses.cancelBody')}</p>}
        confirmLabel={t('budget.actions.cancel')}
        busy={remove.isPending}
        onConfirm={() => {
          void handleDelete()
        }}
        onCancel={() => setPendingDelete(null)}
      />
    </>
  )
}
