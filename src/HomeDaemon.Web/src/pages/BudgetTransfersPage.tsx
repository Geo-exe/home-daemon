import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  budgetTransferSuggestionsQueryKey,
  budgetTransfersQueryKey,
  budgetYearsQueryKey,
  getBudgetTransferSuggestions,
  getBudgetYears,
  listBudgetTransfers,
  type BudgetTransferWriteModel,
} from '../api/budget'
import { getHousehold, getMembers, householdQueryKey, membersQueryKey } from '../api/household'
import type { BudgetTransfer, BudgetTransferSuggestion } from '../api/types'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { EmptyState } from '../components/EmptyState'
import { ErrorState } from '../components/ErrorState'
import { Field } from '../components/Field'
import { FormDialog } from '../components/FormDialog'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { useCreateBudgetTransfer, useDeleteBudgetTransfer, useUpdateBudgetTransfer } from '../hooks/useBudgetMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { errorMessageKey, fieldProblems } from '../lib/errors'
import { formatCalendarDate, todayInTimeZone } from '../lib/format'
import { formatMinor, minorToInputValue, parseAmountToMinor } from '../lib/money'
import { buildTransferShortcuts } from '../lib/transfers'
import {
  amountValidationKey,
  budgetTransferFormSchema,
  issuesToFieldErrors,
  type BudgetTransferFormValues,
} from '../lib/validation'

const pageSize = 50

