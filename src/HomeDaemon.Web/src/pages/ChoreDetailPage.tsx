import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { choreQueryKey, getChore } from '../api/chores'
import { getHousehold, getMembers, getRooms, householdQueryKey, membersQueryKey, roomsQueryKey } from '../api/household'
import { CompleteChoreDialog } from '../components/CompleteChoreDialog'
import { DeleteChoreDialog } from '../components/DeleteChoreDialog'
import { ErrorState } from '../components/ErrorState'
import { PageHeader } from '../components/PageHeader'
import { ScheduleStateBadge } from '../components/ScheduleStateBadge'
import { Spinner } from '../components/Spinner'
import { useSetChoreActive } from '../hooks/useChoreMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { errorMessageKey, isNotFound } from '../lib/errors'
import { formatCalendarDate, formatDateTime, formatMinutes } from '../lib/format'
import { recurrenceLabel } from '../lib/recurrence'

export function ChoreDetailPage() {
  const { t, i18n } = useTranslation()
  const { choreId = '' } = useParams()
  const navigate = useNavigate()
  const isOnline = useOnlineStatus()
  const setActive = useSetChoreActive()

  const [completeOpen, setCompleteOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const chore = useQuery({ queryKey: choreQueryKey(choreId), queryFn: ({ signal }) => getChore(choreId, signal) })
  const household = useQuery({ queryKey: householdQueryKey, queryFn: ({ signal }) => getHousehold(signal) })
  const rooms = useQuery({ queryKey: roomsQueryKey, queryFn: ({ signal }) => getRooms(signal) })
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })

  if (chore.isPending) {
    return <Spinner label={t('app.loading')} />
  }

  if (chore.error) {
    return (
      <>
        <PageHeader
          title={t('chores.title')}
          actions={
            <Link className="button" to="/chores">
              {t('chores.detail.backToList')}
            </Link>
          }
        />
        <ErrorState error={chore.error} onRetry={isNotFound(chore.error) ? undefined : () => void chore.refetch()} />
      </>
    )
  }

  const data = chore.data
  if (data === undefined) {
    return <Spinner label={t('app.loading')} />
  }

  const roomName = data.roomId ? (rooms.data?.find((room) => room.id === data.roomId)?.name ?? null) : null
  const assignee = data.fixedAssigneeMemberId
    ? (members.data?.find((member) => member.id === data.fixedAssigneeMemberId)?.name ?? null)
    : null
  const plannedMember = data.plannedMemberId
    ? (members.data?.find((member) => member.id === data.plannedMemberId)?.name ?? null)
    : null

  const recurrence = recurrenceLabel(data.frequency, data.interval)
  const timeZoneId = household.data?.timeZoneId ?? 'UTC'

  const toggleActive = async () => {
    setErrorKey(null)
    try {
      await setActive.mutateAsync({ choreId: data.id, isActive: !data.isActive })
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    }
  }

  return (
    <>
      <PageHeader
        title={data.title}
        subtitle={roomName ?? undefined}
        actions={
          <>
            <Link className="button" to="/chores">
              {t('chores.detail.backToList')}
            </Link>
            <Link className="button" to={`/chores/${data.id}/edit`}>
              {t('chores.actions.edit')}
            </Link>
          </>
        }
      />

      <div className="detail-stack">
        <div className="detail-actions">
          <button
            type="button"
            className="button button--primary"
            onClick={() => setCompleteOpen(true)}
            disabled={!isOnline}
          >
            {t('chores.actions.complete')}
          </button>
          <button
            type="button"
            className="button"
            onClick={() => { void toggleActive() }}
            disabled={!isOnline || setActive.isPending}
          >
            {data.isActive ? t('chores.actions.pause') : t('chores.actions.resume')}
          </button>
          <button type="button" className="button button--danger" onClick={() => setDeleteOpen(true)} disabled={!isOnline}>
            {t('chores.actions.delete')}
          </button>
        </div>

        {errorKey ? (
          <p className="notice notice--error" role="alert">
            {t(errorKey)}
          </p>
        ) : null}

        <section className="card">
          <div className="card__heading">
            <h2 className="card__title">{t('chores.detail.schedule')}</h2>
            <ScheduleStateBadge state={data.scheduleState} />
          </div>
          <dl className="definition-list">
            <div>
              <dt>{t('chores.dates.lastCompleted')}</dt>
              <dd>{formatCalendarDate(data.lastCompletedDate, i18n.language) ?? '—'}</dd>
            </div>
            <div>
              <dt>{t('chores.dates.nextOccurrence')}</dt>
              <dd>{formatCalendarDate(data.nextOccurrenceDate, i18n.language) ?? '—'}</dd>
            </div>
            <div>
              <dt>{t('chores.dates.nextDue')}</dt>
              <dd>{formatCalendarDate(data.nextDueDate, i18n.language) ?? '—'}</dd>
            </div>
            {plannedMember ? (
              <div>
                <dt>{t('chores.dates.plannedMember')}</dt>
                <dd>{plannedMember}</dd>
              </div>
            ) : null}
            {data.isLocked ? (
              <div>
                <dt>{t('chores.detail.locked')}</dt>
                <dd>{t('common.yes')}</dd>
              </div>
            ) : null}
            {data.unscheduledReason ? (
              <div>
                <dt>{t('chores.detail.unscheduledReason')}</dt>
                <dd>{data.unscheduledReason}</dd>
              </div>
            ) : null}
          </dl>
        </section>

        <section className="card">
          <h2 className="card__title">{t('chores.detail.recurrence')}</h2>
          <dl className="definition-list">
            <div>
              <dt>{t('chores.field.frequency')}</dt>
              <dd>{t(recurrence.key, recurrence.options)}</dd>
            </div>
            <div>
              <dt>{t('chores.field.mode')}</dt>
              <dd>{t(data.mode === 'Dynamic' ? 'chores.recurrence.rollOnCompletion' : 'chores.recurrence.anchored')}</dd>
            </div>
            <div>
              <dt>{t('chores.field.duration')}</dt>
              <dd>{formatMinutes(data.estimatedMinutes, i18n.language)}</dd>
            </div>
            <div>
              <dt>{t('chores.field.assignment')}</dt>
              <dd>
                {t(`enums.assignment.${data.assignmentPolicy}`)}
                {assignee ? ` · ${assignee}` : ''}
              </dd>
            </div>
            {data.anchorDate ? (
              <div>
                <dt>{t('chores.field.anchorDate')}</dt>
                <dd>{formatCalendarDate(data.anchorDate, i18n.language)}</dd>
              </div>
            ) : null}
            <div>
              <dt>{t('chores.field.active')}</dt>
              <dd>{data.isActive ? t('common.yes') : t('chores.state.Paused')}</dd>
            </div>
          </dl>
        </section>

        <section className="card">
          <h2 className="card__title">{t('chores.detail.description')}</h2>
          <p>{data.description ?? t('chores.detail.noDescription')}</p>
        </section>

        <section className="card">
          <h2 className="card__title">{t('chores.detail.history')}</h2>
          {data.recentCompletions.length === 0 ? (
            <p>{t('chores.detail.noHistory')}</p>
          ) : (
            <ul className="completion-list">
              {data.recentCompletions.map((completion) => {
                const member = completion.completedByMemberId
                  ? (members.data?.find((entry) => entry.id === completion.completedByMemberId)?.name ?? null)
                  : null

                return (
                  <li key={completion.id} className="completion-list__item">
                    <span className="completion-list__moment">{formatCalendarDate(completion.completedDate, i18n.language)}</span>
                    <span className="completion-list__meta">
                      {t(`enums.source.${completion.source}`)}
                      {member ? ` · ${member}` : ''}
                    </span>
                    <span className="completion-list__meta">{formatDateTime(completion.recordedAtUtc, i18n.language)}</span>
                  </li>
                )
              })}
            </ul>
          )}
        </section>
      </div>

      <CompleteChoreDialog
        open={completeOpen}
        choreId={data.id}
        choreTitle={data.title}
        timeZoneId={timeZoneId}
        onClose={() => setCompleteOpen(false)}
      />
      <DeleteChoreDialog
        open={deleteOpen}
        choreId={data.id}
        choreTitle={data.title}
        onClose={() => setDeleteOpen(false)}
        onDeleted={() => {
          void navigate('/chores', { replace: true })
        }}
      />
    </>
  )
}
