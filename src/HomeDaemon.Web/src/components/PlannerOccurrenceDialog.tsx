import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { HouseholdMember, PlannerOccurrence } from '../api/types'
import { ConfirmDialog } from './ConfirmDialog'
import { Field } from './Field'
import { useUpdateOccurrence } from '../hooks/usePlannerMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { errorMessageKey } from '../lib/errors'

interface PlannerOccurrenceDialogProps {
  occurrence: PlannerOccurrence | null
  members: HouseholdMember[]
  onClose: () => void
}

export function PlannerOccurrenceDialog({ occurrence, members, onClose }: PlannerOccurrenceDialogProps) {
  if (occurrence === null) {
    return null
  }

  // Keyed so switching occurrence starts from that occurrence's values, without an effect.
  return <OccurrenceEditor key={occurrence.choreId} occurrence={occurrence} members={members} onClose={onClose} />
}

function OccurrenceEditor({ occurrence, members, onClose }: { occurrence: PlannerOccurrence } & Omit<PlannerOccurrenceDialogProps, 'occurrence'>) {
  const { t } = useTranslation()
  const isOnline = useOnlineStatus()
  const update = useUpdateOccurrence()

  const [date, setDate] = useState(occurrence.dueDate)
  const [memberId, setMemberId] = useState(occurrence.memberId)
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const handleConfirm = async () => {
    if (date === '' || memberId === '') {
      setErrorKey('planner.move.invalid')
      return
    }

    setErrorKey(null)
    try {
      await update.mutateAsync({ choreId: occurrence.choreId, date, memberId })
      onClose()
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    }
  }

  return (
    <ConfirmDialog
      open
      title={t('planner.move.title')}
      busy={update.isPending}
      confirmLabel={t('common.save')}
      confirmTone="primary"
      onConfirm={() => {
        void handleConfirm()
      }}
      onCancel={onClose}
      body={
        <div className="stack">
          <p className="dialog__subject">{occurrence.title}</p>

          <Field label={t('planner.move.date')} htmlFor="occurrence-date">
            <input
              id="occurrence-date"
              className="input"
              type="date"
              value={date}
              onChange={(event) => setDate(event.target.value)}
            />
          </Field>

          <Field label={t('planner.move.member')} htmlFor="occurrence-member">
            <select
              id="occurrence-member"
              className="input"
              value={memberId}
              onChange={(event) => setMemberId(event.target.value)}
            >
              {members.map((member) => (
                <option key={member.id} value={member.id}>
                  {member.name}
                </option>
              ))}
            </select>
          </Field>

          <p className="field__hint">{t('planner.move.hint')}</p>
          {errorKey ? <p className="notice notice--error" role="alert">{t(errorKey)}</p> : null}
          {!isOnline ? <p className="notice notice--warning">{t('app.offline')}</p> : null}
        </div>
      }
    />
  )
}
