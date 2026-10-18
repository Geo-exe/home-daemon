import { useId, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useDialogElement } from '../hooks/useDialogElement'

interface FormDialogProps {
  open: boolean
  title: string
  submitLabel: string
  busyLabel?: string
  busy?: boolean
  errorKey?: string | null
  onSubmit: () => void
  onCancel: () => void
  children: ReactNode
}

/** A modal form: the dialog chrome reuses the same native `<dialog>` behaviour as confirmations. */
export function FormDialog({
  open,
  title,
  submitLabel,
  busyLabel,
  busy = false,
  errorKey = null,
  onSubmit,
  onCancel,
  children,
}: FormDialogProps) {
  const { t } = useTranslation()
  const ref = useDialogElement(open)
  const titleId = useId()

  return (
    <dialog
      ref={ref}
      className="dialog"
      aria-labelledby={titleId}
      onCancel={(event) => {
        event.preventDefault()
        onCancel()
      }}
    >
      <form
        className="form"
        onSubmit={(event) => {
          event.preventDefault()
          onSubmit()
        }}
      >
        <h2 className="dialog__title" id={titleId}>
          {title}
        </h2>

        <div className="dialog__body">{children}</div>

        {errorKey ? (
          <p className="notice notice--error" role="alert">
            {t(errorKey)}
          </p>
        ) : null}

        <div className="dialog__actions">
          <button type="button" className="button" onClick={onCancel} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="submit" className="button button--primary" disabled={busy}>
            {busy ? (busyLabel ?? t('common.saving')) : submitLabel}
          </button>
        </div>
      </form>
    </dialog>
  )
}