export function BudgetTransfersPage() {
  const { t, i18n } = useTranslation()
  const isOnline = useOnlineStatus()
  const [searchParams] = useSearchParams()

  const requestedYear = Number(searchParams.get('year'))
  const [settlementYear, setSettlementYear] = useState<number | null>(
    Number.isFinite(requestedYear) && requestedYear > 0 ? requestedYear : null,
  )
  const [editing, setEditing] = useState<{ open: boolean; transfer: BudgetTransfer | null }>({ open: false, transfer: null })
  const [pendingDelete, setPendingDelete] = useState<BudgetTransfer | null>(null)
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const household = useQuery({ queryKey: householdQueryKey, queryFn: ({ signal }) => getHousehold(signal) })
  const years = useQuery({ queryKey: budgetYearsQueryKey, queryFn: ({ signal }) => getBudgetYears(signal) })
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })
  const suggestions = useQuery({
    queryKey: budgetTransferSuggestionsQueryKey,
    queryFn: ({ signal }) => getBudgetTransferSuggestions(2, signal),
    staleTime: 60_000,
  })
  const remove = useDeleteBudgetTransfer()

  const transfers = useInfiniteQuery({
    queryKey: budgetTransfersQueryKey(settlementYear),
    queryFn: ({ pageParam, signal }) => listBudgetTransfers(settlementYear, pageParam, pageSize, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
  })

  const items = useMemo(() => transfers.data?.pages.flatMap((page) => page.items) ?? [], [transfers.data])
  const currency = household.data?.currency ?? 'EUR'
  const participants = useMemo(() => (members.data ?? []).filter((member) => member.isBudgetParticipant), [members.data])

  const yearOptions = useMemo(() => {
    const options = new Set(years.data ?? [])
    options.add(new Date().getFullYear())
    return [...options].sort((left, right) => right - left)
  }, [years.data])

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

  return (
    <>
      <PageHeader
        title={t('budget.transfers.title')}
        subtitle={t('budget.transfers.subtitle')}
        actions={
          <button
            type="button"
            className="button button--primary"
            onClick={() => setEditing({ open: true, transfer: null })}
            disabled={!isOnline}
          >
            {t('budget.actions.addTransfer')}
          </button>
        }
      />

      {errorKey ? (
        <p className="notice notice--error" role="alert">
          {t(errorKey)}
        </p>
      ) : null}

      <div className="filters filters--wrap">
        <div className="filters__group">
          <label className="visually-hidden" htmlFor="transfer-year">
            {t('budget.field.settlementYear')}
          </label>
          <select
            id="transfer-year"
            className="input"
            value={settlementYear ?? ''}
            onChange={(event) => setSettlementYear(event.target.value === '' ? null : Number(event.target.value))}
          >
            <option value="">{t('budget.transfers.allYears')}</option>
            {yearOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>
        <div className="filters__group">
          <Link className="button" to="/budget">
            {t('budget.nav.summary')}
          </Link>
        </div>
      </div>

      {transfers.isPending ? <Spinner label={t('app.loading')} /> : null}
      {transfers.error ? <ErrorState error={transfers.error} onRetry={() => void transfers.refetch()} /> : null}

      {!transfers.isPending && items.length === 0 ? <EmptyState message={t('budget.transfers.empty')} /> : null}

      {items.length > 0 ? (
        <ul className="settings-list">
          {items.map((transfer) => (
            <li className="settings-row" key={transfer.id}>
              <div className="settings-row__main">
                <button
                  type="button"
                  className="settings-row__title budget-link"
                  onClick={() => setEditing({ open: true, transfer })}
                  disabled={!isOnline}
                >
                  {t('budget.transfers.direction', { from: transfer.fromMemberName, to: transfer.toMemberName })}
                </button>
                <span className="settings-row__meta">
                  {formatCalendarDate(transfer.date, i18n.language)} · {t('budget.field.settlementYear')} {transfer.settlementYear}
                  {transfer.description ? ` · ${transfer.description}` : ''}
                </span>
              </div>
              <div className="settings-row__actions">
                <span className="budget-amount">{formatMinor(transfer.amountMinor, currency, i18n.language)}</span>
                <button
                  type="button"
                  className="button button--danger"
                  onClick={() => setPendingDelete(transfer)}
                  disabled={!isOnline}
                >
                  {t('budget.actions.cancel')}
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}

      {transfers.hasNextPage ? (
        <button
          type="button"
          className="button"
          onClick={() => void transfers.fetchNextPage()}
          disabled={transfers.isFetchingNextPage}
        >
          {transfers.isFetchingNextPage ? t('app.loading') : t('common.loadMore')}
        </button>
      ) : null}

      {editing.open ? (
        <TransferDialog
          key={editing.transfer?.id ?? 'new'}
          transfer={editing.transfer}
          participants={participants}
          suggestions={suggestions.data ?? []}
          defaultYear={settlementYear ?? new Date().getFullYear()}
          timeZoneId={household.data?.timeZoneId ?? 'UTC'}
          onClose={() => setEditing({ open: false, transfer: null })}
        />
      ) : null}

      <ConfirmDialog
        open={pendingDelete !== null}
        title={t('budget.transfers.cancelTitle')}
        body={<p>{t('budget.transfers.cancelBody')}</p>}
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

function TransferDialog({
  transfer,
  participants,
  suggestions,
  defaultYear,
  timeZoneId,
  onClose,
}: {
  transfer: BudgetTransfer | null
  participants: { id: string; name: string }[]
  suggestions: BudgetTransferSuggestion[]
  defaultYear: number
  timeZoneId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const create = useCreateBudgetTransfer()
  const update = useUpdateBudgetTransfer()

  const [values, setValues] = useState<BudgetTransferFormValues>({
    date: transfer?.date ?? todayInTimeZone(timeZoneId),
    amount: minorToInputValue(transfer?.amountMinor ?? null),
    settlementYear: String(transfer?.settlementYear ?? defaultYear),
    fromMemberId: transfer?.fromMemberId ?? '',
    toMemberId: transfer?.toMemberId ?? '',
    description: transfer?.description ?? '',
  })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [errorKey, setErrorKey] = useState<string | null>(null)
  const shortcuts = useMemo(() => buildTransferShortcuts(participants, suggestions), [participants, suggestions])
  // The manual pickers stay collapsed unless they are the only way to choose, so a household
  // with two participants never sees them at all.
  const [manualOpen, setManualOpen] = useState(
    transfer !== null || values.fromMemberId === '' || values.toMemberId === '',
  )

  const busy = create.isPending || update.isPending
  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)
  const setField = <K extends keyof BudgetTransferFormValues>(key: K, value: BudgetTransferFormValues[K]) =>
    setValues((current) => ({ ...current, [key]: value }))

  const choosePair = (pair: BudgetTransferSuggestion) => {
    setValues((current) => ({
      ...current,
      fromMemberId: pair.fromMemberId,
      toMemberId: pair.toMemberId,
    }))
    setFieldErrors((current) => {
      const next = { ...current }
      delete next.fromMemberId
      delete next.toMemberId
      return next
    })
    setManualOpen(false)
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

  // Surface a malformed amount as soon as the field loses focus.
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

  const handleSubmit = async () => {
    setErrorKey(null)

    const parsed = budgetTransferFormSchema.safeParse(values)
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
    const model: BudgetTransferWriteModel = {
      date: parsed.data.date,
      settlementYear: Number(parsed.data.settlementYear),
      amountMinor,
      fromMemberId: parsed.data.fromMemberId,
      toMemberId: parsed.data.toMemberId,
      description: parsed.data.description === '' ? null : parsed.data.description,
    }

    try {
      if (transfer) {
        await update.mutateAsync({ transferId: transfer.id, model })
      } else {
        await create.mutateAsync(model)
      }

      onClose()
    } catch (error) {
      const problems = fieldProblems(error)
      if (Object.keys(problems).length > 0) {
        setFieldErrors(Object.fromEntries(Object.entries(problems).map(([field, messages]) => [field, messages[0] ?? 'errors.generic'])))
      } else {
        setErrorKey(errorMessageKey(error))
      }
    }
  }

  return (
    <FormDialog
      open
      title={transfer ? t('budget.transfers.editTitle') : t('budget.transfers.newTitle')}
      submitLabel={t('common.save')}
      busy={busy}
      errorKey={errorKey}
      onCancel={onClose}
      onSubmit={() => {
        void handleSubmit()
      }}
    >
      <div className="stack">
        <Field label={t('budget.field.amount')} htmlFor="transfer-amount" error={errorFor('amount')}>
          <input
            id="transfer-amount"
            className="input"
            type="text"
            inputMode="decimal"
            placeholder="0.00"
            value={values.amount}
            onChange={(event) => {
              setField('amount', event.target.value)
              clearAmountError()
            }}
            onBlur={validateAmount}
          />
        </Field>

        <Field label={t('budget.field.date')} htmlFor="transfer-date" error={errorFor('date')}>
          <input
            id="transfer-date"
            className="input"
            type="date"
            value={values.date}
            onChange={(event) => setField('date', event.target.value)}
          />
        </Field>

        <Field
          label={t('budget.field.settlementYear')}
          htmlFor="transfer-settlement-year"
          hint={t('budget.field.settlementYearHint')}
          error={errorFor('settlementYear')}
        >
          <input
            id="transfer-settlement-year"
            className="input"
            type="number"
            inputMode="numeric"
            min={2000}
            max={2100}
            value={values.settlementYear}
            onChange={(event) => setField('settlementYear', event.target.value)}
          />
        </Field>

        {shortcuts.length > 0 ? (
          <div className="field">
            <span className="field__label">{t('budget.transfers.suggestions')}</span>
            <div className="transfer-suggestions">
              {shortcuts.map((pair) => {
                const isSelected =
                  values.fromMemberId === pair.fromMemberId && values.toMemberId === pair.toMemberId
                return (
                  <button
                    type="button"
                    key={`${pair.fromMemberId}-${pair.toMemberId}`}
                    className={`button${isSelected ? ' button--primary' : ''}`}
                    aria-pressed={isSelected}
                    onClick={() => choosePair(pair)}
                  >
                    {t('budget.transfers.direction', { from: pair.fromMemberName, to: pair.toMemberName })}
                  </button>
                )
              })}
            </div>
          </div>
        ) : null}

        {participants.length !== 2 ? (
          <details
            className="transfer-manual"
            open={manualOpen || Boolean(fieldErrors.fromMemberId) || Boolean(fieldErrors.toMemberId)}
            onToggle={(event) => setManualOpen(event.currentTarget.open)}
          >
            <summary>{t('budget.transfers.chooseOtherPair')}</summary>

            <Field label={t('budget.field.from')} htmlFor="transfer-from" error={errorFor('fromMemberId')}>
              <select
                id="transfer-from"
                className="input"
                value={values.fromMemberId}
                onChange={(event) => setField('fromMemberId', event.target.value)}
              >
                <option value="">{t('budget.field.selectPerson')}</option>
                {participants.map((member) => (
                  <option key={member.id} value={member.id}>
                    {member.name}
                  </option>
                ))}
              </select>
            </Field>

            <Field label={t('budget.field.to')} htmlFor="transfer-to" error={errorFor('toMemberId')}>
              <select
                id="transfer-to"
                className="input"
                value={values.toMemberId}
                onChange={(event) => setField('toMemberId', event.target.value)}
              >
                <option value="">{t('budget.field.selectPerson')}</option>
                {participants.map((member) => (
                  <option key={member.id} value={member.id}>
                    {member.name}
                  </option>
                ))}
              </select>
            </Field>
          </details>
        ) : null}

        <Field label={t('budget.field.description')} htmlFor="transfer-description" error={errorFor('description')}>
          <input
            id="transfer-description"
            className="input"
            type="text"
            maxLength={500}
            value={values.description}
            onChange={(event) => setField('description', event.target.value)}
          />
        </Field>
      </div>
    </FormDialog>
  )
}
