import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getMembers, getRooms, householdQueryKey, membersQueryKey, roomsQueryKey, getHousehold } from '../api/household'
import { getPlanner, plannerQueryKey } from '../api/planner'
import type { CalendarDate, PlannerOccurrence } from '../api/types'
import { ErrorState } from '../components/ErrorState'
import { EmptyState } from '../components/EmptyState'
import { PageHeader } from '../components/PageHeader'
import { PlannerOccurrenceDialog } from '../components/PlannerOccurrenceDialog'
import { Spinner } from '../components/Spinner'
import { useRecalculate, useUnlockOccurrence } from '../hooks/usePlannerMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { errorMessageKey } from '../lib/errors'
import { formatCalendarDate, formatMinutes, todayInTimeZone } from '../lib/format'
import { freeRatio, plannerWindow, shiftWindow } from '../lib/planner'

const shiftDays = 7

export function PlannerPage() {
  const { t, i18n } = useTranslation()
  const isOnline = useOnlineStatus()

  const [anchoredFrom, setAnchoredFrom] = useState<CalendarDate | null>(null)
  const [editing, setEditing] = useState<PlannerOccurrence | null>(null)
  const [notice, setNotice] = useState<{ key: string; options?: Record<string, number> } | null>(null)

  const household = useQuery({ queryKey: householdQueryKey, queryFn: ({ signal }) => getHousehold(signal) })
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })
  const rooms = useQuery({ queryKey: roomsQueryKey, queryFn: ({ signal }) => getRooms(signal) })

  const horizonDays = household.data?.planningHorizonDays ?? 30
  const timeZoneId = household.data?.timeZoneId ?? 'UTC'
  const from = anchoredFrom ?? todayInTimeZone(timeZoneId)
  const window = plannerWindow(from, horizonDays)

  const planner = useQuery({
    queryKey: [...plannerQueryKey, window.from, window.to],
    queryFn: ({ signal }) => getPlanner(window.from, window.to, signal),
  })

  const recalculate = useRecalculate()
  const unlock = useUnlockOccurrence()

  const memberNames = useMemo(() => new Map((members.data ?? []).map((member) => [member.id, member.name])), [members.data])
  const roomNames = useMemo(() => new Map((rooms.data ?? []).map((room) => [room.id, room.name])), [rooms.data])

  const isPending = planner.isPending || household.isPending
  const failure = planner.error ?? household.error ?? members.error

  const refresh = () => {
    void planner.refetch()
    void household.refetch()
    void members.refetch()
  }

  const handleRecalculate = async () => {
    setNotice(null)
    try {
      const run = await recalculate.mutateAsync()
      if (run === null) {
        setNotice({ key: 'planner.runInterrupted' })
      } else if (run.status === 'Failed') {
        setNotice({ key: 'planner.runFailed' })
      } else {
        setNotice({
          key: 'planner.runSucceeded',
          options: { scheduled: run.scheduledCount, unscheduled: run.unscheduledCount },
        })
      }
    } catch (error) {
      setNotice({ key: errorMessageKey(error) })
    }
  }

  const handleUnlock = async (choreId: string) => {
    setNotice(null)
    try {
      await unlock.mutateAsync(choreId)
    } catch (error) {
      setNotice({ key: errorMessageKey(error) })
    }
  }

  return (
    <>
      <PageHeader
        title={t('planner.title')}
        subtitle={t('planner.subtitle', { count: horizonDays })}
        actions={
          <button
            type="button"
            className="button button--primary"
            onClick={() => { void handleRecalculate() }}
            disabled={!isOnline || recalculate.isPending}
          >
            {recalculate.isPending ? t('planner.recalculating') : t('planner.recalculate')}
          </button>
        }
      />

      <div className="window-nav">
        <button
          type="button"
          className="button"
          onClick={() => setAnchoredFrom(shiftWindow(from, window.to, -shiftDays).from)}
        >
          {t('planner.previous')}
        </button>
        <button type="button" className="button" onClick={() => setAnchoredFrom(todayInTimeZone(timeZoneId))}>
          {t('planner.today')}
        </button>
        <button
          type="button"
          className="button"
          onClick={() => setAnchoredFrom(shiftWindow(from, window.to, shiftDays).from)}
        >
          {t('planner.next')}
        </button>
      </div>

      {notice ? (
        <p className="notice" role="status">
          {t(notice.key, notice.options)}
        </p>
      ) : null}

      {isPending ? <Spinner label={t('app.loading')} /> : null}
      {!isPending && failure ? <ErrorState error={failure} onRetry={refresh} /> : null}

      {!isPending && !failure && planner.data ? (
        <>
          {!planner.data.calendarDataAvailable ? (
            <p className="notice notice--warning" role="status">
              {t('planner.calendarUnavailable')}
            </p>
          ) : null}

          {planner.data.days.length === 0 ? <EmptyState message={t('planner.empty')} /> : null}

          <div className="planner-days">
            {planner.data.days.map((day) => (
              <article className="planner-day" key={day.date}>
                <header className="planner-day__header">
                  <time className="planner-day__date" dateTime={day.date}>
                    {formatCalendarDate(day.date, i18n.language)}
                  </time>
                  <span className="planner-day__count">
                    {t('chores.subtitle', { count: day.occurrences.length })}
                  </span>
                </header>

                <ul className="load-list">
                  {day.members.map((memberDay) => {
                    const label = memberNames.get(memberDay.memberId) ?? t('common.unknown')
                    const ratio = freeRatio(memberDay.freeMinutes, memberDay.windowMinutes)

                    return (
                      <li className="load" key={memberDay.memberId}>
                        <div className="load__head">
                          <span className="load__name">{label}</span>
                          <span className="load__free">
                            {t('planner.load.free')}: {formatMinutes(memberDay.freeMinutes, i18n.language)}
                          </span>
                        </div>
                        <div className="load__bar">
                          <span className="load__fill" style={{ width: `${Math.round(ratio * 100)}%` }} />
                        </div>
                        <p className="load__detail">
                          {t('planner.load.window')} {formatMinutes(memberDay.windowMinutes, i18n.language)} ·{' '}
                          {t('planner.load.calendar')} {formatMinutes(memberDay.calendarOccupiedMinutes, i18n.language)} ·{' '}
                          {t('planner.load.planned')} {formatMinutes(memberDay.scheduledMinutes, i18n.language)}
                          {memberDay.isAllDayBlocked ? ` · ${t('planner.load.allDayBlocked')}` : ''}
                        </p>
                      </li>
                    )
                  })}
                </ul>

                {day.occurrences.length === 0 ? (
                  <p className="planner-day__empty">{t('planner.emptyDay')}</p>
                ) : (
                  <ul className="occurrence-list">
                    {day.occurrences.map((occurrence) => (
                      <li className="occurrence" key={occurrence.choreId}>
                        <div className="occurrence__main">
                          <Link className="occurrence__title" to={`/chores/${occurrence.choreId}`}>
                            {occurrence.title}
                          </Link>
                          <ul className="occurrence__meta">
                            {occurrence.roomId ? <li>{roomNames.get(occurrence.roomId) ?? ''}</li> : null}
                            <li>{formatMinutes(occurrence.estimatedMinutes, i18n.language)}</li>
                            <li>{memberNames.get(occurrence.memberId) ?? t('common.unknown')}</li>
                            {occurrence.isLocked ? (
                              <li className="occurrence__lock">{t('planner.occurrence.locked')}</li>
                            ) : null}
                          </ul>
                        </div>

                        <div className="occurrence__actions">
                          <button
                            type="button"
                            className="button"
                            onClick={() => setEditing(occurrence)}
                            disabled={!isOnline}
                          >
                            {t('planner.occurrence.move')}
                          </button>
                          {occurrence.isLocked ? (
                            <button
                              type="button"
                              className="button"
                              onClick={() => { void handleUnlock(occurrence.choreId) }}
                              disabled={!isOnline || unlock.isPending}
                            >
                              {t('planner.occurrence.unlock')}
                            </button>
                          ) : null}
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </article>
            ))}
          </div>

          <section className="card">
            <h2 className="card__title">{t('planner.unscheduled.title')}</h2>
            {planner.data.unscheduled.length === 0 ? (
              <p>{t('planner.unscheduled.empty')}</p>
            ) : (
              <ul className="unscheduled-list">
                {planner.data.unscheduled.map((item) => (
                  <li key={item.choreId}>
                    <Link to={`/chores/${item.choreId}`}>{item.title}</Link>
                    <span className="unscheduled-list__reason">{item.reason}</span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      ) : null}

      <PlannerOccurrenceDialog
        occurrence={editing}
        members={members.data ?? []}
        onClose={() => setEditing(null)}
      />
    </>
  )
}
