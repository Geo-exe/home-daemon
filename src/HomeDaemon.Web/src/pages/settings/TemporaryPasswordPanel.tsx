import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { TemporaryPassword } from '../../api/accounts'

/**
 * Shows a one-time password. It is only ever returned by the API at creation or reset time, so
 * the panel is meant to be dismissed once the password has been handed over.
 */
export function TemporaryPasswordPanel({ value, onDismiss }: { value: TemporaryPassword; onDismiss: () => void }) {
  const { t } = useTranslation()
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(`${value.user.email} ${value.temporaryPassword}`)
      setCopied(true)
    } catch {
      // Clipboard access can be unavailable; the value stays on screen to be read.
      setCopied(false)
    }
  }

  return (
    <section className="card card--accent" role="status">
      <h2 className="card__title">{t('settings.accounts.temporaryPassword')}</h2>
      <p className="field__hint">{t('settings.accounts.temporaryPasswordHint')}</p>
      <p className="credential">
        <code>{value.user.email}</code>
        <span aria-hidden="true"> · </span>
        <code>{value.temporaryPassword}</code>
      </p>
      <div className="dialog__actions">
        <button
          type="button"
          className="button"
          onClick={() => {
            void copy()
          }}
        >
          {copied ? t('settings.accounts.copied') : t('settings.accounts.copy')}
        </button>
        <button type="button" className="button button--ghost" onClick={onDismiss}>
          {t('settings.accounts.dismiss')}
        </button>
      </div>
    </section>
  )
}
