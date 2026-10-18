import { useId, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useDialogElement } from '../hooks/useDialogElement'

interface ConfirmDialogProps {
  open: boolean
  title: string
  body?: ReactNode
  confirmLabel: string
  busyLabel?: string
  confirmTone?: 'primary' | 'danger'
  busy?: boolean
  onConfirm: () => void
  onCancel: () => void
}

/**
 * A modal confirmation built on the native `<dialog>` element, which brings focus handling and
 * Escape support without extra dependencies.
 */
export function ConfirmDialog({
  open,
  title,
  body,
  confirmLabel,
  busyLabel,
  confirmTone = 'danger',
  busy = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
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
      <h2 className="dialog__title" id={titleId}>
        {title}
      </h2>
      {body ? <div className="dialog__body">{body}</div> : null}
      <div className="dialog__actions">
        <button type="button" className="button" onClick={onCancel} disabled={busy}>
          {t('common.cancel')}
        </button>
        <button
          type="button"
          className={`button ${confirmTone === 'danger' ? 'button--danger' : 'button--primary'}`}
          onClick={onConfirm}
          disabled={busy}
        >
          {busy ? (busyLabel ?? t('common.saving')) : confirmLabel}
        </button>
      </div>
    </dialog>
  )
}
