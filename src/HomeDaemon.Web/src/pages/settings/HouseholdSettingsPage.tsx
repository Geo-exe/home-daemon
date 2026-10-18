import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getHousehold, householdQueryKey, type HouseholdWriteModel } from '../../api/household'
import type { Household } from '../../api/types'
import { ErrorState } from '../../components/ErrorState'
import { Field } from '../../components/Field'
import { describedBy } from '../../lib/aria'
import { Spinner } from '../../components/Spinner'
import { useUpdateHousehold } from '../../hooks/useHouseholdMutations'
import { useOnlineStatus } from '../../hooks/useOnlineStatus'
import { errorMessageKey, fieldProblems } from '../../lib/errors'
import { householdFormSchema, issuesToFieldErrors, type HouseholdFormValues } from '../../lib/validation'

function toFormValues(household: Household): HouseholdFormValues {
  return {
    name: household.name,
    timeZoneId: household.timeZoneId,
    planningHorizonDays: String(household.planningHorizonDays),
    // The API returns HH:mm:ss; the time input wants HH:mm.
    dailyWindowStart: household.dailyWindowStart.slice(0, 5),
    dailyWindowEnd: household.dailyWindowEnd.slice(0, 5),
    maxChoreMinutesPerWeekday: household.maxChoreMinutesPerWeekday === null ? '' : String(household.maxChoreMinutesPerWeekday),
    maxChoreMinutesPerWeekendDay: household.maxChoreMinutesPerWeekendDay === null ? '' : String(household.maxChoreMinutesPerWeekendDay),
  }
}

export function HouseholdSettingsPage() {
  const { t } = useTranslation()
  const household = useQuery({ queryKey: householdQueryKey, queryFn: ({ signal }) => getHousehold(signal) })

  if (household.isPending) {
    return <Spinner label={t('app.loading')} />
  }

  if (household.error || !household.data) {
    return <ErrorState error={household.error} onRetry={() => void household.refetch()} />
  }

  return <HouseholdForm key={household.data.id} initialValues={toFormValues(household.data)} />
}

function HouseholdForm({ initialValues }: { initialValues: HouseholdFormValues }) {
  const { t } = useTranslation()
  const isOnline = useOnlineStatus()
  const save = useUpdateHousehold()

  const [values, setValues] = useState(initialValues)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [errorKey, setErrorKey] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)

  const setField = <K extends keyof HouseholdFormValues>(key: K, value: HouseholdFormValues[K]) => {
    setSaved(false)
    setValues((current) => ({ ...current, [key]: value }))
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setErrorKey(null)
    setSaved(false)

    const parsed = householdFormSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setFieldErrors({})

    const model: HouseholdWriteModel = {
      name: parsed.data.name,
      timeZoneId: parsed.data.timeZoneId,
      planningHorizonDays: parsed.data.planningHorizonDays,
      dailyWindowStart: parsed.data.dailyWindowStart,
      dailyWindowEnd: parsed.data.dailyWindowEnd,
      maxChoreMinutesPerWeekday: parsed.data.maxChoreMinutesPerWeekday,
      maxChoreMinutesPerWeekendDay: parsed.data.maxChoreMinutesPerWeekendDay,
    }

    try {
      await save.mutateAsync(model)
      setSaved(true)
    } catch (error) {
      const problems = fieldProblems(error)
      if (Object.keys(problems).length > 0) {
        setFieldErrors(
          Object.fromEntries(Object.entries(problems).map(([field, messages]) => [field, messages[0] ?? 'errors.generic'])),
        )
      } else {
        setErrorKey(errorMessageKey(error))
      }
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
          {t('settings.household.saved')}
        </p>
      ) : null}

      <form className="form form--grid" onSubmit={(event) => { void handleSubmit(event) }} noValidate>
        <div className="form__row">
          <Field label={t('settings.household.name')} htmlFor="household-name" error={errorFor('name')}>
            <input
              id="household-name"
              className="input"
              type="text"
              required
              maxLength={200}
              value={values.name}
              onChange={(event) => setField('name', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field
            label={t('settings.household.timeZone')}
            htmlFor="household-timezone"
            hint={t('settings.household.timeZoneHint')}
            error={errorFor('timeZoneId')}
          >
            <input
              id="household-timezone"
              className="input"
              type="text"
              required
              autoComplete="off"
              spellCheck={false}
              placeholder="Europe/Rome"
              value={values.timeZoneId}
              aria-describedby={describedBy('household-timezone', t('settings.household.timeZoneHint'), errorFor('timeZoneId'))}
              onChange={(event) => setField('timeZoneId', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field
            label={t('settings.household.planningHorizon')}
            htmlFor="household-horizon"
            hint={t('settings.household.planningHorizonHint')}
            error={errorFor('planningHorizonDays')}
          >
            <input
              id="household-horizon"
              className="input"
              type="number"
              inputMode="numeric"
              min={1}
              max={365}
              value={values.planningHorizonDays}
              aria-describedby={describedBy('household-horizon', t('settings.household.planningHorizonHint'), errorFor('planningHorizonDays'))}
              onChange={(event) => setField('planningHorizonDays', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field label={t('settings.household.windowStart')} htmlFor="household-window-start" error={errorFor('dailyWindowStart')}>
            <input
              id="household-window-start"
              className="input"
              type="time"
              required
              value={values.dailyWindowStart}
              onChange={(event) => setField('dailyWindowStart', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field label={t('settings.household.windowEnd')} htmlFor="household-window-end" error={errorFor('dailyWindowEnd')}>
            <input
              id="household-window-end"
              className="input"
              type="time"
              required
              value={values.dailyWindowEnd}
              onChange={(event) => setField('dailyWindowEnd', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field
            label={t('settings.household.maxWeekday')}
            htmlFor="household-max-weekday"
            hint={t('settings.household.maxHint')}
            error={errorFor('maxChoreMinutesPerWeekday')}
          >
            <input
              id="household-max-weekday"
              className="input"
              type="number"
              inputMode="numeric"
              min={0}
              value={values.maxChoreMinutesPerWeekday}
              onChange={(event) => setField('maxChoreMinutesPerWeekday', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field
            label={t('settings.household.maxWeekend')}
            htmlFor="household-max-weekend"
            hint={t('settings.household.maxHint')}
            error={errorFor('maxChoreMinutesPerWeekendDay')}
          >
            <input
              id="household-max-weekend"
              className="input"
              type="number"
              inputMode="numeric"
              min={0}
              value={values.maxChoreMinutesPerWeekendDay}
              onChange={(event) => setField('maxChoreMinutesPerWeekendDay', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row form__row--wide">
          <button type="submit" className="button button--primary" disabled={save.isPending || !isOnline}>
            {save.isPending ? t('settings.household.saving') : t('settings.household.save')}
          </button>
        </div>
      </form>
    </>
  )
}
