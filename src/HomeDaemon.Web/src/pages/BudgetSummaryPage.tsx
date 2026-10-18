import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { budgetSummaryQueryKey, budgetYearsQueryKey, getBudgetSummary, getBudgetYears } from '../api/budget'
import type { BudgetMemberSummary, BudgetSummary } from '../api/types'
import { EmptyState } from '../components/EmptyState'
import { ErrorState } from '../components/ErrorState'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { formatMinor } from '../lib/money'

export function BudgetSummaryPage() {
  const { t, i18n } = useTranslation()

  const years = useQuery({ queryKey: budgetYearsQueryKey, queryFn: ({ signal }) => getBudgetYears(signal) })
  const [selectedYear, setSelectedYear] = useState<number | null>(null)
  const year = selectedYear ?? years.data?.[0] ?? new Date().getFullYear()

  const summary = useQuery({
    queryKey: budgetSummaryQueryKey(year),
    queryFn: ({ signal }) => getBudgetSummary(year, signal),
  })

  const settle = useMemo(() => settlement(summary.data, summary.data?.currency ?? 'EUR', i18n.language), [summary.data, i18n.language])
  const currency = summary.data?.currency ?? 'EUR'

  return (
    <>
      <PageHeader
        title={t('budget.title')}
        subtitle={t('budget.subtitle')}
        actions={
          <>
            <Link className="button button--primary" to="/budget/expenses/new">
              {t('budget.actions.addExpense')}
            </Link>
            <Link className="button" to="/budget/transfers">
              {t('budget.actions.addTransfer')}
            </Link>
          </>
        }
      />

      <div className="filters">
        <div className="filters__group">
          <label className="visually-hidden" htmlFor="budget-year">
            {t('budget.field.year')}
          </label>
          <select
            id="budget-year"
            className="input"
            value={year}
            onChange={(event) => setSelectedYear(Number(event.target.value))}
          >
            {(years.data ?? [year]).map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <div className="filters__group">
          <Link className="button" to={`/budget/incomes?year=${year}`}>
            {t('budget.nav.incomes')}
          </Link>
          <Link className="button" to={`/budget/expenses?year=${year}`}>
            {t('budget.nav.expenses')}
          </Link>
        </div>
      </div>

      {summary.isPending ? <Spinner label={t('app.loading')} /> : null}
      {summary.error ? <ErrorState error={summary.error} onRetry={() => void summary.refetch()} /> : null}

      {summary.data ? (
        <>
          {!summary.data.proportionAvailable ? (
            <p className="notice notice--warning" role="status">
              {t('budget.summary.noIncome')}
            </p>
          ) : null}

          {summary.data.proportionAvailable && !summary.data.incomeComplete ? (
            <p className="notice notice--warning" role="status">
              {t('budget.summary.provisional')}{' '}
              <Link to={`/budget/incomes?year=${year}`}>{t('budget.summary.completeIncome')}</Link>
            </p>
          ) : null}

          <section className="budget-balance" aria-live="polite">
            <p className="budget-balance__headline">
              {settle ? t(settle.key, settle.params) : t('budget.summary.settled')}
            </p>
            <dl className="definition-list">
              <div>
                <dt>{t('budget.summary.totalIncome')}</dt>
                <dd>{formatMinor(summary.data.totalIncomeMinor, currency, i18n.language)}</dd>
              </div>
              <div>
                <dt>{t('budget.summary.totalExpenses')}</dt>
                <dd>{formatMinor(summary.data.totalExpensesMinor, currency, i18n.language)}</dd>
              </div>
            </dl>
          </section>

          <section className="budget-cards">
            {summary.data.members.map((member) => (
              <article className="budget-card" key={member.memberId}>
                <header className="budget-card__header">
                  <h2 className="budget-card__name">{member.name}</h2>
                  <span className="budget-card__share">
                    {member.share === null
                      ? t('budget.summary.shareUnavailable')
                      : t('budget.summary.share', { percent: Math.round(member.share * 100) })}
                  </span>
                </header>

                <dl className="definition-list">
                  <div>
                    <dt>{t('budget.summary.annualIncome')}</dt>
                    <dd>{formatMinor(member.annualIncomeMinor, currency, i18n.language)}</dd>
                  </div>
                  <div>
                    <dt>{t('budget.summary.paid')}</dt>
                    <dd>{formatMinor(member.paidMinor, currency, i18n.language)}</dd>
                  </div>
                  <div>
                    <dt>{t('budget.summary.due')}</dt>
                    <dd>{formatMinor(member.dueMinor, currency, i18n.language) ?? '—'}</dd>
                  </div>
                  <div>
                    <dt>{t('budget.summary.balance')}</dt>
                    <dd className={balanceTone(member)}>
                      {formatMinor(member.balanceMinor, currency, i18n.language) ?? '—'}
                    </dd>
                  </div>
                </dl>

                {member.missingMonths.length > 0 ? (
                  <p className="budget-card__missing">
                    {t('budget.summary.missingMonths', { count: member.missingMonths.length })}
                  </p>
                ) : null}
              </article>
            ))}
          </section>

          <section>
            <h2 className="page-header__title">{t('budget.summary.byCategory')}</h2>
            {summary.data.byCategory.length === 0 ? (
              <EmptyState message={t('budget.summary.emptyCategories')} />
            ) : (
              <ul className="settings-list">
                {summary.data.byCategory.map((total) => (
                  <li className="settings-row" key={total.categoryId}>
                    <div className="settings-row__main">
                      <span className="settings-row__title">{total.name}</span>
                    </div>
                    <span className="budget-amount">
                      {formatMinor(total.amountMinor, currency, i18n.language)}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      ) : null}
    </>
  )
}

interface Settlement {
  key: string
  params: Record<string, string>
}

/**
 * Turns the per-member balances into the one sentence a household actually cares about:
 * who owes whom. Balances always sum to zero, so the largest credit and debit settle it.
 */
function settlement(summary: BudgetSummary | undefined, currency: string, locale: string): Settlement | null {
  const members = (summary?.members ?? []).filter((member) => member.balanceMinor !== null)
  if (members.length === 0) {
    return null
  }

  const creditor = members.reduce((best, member) => (member.balanceMinor! > best.balanceMinor! ? member : best))
  const debtor = members.reduce((worst, member) => (member.balanceMinor! < worst.balanceMinor! ? member : worst))

  if (creditor.balanceMinor! <= 0 || debtor.balanceMinor! >= 0) {
    return null
  }

  return {
    key: 'budget.summary.owes',
    params: {
      debtor: debtor.name,
      creditor: creditor.name,
      amount: formatMinor(creditor.balanceMinor!, currency, locale) ?? '',
    },
  }
}

function balanceTone(member: BudgetMemberSummary): string {
  if (member.balanceMinor === null || member.balanceMinor === 0) {
    return 'budget-amount'
  }

  return member.balanceMinor > 0 ? 'budget-amount budget-amount--positive' : 'budget-amount budget-amount--negative'
}
