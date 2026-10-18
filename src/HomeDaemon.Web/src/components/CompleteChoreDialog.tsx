import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useCompleteChore } from '../hooks/useChoreMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { todayInTimeZone } from '../lib/format'
import { errorMessageKey } from '../lib/errors'
import { Field } from './Field'
import { describedBy } from '../lib/aria'
import { ConfirmDialog } from './ConfirmDialog'

interface CompleteChoreDialogProps {
  open: boolean
  choreId: string
  choreTitle: string
  timeZoneId: string
  onClose: () => void
}

/**
 * Records a manual completion. The account and its household member come from the session, so
 * the dialog never asks who did it. Offline, the action is refused instead of queued.
 */
export function CompleteChoreDialog({ open, choreId, choreTitle, timeZoneId, onClose }: CompleteChoreDialogProps) {
  const { t } = useTranslation()
  const complete = useCompleteChore()
  const isOnline = useOnlineStatus()
  const [date, setDate] = useState(() => todayInTimeZone(timeZoneId))
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const fieldId = `complete-date-${choreId}`

  const handleConfirm = async () => {
    setErrorKey(null)
    try {
      await complete.mutateAsync({ choreId, completedDate: date === '' ? null : date })
      onClose()
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    }
  }

  return (
    <ConfirmDialog
      open={open}
      title={t('chores.complete.title')}
      busy={complete.isPending}
      confirmLabel={t('chores.complete.submit')}
      busyLabel={t('chores.complete.submitting')}
      confirmTone="primary"
      onConfirm={() => {
        void handleConfirm()
      }}
      onCancel={onClose}
      body={
        <div className="stack">
          <p className="dialog__subject">{choreTitle}</p>
          <Field
            label={t('chores.complete.date')}
            htmlFor={fieldId}
            hint={t('chores.complete.dateHint')}
            error={errorKey ? t(errorKey) : undefined}
          >
            <input
              id={fieldId}
              className="input"
              type="date"
              value={date}
              aria-describedby={describedBy(fieldId, t('chores.complete.dateHint'), errorKey ? t(errorKey) : undefined)}
              onChange={(event) => setDate(event.target.value)}
            />
          </Field>
          {!isOnline ? <p className="notice notice--warning">{t('app.offline')}</p> : null}
        </div>
      }
    />
  )
}
