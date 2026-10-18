import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import type { ChoreListItem } from '../api/types'
import { useSetChoreActive } from '../hooks/useChoreMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { errorMessageKey } from '../lib/errors'
import { formatCalendarDate, formatMinutes } from '../lib/format'
import { CompleteChoreDialog } from './CompleteChoreDialog'
import { DeleteChoreDialog } from './DeleteChoreDialog'
import { ScheduleStateBadge } from './ScheduleStateBadge'

interface ChoreCardProps {
  chore: ChoreListItem
  roomName: string | null
  memberName: string | null
  timeZoneId: string
}

export function ChoreCard({ chore, roomName, memberName, timeZoneId }: ChoreCardProps) {
  const { t, i18n } = useTranslation()
  const isOnline = useOnlineStatus()
  const setActive = useSetChoreActive()

  const [completeOpen, setCompleteOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const planned = formatCalendarDate(chore.nextDueDate, i18n.language)

  const toggleActive = async () => {
    setErrorKey(null)
    try {
      await setActive.mutateAsync({ choreId: chore.id, isActive: !chore.isActive })
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    }
  }

  return (
    <li className="chore-card">
      <div className="chore-card__main">
        <div className="chore-card__heading">
          <h2 className="chore-card__title">
            <Link to={`/chores/${chore.id}`}>{chore.title}</Link>
          </h2>
          <ScheduleStateBadge state={chore.scheduleState} />
        </div>

        <ul className="chore-card__meta">
          {roomName ? <li>{roomName}</li> : null}
          <li>{formatMinutes(chore.estimatedMinutes, i18n.language)}</li>
          {planned ? (
            <li>
              {t('chores.dates.nextDue')}: {planned}
            </li>
          ) : null}
          {memberName ? <li>{memberName}</li> : null}
        </ul>

        {chore.isLocked ? <p className="chore-card__flag">{t('chores.detail.locked')}</p> : null}
        {!chore.isActive ? <p className="chore-card__flag">{t('chores.state.Paused')}</p> : null}
        {errorKey ? (
          <p className="notice notice--error" role="alert">
            {t(errorKey)}
          </p>
        ) : null}
      </div>

      <div className="chore-card__actions">
        <button
          type="button"
          className="button button--primary"
          onClick={() => setCompleteOpen(true)}
          disabled={!isOnline}
        >
          {t('chores.actions.complete')}
        </button>
        <button type="button" className="button" onClick={() => { void toggleActive() }} disabled={!isOnline || setActive.isPending}>
          {chore.isActive ? t('chores.actions.pause') : t('chores.actions.resume')}
        </button>
        <Link className="button" to={`/chores/${chore.id}/edit`}>
          {t('chores.actions.edit')}
        </Link>
        <button type="button" className="button button--danger" onClick={() => setDeleteOpen(true)} disabled={!isOnline}>
          {t('chores.actions.delete')}
        </button>
      </div>

      <CompleteChoreDialog
        open={completeOpen}
        choreId={chore.id}
        choreTitle={chore.title}
        timeZoneId={timeZoneId}
        onClose={() => setCompleteOpen(false)}
      />
      <DeleteChoreDialog
        open={deleteOpen}
        choreId={chore.id}
        choreTitle={chore.title}
        onClose={() => setDeleteOpen(false)}
      />
    </li>
  )
}
