import { useId, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { budgetCategoriesQueryKey, getBudgetCategories, getBudgetExpense } from '../api/budget'
import { getHousehold, getMembers, householdQueryKey, membersQueryKey } from '../api/household'
import type { BudgetExpense } from '../api/types'
import { useSession } from '../auth/session-context'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { ErrorState } from '../components/ErrorState'
import { Field } from '../components/Field'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { useCreateBudgetExpense, useDeleteBudgetExpense, useUpdateBudgetExpense } from '../hooks/useBudgetMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { errorMessageKey, fieldProblems } from '../lib/errors'
import { todayInTimeZone } from '../lib/format'
import { minorToInputValue, parseAmountToMinor } from '../lib/money'
import {
  amountValidationKey,
  issuesToFieldErrors,
  budgetExpenseFormSchema,
  type BudgetExpenseFormValues,
} from '../lib/validation'

const expenseQueryKey = (expenseId: string) => ['budget', 'expense', expenseId] as const

export function BudgetExpenseFormPage() {
  const { t } = useTranslation()
  const { expenseId } = useParams()

  const expense = useQuery({
    queryKey: expenseQueryKey(expenseId ?? 'new'),
    queryFn: ({ signal }) => getBudgetExpense(expenseId!, signal),
    enabled: Boolean(expenseId),
  })

  return (
    <>
      <PageHeader title={expenseId ? t('budget.expenseForm.editTitle') : t('budget.expenseForm.newTitle')} />

      {expenseId && expense.isPending ? <Spinner label={t('app.loading')} /> : null}
      {expense.error ? <ErrorState error={expense.error} onRetry={() => void expense.refetch()} /> : null}

      {!expenseId || expense.data ? <ExpenseForm key={expenseId ?? 'new'} expense={expense.data ?? null} /> : null}
    </>
  )
}

function ExpenseForm({ expense }: { expense: BudgetExpense | null }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const isOnline = useOnlineStatus()
  const { user } = useSession()
  const create = useCreateBudgetExpense()
  const update = useUpdateBudgetExpense()
  const remove = useDeleteBudgetExpense()
  const amountRef = useRef<HTMLInputElement>(null)

  const household = useQuery({ queryKey: householdQueryKey, queryFn: ({ signal }) => getHousehold(signal) })
  const categories = useQuery({ queryKey: budgetCategoriesQueryKey, queryFn: ({ signal }) => getBudgetCategories(signal) })
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })

  const [values, setValues] = useState<BudgetExpenseFormValues>(() => ({
    date: expense?.date ?? todayInTimeZone(household.data?.timeZoneId ?? 'UTC'),
    amount: minorToInputValue(expense?.amountMinor ?? null),
    categoryId: expense?.categoryId ?? '',
    subcategoryId: expense?.subcategoryId ?? '',
    paidByMemberId: expense?.paidByMemberId ?? '',
    description: expense?.description ?? '',
  }))
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [errorKey, setErrorKey] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const participants = useMemo(
    () => (members.data ?? []).filter((member) => member.isBudgetParticipant),
    [members.data],
  )

  // With no common account the person entering the expense is almost always the payer, so the
  // signed-in member is preselected and the most common tap disappears.
  const signedInMemberId = user?.householdMemberId ?? null
  const defaultPayerId = useMemo(() => {
    if (expense) {
      return expense.paidByMemberId
    }

    if (signedInMemberId && participants.some((member) => member.id === signedInMemberId)) {
      return signedInMemberId
    }

    return participants.length === 1 ? (participants[0]?.id ?? '') : ''
  }, [expense, participants, signedInMemberId])

  const payerId = values.paidByMemberId === '' ? defaultPayerId : values.paidByMemberId
  const selectedCategory = (categories.data ?? []).find((category) => category.id === values.categoryId)
  const busy = create.isPending || update.isPending

  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)

  const setField = <K extends keyof BudgetExpenseFormValues>(key: K, value: BudgetExpenseFormValues[K]) => {
    setSaved(false)
    setValues((current) => ({ ...current, [key]: value }))
  }

  const clearAmountError = () =>
    setFieldErrors((current) => {
      if (!current.amount) {
        return current
      }

      const next = { ...current }
      delete next.amount
      return next
    })

  // Show the problem while the field is being filled in, not only after Save.
  const validateAmount = () => {
    const key = amountValidationKey(values.amount)
    setFieldErrors((current) => {
      const next = { ...current }
      if (key) {
        next.amount = key
      } else {
        delete next.amount
      }
      return next
    })
  }

  const submit = async (addAnother: boolean) => {
    setErrorKey(null)
    setSaved(false)

    const parsed = budgetExpenseFormSchema.safeParse({ ...values, paidByMemberId: payerId })
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    const amountMinor = parseAmountToMinor(parsed.data.amount)
    if (amountMinor === null) {
      setFieldErrors({ amount: 'budget.validation.amountInvalid' })
      return
    }

    setFieldErrors({})
    const model = {
      date: parsed.data.date,
      amountMinor,
      categoryId: parsed.data.categoryId,
      subcategoryId: parsed.data.subcategoryId === '' ? null : parsed.data.subcategoryId,
      paidByMemberId: parsed.data.paidByMemberId,
      description: parsed.data.description === '' ? null : parsed.data.description,
    }

    try {
      if (expense) {
        await update.mutateAsync({ expenseId: expense.id, model })
        await navigate('/budget/expenses')
        return
      }

      await create.mutateAsync(model)

      if (!addAnother) {
        await navigate('/budget/expenses')
        return
      }

      // Keep date, category and payer: only the amount changes between two expenses.
      setValues((current) => ({ ...current, amount: '', description: '' }))
      setSaved(true)
      amountRef.current?.focus()
    } catch (error) {
      const problems = fieldProblems(error)
      if (Object.keys(problems).length > 0) {
        setFieldErrors(Object.fromEntries(Object.entries(problems).map(([field, messages]) => [field, messages[0] ?? 'errors.generic'])))
      } else {
        setErrorKey(errorMessageKey(error))
      }
    }
  }

  const handleDelete = async () => {
    if (!expense) {
      return
    }

    setErrorKey(null)
    try {
      await remove.mutateAsync(expense.id)
      await navigate('/budget/expenses')
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    } finally {
      setConfirmDelete(false)
    }
  }

  return (
    <>
      {errorKey ? (
        <p className="notice notice--error" role="alert">
          {t(errorKey)}
        </p>
      ) : null}
      {saved ? (
        <p className="notice" role="status">
          {t('budget.expenseForm.saved')}
        </p>
      ) : null}

      {categories.data && categories.data.length === 0 ? (
        <p className="notice notice--warning">
          {t('budget.expenseForm.noCategories')}{' '}
          <Link to="/settings/budget/categories">{t('budget.expenseForm.configureCategories')}</Link>
        </p>
      ) : null}

      {participants.length === 0 ? (
        <p className="notice notice--warning">
          {t('budget.expenseForm.noParticipants')}{' '}
          <Link to="/settings/members">{t('budget.expenseForm.configurePeople')}</Link>
        </p>
      ) : null}

      <form
        className="form"
        onSubmit={(event) => {
          event.preventDefault()
          void submit(false)
        }}
      >
        <Field label={t('budget.field.amount')} htmlFor="expense-amount" error={errorFor('amount')}>
          <input
            id="expense-amount"
            ref={amountRef}
            className="input input--amount"
            type="text"
            inputMode="decimal"
            autoComplete="off"
            placeholder="0.00"
            autoFocus={!expense}
            value={values.amount}
            onChange={(event) => {
              setField('amount', event.target.value)
              clearAmountError()
            }}
            onBlur={validateAmount}
          />
        </Field>

        <Field label={t('budget.field.date')} htmlFor="expense-date" error={errorFor('date')}>
          <input
            id="expense-date"
            className="input"
            type="date"
            value={values.date}
            onChange={(event) => setField('date', event.target.value)}
          />
        </Field>

        <ChipGroup label={t('budget.field.category')} error={errorFor('categoryId')}>
          {(categories.data ?? []).map((category) => (
            <Chip
              key={category.id}
              selected={values.categoryId === category.id}
              disabled={!isOnline}
              onClick={() => {
                setSaved(false)
                setValues((current) => ({ ...current, categoryId: category.id, subcategoryId: '' }))
              }}
            >
              {category.name}
            </Chip>
          ))}
        </ChipGroup>

        {selectedCategory && selectedCategory.subcategories.length > 0 ? (
          <ChipGroup label={t('budget.field.subcategory')} error={errorFor('subcategoryId')}>
            <Chip
              selected={values.subcategoryId === ''}
              disabled={!isOnline}
              onClick={() => setField('subcategoryId', '')}
            >
              {t('budget.field.noSubcategory')}
            </Chip>
            {selectedCategory.subcategories.map((subcategory) => (
              <Chip
                key={subcategory.id}
                selected={values.subcategoryId === subcategory.id}
                disabled={!isOnline}
                onClick={() => setField('subcategoryId', subcategory.id)}
              >
                {subcategory.name}
              </Chip>
            ))}
          </ChipGroup>
        ) : null}

        <ChipGroup label={t('budget.field.paidBy')} error={errorFor('paidByMemberId')}>
          {participants.map((member) => (
            <Chip
              key={member.id}
              selected={payerId === member.id}
              disabled={!isOnline}
              onClick={() => setField('paidByMemberId', member.id)}
            >
              {member.name}
            </Chip>
          ))}
        </ChipGroup>

        <Field label={t('budget.field.description')} htmlFor="expense-description" error={errorFor('description')}>
          <input
            id="expense-description"
            className="input"
            type="text"
            maxLength={500}
            value={values.description}
            onChange={(event) => setField('description', event.target.value)}
          />
        </Field>

        {!isOnline ? <p className="notice notice--warning">{t('app.offline')}</p> : null}

        {/* Stays in view while the form scrolls, which is the common case on a phone. */}
        <div className="form-actions">
          <Link className="button button--ghost" to="/budget/expenses">
            {t('common.cancel')}
          </Link>
          {expense ? (
            <>
              <button type="button" className="button button--danger" onClick={() => setConfirmDelete(true)} disabled={!isOnline}>
                {t('common.delete')}
              </button>
              <button type="submit" className="button button--primary" disabled={busy || !isOnline}>
                {busy ? t('common.saving') : t('common.save')}
              </button>
            </>
          ) : (
            <>
              <button type="button" className="button" onClick={() => void submit(true)} disabled={busy || !isOnline}>
                {t('budget.expenseForm.saveAndAdd')}
              </button>
              <button type="submit" className="button button--primary" disabled={busy || !isOnline}>
                {busy ? t('common.saving') : t('common.save')}
              </button>
            </>
          )}
        </div>
      </form>

      <ConfirmDialog
        open={confirmDelete}
        title={t('budget.expenses.cancelTitle')}
        body={<p>{t('budget.expenses.cancelBody')}</p>}
        confirmLabel={t('budget.actions.cancel')}
        busy={remove.isPending}
        onConfirm={() => {
          void handleDelete()
        }}
        onCancel={() => setConfirmDelete(false)}
      />
    </>
  )
}

/** A labelled group of toggle chips: faster than a select on a phone. */
function ChipGroup({ label, error, children }: { label: string; error?: string; children: ReactNode }) {
  const labelId = useId()

  return (
    <div className="field">
      <span className="field__label" id={labelId}>
        {label}
      </span>
      <div className="chips" role="group" aria-labelledby={labelId}>
        {children}
      </div>
      {error ? (
        <p className="field__error" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  )
}

function Chip({
  selected,
  disabled,
  onClick,
  children,
}: {
  selected: boolean
  disabled: boolean
  onClick: () => void
  children: ReactNode
}) {
  return (
    <button
      type="button"
      className={selected ? 'chip is-selected' : 'chip'}
      aria-pressed={selected}
      disabled={disabled}
      onClick={onClick}
    >
      {children}
    </button>
  )
}
