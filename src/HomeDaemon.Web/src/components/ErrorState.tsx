import { useTranslation } from 'react-i18next'
import { errorMessageKey } from '../lib/errors'

/** A failure that replaced the content, with an optional retry. */
export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation()

  return (
    <div className="state state--error" role="alert">
      <p className="state__message">{t(errorMessageKey(error))}</p>
      {onRetry ? (
        <button type="button" className="button" onClick={onRetry}>
          {t('common.retry')}
        </button>
      ) : null}
    </div>
  )
}
