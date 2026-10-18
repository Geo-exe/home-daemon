import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { budgetIncomesQueryKey, budgetYearsQueryKey, getBudgetIncomes, getBudgetYears } from '../api/budget'
import type { BudgetIncomeMonth } from '../api/types'
import { ErrorState } from '../components/ErrorState'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { useDeleteBudgetIncome, useUpsertBudgetIncome } from '../hooks/useBudgetMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { minorToInputValue, parseAmountToMinor } from '../lib/money'

export function BudgetIncomesPage() {
  const { t } = useTranslation()
  const [searchParams] = useSearchParams()

  const requestedYear = Number(searchParams.get('year'))
  const [selectedYear, setSelectedYear] = useState<number | null>(
    Number.isFinite(requestedYear) && requestedYear > 0 ? requestedYear : null,
  )

  const years = useQuery({ queryKey: budgetYearsQueryKey, queryFn: ({ signal }) => getBudgetYears(signal) })
  const year = selectedYear ?? years.data?.[0] ?? new Date().getFullYear()

  const incomes = useQuery({
    queryKey: budgetIncomesQueryKey(year),
    queryFn: ({ signal }) => getBudgetIncomes(year, signal),
  })

  const yearOptions = useMemo(() => {
    const options = new Set(years.data ?? [])
    options.add(new Date().getFullYear())
    options.add(year)
    return [...options].sort((left, right) => right - left)
  }, [years.data, year])

  return (
    <>
      <PageHeader title={t('budget.incomes.title')} subtitle={t('budget.incomes.subtitle')} />

      <p className="notice notice--warning" role="note">
        {t('budget.incomes.retroactive')}
      </p>

      <div className="filters">
        <div className="filters__group">
          <label className="visually-hidden" htmlFor="income-year">
            {t('budget.field.year')}
          </label>
          <select
            id="income-year"
            className="input"
            value={year}
            onChange={(event) => setSelectedYear(Number(event.target.value))}
          >
            {yearOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <div className="filters__group">
          <Link className="button" to={`/budget?year=${year}`}>
            {t('budget.nav.summary')}
          </Link>
        </div>
      </div>

      {incomes.isPending ? <Spinner label={t('app.loading')} /> : null}
      {incomes.error ? <ErrorState error={incomes.error} onRetry={() => void incomes.refetch()} /> : null}

      {incomes.data && incomes.data.members.length === 0 ? (
        <p className="notice notice--warning">
          {t('budget.incomes.noParticipants')} <Link to="/settings/members">{t('budget.expenseForm.configurePeople')}</Link>
        </p>
      ) : null}

      {incomes.data?.members.map((member) => (
        <section className="budget-income" key={member.memberId}>
          <h2 className="budget-card__name">{member.name}</h2>
          <ul className="budget-income__months">
            {member.months.map((cell) => (
              <IncomeCell key={cell.month} year={year} memberId={member.memberId} cell={cell} />
            ))}
          </ul>
        </section>
      ))}
    </>
  )
}

function IncomeCell({
  year,
  memberId,
  cell,
}: {
  year: number
  memberId: string
  cell: BudgetIncomeMonth
}) {
  const { t, i18n } = useTranslation()
  const isOnline = useOnlineStatus()
  const upsert = useUpsertBudgetIncome()
  const removeIncome = useDeleteBudgetIncome()

  const [value, setValue] = useState(minorToInputValue(cell.amountMinor))
  const [invalid, setInvalid] = useState(false)

  const label = new Intl.DateTimeFormat(i18n.language, { month: 'long' }).format(new Date(2026, cell.month - 1, 1))
  const inputId = `income-${memberId}-${cell.month}`

  const commit = async () => {
    const trimmed = value.trim()

    if (trimmed === '') {
      setInvalid(false)
      if (cell.incomeId) {
        await removeIncome.mutateAsync({ incomeId: cell.incomeId, year })
      }
      return
    }

    const minor = parseAmountToMinor(trimmed)
    if (minor === null) {
      setInvalid(true)
      return
    }

    setInvalid(false)
    if (minor === cell.amountMinor) {
      return
    }

    await upsert.mutateAsync({ memberId, year, month: cell.month, amountMinor: minor })
  }

  const busy = upsert.isPending || removeIncome.isPending

  return (
    <li className={cell.amountMinor === null ? 'budget-income__month is-missing' : 'budget-income__month'}>
      <label className="budget-income__label" htmlFor={inputId}>
        {label}
      </label>
      <input
        id={inputId}
        className="input"
        type="text"
        inputMode="decimal"
        placeholder="0.00"
        value={value}
        disabled={!isOnline || busy}
        aria-invalid={invalid}
        aria-label={t('budget.incomes.amountFor', { month: label })}
        onChange={(event) => setValue(event.target.value)}
        onBlur={() => {
          void commit()
        }}
      />
      {invalid ? (
        <span className="field__error" role="alert">
          {t('budget.validation.amountInvalid')}
        </span>
      ) : null}
    </li>
  )
}
