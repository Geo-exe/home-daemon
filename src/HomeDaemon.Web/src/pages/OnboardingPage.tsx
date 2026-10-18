import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useSession } from '../auth/session-context'
import { Field } from '../components/Field'
import { useCreateHousehold } from '../hooks/useHouseholdMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { describedBy } from '../lib/aria'
import { errorMessageKey, fieldProblems } from '../lib/errors'
import { householdOnboardingSchema, issuesToFieldErrors, type HouseholdOnboardingValues } from '../lib/validation'

/** The browser's time zone, used to pre-fill the form; "UTC" is the safe fallback. */
function detectedTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  } catch {
    return 'UTC'
  }
}

/**
 * First-run setup, shown by {@link RequireHousehold} while no household exists. Creating it is
 * an administrative step, so everyone else is told to ask an administrator.
 */
export function OnboardingPage() {
  const { t } = useTranslation()
  const { user, signOut } = useSession()
  const isAdmin = user?.roles.includes('Admin') ?? false

  if (!isAdmin) {
    return (
      <div className="auth-page">
        <div className="auth-page__card">
          <h1 className="auth-page__title">{t('onboarding.title')}</h1>
          <p className="auth-page__subtitle">{t('onboarding.adminRequired')}</p>
          <button
            type="button"
            className="button button--full"
            onClick={() => {
              void signOut()
            }}
          >
            {t('account.signOut')}
          </button>
        </div>
      </div>
    )
  }

  return <OnboardingForm />
}

function OnboardingForm() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const isOnline = useOnlineStatus()
  const create = useCreateHousehold()

  const [values, setValues] = useState<HouseholdOnboardingValues>({
    name: '',
    timeZoneId: detectedTimeZone(),
    currency: 'EUR',
  })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formErrorKey, setFormErrorKey] = useState<string | null>(null)

  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)
  const setField = <K extends keyof HouseholdOnboardingValues>(key: K, value: HouseholdOnboardingValues[K]) =>
    setValues((current) => ({ ...current, [key]: value }))

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFormErrorKey(null)

    const parsed = householdOnboardingSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setFieldErrors({})

    try {
      await create.mutateAsync({
        name: parsed.data.name,
        timeZoneId: parsed.data.timeZoneId,
        currency: parsed.data.currency.toUpperCase(),
      })

      // Adding people is the natural next step: chores and the budget both need them.
      await navigate('/settings/members', { replace: true })
    } catch (error) {
      const problems = fieldProblems(error)
      if (Object.keys(problems).length > 0) {
        setFieldErrors(
          Object.fromEntries(Object.entries(problems).map(([field, messages]) => [field, messages[0] ?? 'errors.generic'])),
        )
      } else {
        setFormErrorKey(errorMessageKey(error))
      }
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-page__card">
        <h1 className="auth-page__title">{t('onboarding.title')}</h1>
        <p className="auth-page__subtitle">{t('onboarding.subtitle')}</p>

        {formErrorKey ? (
          <p className="notice notice--error" role="alert">
            {t(formErrorKey)}
          </p>
        ) : null}

        <form className="form" onSubmit={(event) => { void handleSubmit(event) }} noValidate>
          <Field label={t('onboarding.name')} htmlFor="onboarding-name" error={errorFor('name')}>
            <input
              id="onboarding-name"
              className="input"
              type="text"
              required
              maxLength={200}
              value={values.name}
              onChange={(event) => setField('name', event.target.value)}
            />
          </Field>

          <Field
            label={t('onboarding.timeZone')}
            htmlFor="onboarding-timezone"
            hint={t('onboarding.timeZoneHint')}
            error={errorFor('timeZoneId')}
          >
            <input
              id="onboarding-timezone"
              className="input"
              type="text"
              required
              autoComplete="off"
              spellCheck={false}
              placeholder="Europe/Rome"
              value={values.timeZoneId}
              aria-describedby={describedBy('onboarding-timezone', t('onboarding.timeZoneHint'), errorFor('timeZoneId'))}
              onChange={(event) => setField('timeZoneId', event.target.value)}
            />
          </Field>

          <Field
            label={t('onboarding.currency')}
            htmlFor="onboarding-currency"
            hint={t('onboarding.currencyHint')}
            error={errorFor('currency')}
          >
            <input
              id="onboarding-currency"
              className="input"
              type="text"
              required
              maxLength={3}
              autoComplete="off"
              spellCheck={false}
              placeholder="EUR"
              value={values.currency}
              aria-describedby={describedBy('onboarding-currency', t('onboarding.currencyHint'), errorFor('currency'))}
              onChange={(event) => setField('currency', event.target.value)}
            />
          </Field>

          <button type="submit" className="button button--primary button--full" disabled={create.isPending || !isOnline}>
            {create.isPending ? t('onboarding.submitting') : t('onboarding.submit')}
          </button>
        </form>

        <p className="field__hint">{t('onboarding.nextSteps')}</p>
      </div>
    </div>
  )
}
