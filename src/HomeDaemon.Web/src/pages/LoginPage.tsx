import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useSession } from '../auth/session-context'
import { ApiError } from '../api/client'
import { BrandMark } from '../components/BrandMark'
import { Field } from '../components/Field'
import { describedBy } from '../lib/aria'
import { Spinner } from '../components/Spinner'
import { errorMessageKey } from '../lib/errors'
import { issuesToFieldErrors, loginSchema } from '../lib/validation'

export function LoginPage() {
  const { t } = useTranslation()
  const { user, isLoading, signIn } = useSession()

  const [values, setValues] = useState({ email: '', password: '' })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formErrorKey, setFormErrorKey] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [passwordHidden, setPasswordHidden] = useState(true)

  if (isLoading) {
    return <Spinner label={t('app.loading')} />
  }

  if (user) {
    return <Navigate to={user.mustChangePassword ? '/change-password' : '/chores'} replace />
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFormErrorKey(null)

    const parsed = loginSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setFieldErrors({})
    setSubmitting(true)

    try {
      await signIn(parsed.data.email, parsed.data.password)
    } catch (error) {
      setFormErrorKey(error instanceof ApiError && error.isUnauthorized ? 'login.invalidCredentials' : errorMessageKey(error))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-page__card">
        <div className="auth-page__brand">
          <BrandMark size={48} />
          <span className="auth-page__brand-name">{t('app.name')}</span>
        </div>

        <h1 className="auth-page__title">{t('login.title')}</h1>
        <p className="auth-page__subtitle">{t('login.subtitle')}</p>

        {formErrorKey ? (
          <p className="notice notice--error" role="alert">
            {t(formErrorKey)}
          </p>
        ) : null}

        <form className="form" onSubmit={(event) => { void handleSubmit(event) }} noValidate>
          <Field label={t('login.email')} htmlFor="login-email" error={fieldErrors.email ? t(fieldErrors.email) : undefined}>
            <input
              id="login-email"
              className="input"
              type="email"
              name="email"
              autoComplete="username"
              inputMode="email"
              required
              value={values.email}
              aria-describedby={describedBy('login-email', undefined, fieldErrors.email ? t(fieldErrors.email) : undefined)}
              onChange={(event) => setValues((current) => ({ ...current, email: event.target.value }))}
            />
          </Field>

          <Field label={t('login.password')} htmlFor="login-password" error={fieldErrors.password ? t(fieldErrors.password) : undefined}>
            <div className="input-row">
              <input
                id="login-password"
                className="input"
                type={passwordHidden ? 'password' : 'text'}
                name="password"
                autoComplete="current-password"
                required
                value={values.password}
                aria-describedby={describedBy('login-password', undefined, fieldErrors.password ? t(fieldErrors.password) : undefined)}
                onChange={(event) => setValues((current) => ({ ...current, password: event.target.value }))}
              />
              <button
                type="button"
                className="button button--ghost"
                aria-pressed={!passwordHidden}
                onClick={() => setPasswordHidden((hidden) => !hidden)}
              >
                {passwordHidden ? '👁' : '🙈'}
              </button>
            </div>
          </Field>

          <button type="submit" className="button button--primary button--full" disabled={submitting}>
            {submitting ? t('login.submitting') : t('login.submit')}
          </button>
        </form>
      </div>
    </div>
  )
}
