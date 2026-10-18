import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { choresQueryKey, listChores } from '../api/chores'
import { getHousehold, getMembers, getRooms, householdQueryKey, membersQueryKey, roomsQueryKey } from '../api/household'
import { ChoreCard } from '../components/ChoreCard'
import { EmptyState } from '../components/EmptyState'
import { ErrorState } from '../components/ErrorState'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'

type StateFilter = 'all' | 'active' | 'paused'

export function ChoresListPage() {
  const { t } = useTranslation()

  const [search, setSearch] = useState('')
  const [stateFilter, setStateFilter] = useState<StateFilter>('all')
  const [roomFilter, setRoomFilter] = useState('')

  const household = useQuery({ queryKey: householdQueryKey, queryFn: ({ signal }) => getHousehold(signal) })
  const rooms = useQuery({ queryKey: roomsQueryKey, queryFn: ({ signal }) => getRooms(signal) })
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })

  const isActive = stateFilter === 'all' ? undefined : stateFilter === 'active'
  const chores = useQuery({
    queryKey: [...choresQueryKey, 'list', { stateFilter, roomFilter }],
    queryFn: ({ signal }) => listChores({ roomId: roomFilter === '' ? undefined : roomFilter, isActive }, signal),
  })

  const roomNames = useMemo(
    () => new Map((rooms.data ?? []).map((room) => [room.id, room.name])),
    [rooms.data],
  )
  const memberNames = useMemo(
    () => new Map((members.data ?? []).map((member) => [member.id, member.name])),
    [members.data],
  )

  const visible = useMemo(() => {
    const needle = search.trim().toLowerCase()
    const items = chores.data ?? []
    return needle === '' ? items : items.filter((chore) => chore.title.toLowerCase().includes(needle))
  }, [chores.data, search])

  const hasFilters = search.trim() !== '' || stateFilter !== 'all' || roomFilter !== ''
  const isPending = chores.isPending || household.isPending || rooms.isPending
  const failure = chores.error ?? household.error ?? rooms.error ?? members.error

  const refresh = () => {
    void chores.refetch()
    void household.refetch()
    void rooms.refetch()
    void members.refetch()
  }

  return (
    <>
      <PageHeader
        title={t('chores.title')}
        subtitle={t('chores.subtitle', { count: visible.length })}
        actions={
          <Link className="button button--primary" to="/chores/new">
            {t('chores.new')}
          </Link>
        }
      />

      <div className="filters">
        <div className="filters__search">
          <label className="visually-hidden" htmlFor="chore-search">
            {t('chores.searchLabel')}
          </label>
          <input
            id="chore-search"
            className="input"
            type="search"
            inputMode="search"
            placeholder={t('chores.searchPlaceholder')}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>

        <div className="filters__group">
          <label className="visually-hidden" htmlFor="chore-state">
            {t('chores.field.active')}
          </label>
          <select
            id="chore-state"
            className="input"
            value={stateFilter}
            onChange={(event) => setStateFilter(event.target.value as StateFilter)}
          >
            <option value="all">{t('chores.filter.all')}</option>
            <option value="active">{t('chores.filter.active')}</option>
            <option value="paused">{t('chores.filter.paused')}</option>
          </select>

          <label className="visually-hidden" htmlFor="chore-room">
            {t('chores.field.room')}
          </label>
          <select id="chore-room" className="input" value={roomFilter} onChange={(event) => setRoomFilter(event.target.value)}>
            <option value="">{t('chores.filter.allRooms')}</option>
            {(rooms.data ?? []).map((room) => (
              <option key={room.id} value={room.id}>
                {room.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {isPending ? <Spinner label={t('app.loading')} /> : null}

      {!isPending && failure ? <ErrorState error={failure} onRetry={refresh} /> : null}

      {!isPending && !failure && visible.length === 0 ? (
        <EmptyState
          message={hasFilters ? t('chores.emptyFiltered') : t('chores.empty')}
          action={
            hasFilters ? null : (
              <Link className="button button--primary" to="/chores/new">
                {t('chores.new')}
              </Link>
            )
          }
        />
      ) : null}

      {!isPending && !failure && visible.length > 0 ? (
        <ul className="chore-list">
          {visible.map((chore) => (
            <ChoreCard
              key={chore.id}
              chore={chore}
              roomName={chore.roomId ? (roomNames.get(chore.roomId) ?? null) : null}
              memberName={chore.plannedMemberId ? (memberNames.get(chore.plannedMemberId) ?? null) : null}
              timeZoneId={household.data?.timeZoneId ?? 'UTC'}
            />
          ))}
        </ul>
      ) : null}
    </>
  )
}
