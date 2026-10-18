import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useSession } from '../auth/session-context'
import { changePassword } from '../api/auth'
import { Field } from '../components/Field'
import { describedBy } from '../lib/aria'
import { Spinner } from '../components/Spinner'
import { errorMessageKey, fieldProblems } from '../lib/errors'
import { changePasswordSchema, issuesToFieldErrors } from '../lib/validation'

/**
 * Reached automatically while the account still carries the mandatory-change flag, and also
 * from the account menu. The API refuses everything else until the change succeeds.
 */
export function ChangePasswordPage() {
  const { t } = useTranslation()
  const { user, isLoading, reload } = useSession()
  const navigate = useNavigate()

  const [values, setValues] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formErrorKey, setFormErrorKey] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  if (isLoading) {
    return <Spinner label={t('app.loading')} />
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  if (!user.mustChangePassword) {
    return <Navigate to="/chores" replace />
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFormErrorKey(null)

    const parsed = changePasswordSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setSubmitting(true)

    try {
      await changePassword(parsed.data.currentPassword, parsed.data.newPassword)
      await reload()
      await navigate('/chores', { replace: true })
    } catch (error) {
      const problems = fieldProblems(error)
      if (Object.keys(problems).length > 0) {
        setFieldErrors(Object.fromEntries(Object.entries(problems).map(([field, messages]) => [field, messages[0] ?? 'errors.generic'])))
      } else {
        setFormErrorKey(errorMessageKey(error))
      }
    } finally {
      setSubmitting(false)
    }
  }

  const errorFor = (name: keyof typeof values): string | undefined =>
    fieldErrors[name] ? t(fieldErrors[name]!) : undefined

  return (
    <div className="auth-page">
      <div className="auth-page__card">
        <h1 className="auth-page__title">{t('changePassword.title')}</h1>
        <p className="auth-page__subtitle">{t('changePassword.subtitle')}</p>

        {formErrorKey ? (
          <p className="notice notice--error" role="alert">
            {t(formErrorKey)}
          </p>
        ) : null}

        <form className="form" onSubmit={(event) => { void handleSubmit(event) }} noValidate>
          <Field label={t('changePassword.current')} htmlFor="current-password" error={errorFor('currentPassword')}>
            <input
              id="current-password"
              className="input"
              type="password"
              autoComplete="current-password"
              required
              value={values.currentPassword}
              aria-describedby={describedBy('current-password', undefined, errorFor('currentPassword'))}
              onChange={(event) => setValues((current) => ({ ...current, currentPassword: event.target.value }))}
            />
          </Field>

          <Field label={t('changePassword.new')} htmlFor="new-password" error={errorFor('newPassword')}>
            <input
              id="new-password"
              className="input"
              type="password"
              autoComplete="new-password"
              required
              value={values.newPassword}
              aria-describedby={describedBy('new-password', undefined, errorFor('newPassword'))}
              onChange={(event) => setValues((current) => ({ ...current, newPassword: event.target.value }))}
            />
          </Field>

          <Field label={t('changePassword.confirm')} htmlFor="confirm-password" error={errorFor('confirmPassword')}>
            <input
              id="confirm-password"
              className="input"
              type="password"
              autoComplete="new-password"
              required
              value={values.confirmPassword}
              aria-describedby={describedBy('confirm-password', undefined, errorFor('confirmPassword'))}
              onChange={(event) => setValues((current) => ({ ...current, confirmPassword: event.target.value }))}
            />
          </Field>

          <button type="submit" className="button button--primary button--full" disabled={submitting}>
            {submitting ? t('changePassword.submitting') : t('changePassword.submit')}
          </button>
        </form>
      </div>
    </div>
  )
}
