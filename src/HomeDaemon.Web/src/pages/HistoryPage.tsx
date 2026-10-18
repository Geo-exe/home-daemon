import { useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useVirtualizer } from '@tanstack/react-virtual'
import { useTranslation } from 'react-i18next'
import { choresQueryKey, listChores } from '../api/chores'
import { historyQueryKey, listChoreHistory, type ChoreHistoryFilters } from '../api/history'
import { getMembers, membersQueryKey } from '../api/household'
import type { ChoreHistoryItem, CompletionSource } from '../api/types'
import { EmptyState } from '../components/EmptyState'
import { ErrorState } from '../components/ErrorState'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { formatCalendarDate, formatDateTime } from '../lib/format'

const pageSize = 50
const sourceOptions: CompletionSource[] = ['Manual', 'HomeAssistant', 'Api']

export function HistoryPage() {
  const { t, i18n } = useTranslation()

  const [filters, setFilters] = useState<ChoreHistoryFilters>({})

  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })
  const chores = useQuery({
    queryKey: [...choresQueryKey, 'list', 'history-filter'],
    queryFn: ({ signal }) => listChores({}, signal),
  })

  const history = useInfiniteQuery({
    queryKey: historyQueryKey(filters),
    queryFn: ({ pageParam, signal }) => listChoreHistory(filters, pageParam, pageSize, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
  })

  const items = useMemo(() => history.data?.pages.flatMap((page) => page.items) ?? [], [history.data])

  const scrollRef = useRef<HTMLDivElement>(null)
  // TanStack Virtual exposes imperative helpers the React Compiler cannot memoize; the
  // component simply uses the virtualizer's output directly.
  // eslint-disable-next-line react-hooks/incompatible-library
  const virtualizer = useVirtualizer({
    count: items.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => 72,
    overscan: 10,
  })

  const { fetchNextPage, hasNextPage, isFetchingNextPage } = history
  const lastIndex = virtualizer.getVirtualItems().at(-1)?.index ?? 0

  // Fetch the next page as soon as the last rendered row comes into view.
  useEffect(() => {
    if (lastIndex >= items.length - 1 && hasNextPage && !isFetchingNextPage) {
      void fetchNextPage()
    }
  }, [lastIndex, items.length, hasNextPage, isFetchingNextPage, fetchNextPage])

  const hasFilters = Object.values(filters).some((value) => value !== undefined && value !== '')

  const setFilter = <K extends keyof ChoreHistoryFilters>(key: K, value: string) =>
    setFilters((current) => {
      const next = { ...current }
      if (value === '') {
        delete next[key]
      } else {
        next[key] = value as ChoreHistoryFilters[K]
      }

      return next
    })

  return (
    <>
      <PageHeader title={t('history.title')} subtitle={t('history.subtitle')} />

      <div className="filters filters--wrap">
        <div className="filters__group">
          <label className="visually-hidden" htmlFor="history-chore">
            {t('history.filters.chore')}
          </label>
          <select
            id="history-chore"
            className="input"
            value={filters.choreId ?? ''}
            onChange={(event) => setFilter('choreId', event.target.value)}
          >
            <option value="">{t('history.filters.allChores')}</option>
            {(chores.data ?? []).map((chore) => (
              <option key={chore.id} value={chore.id}>
                {chore.title}
              </option>
            ))}
          </select>

          <label className="visually-hidden" htmlFor="history-member">
            {t('history.filters.member')}
          </label>
          <select
            id="history-member"
            className="input"
            value={filters.memberId ?? ''}
            onChange={(event) => setFilter('memberId', event.target.value)}
          >
            <option value="">{t('history.filters.allMembers')}</option>
            {(members.data ?? []).map((member) => (
              <option key={member.id} value={member.id}>
                {member.name}
              </option>
            ))}
          </select>

          <label className="visually-hidden" htmlFor="history-source">
            {t('history.filters.source')}
          </label>
          <select
            id="history-source"
            className="input"
            value={filters.source ?? ''}
            onChange={(event) => setFilter('source', event.target.value)}
          >
            <option value="">{t('history.filters.allSources')}</option>
            {sourceOptions.map((source) => (
              <option key={source} value={source}>
                {t(`enums.source.${source}`)}
              </option>
            ))}
          </select>
        </div>

        <div className="filters__group">
          <label className="visually-hidden" htmlFor="history-from">
            {t('history.filters.from')}
          </label>
          <input
            id="history-from"
            className="input"
            type="date"
            value={filters.from ?? ''}
            onChange={(event) => setFilter('from', event.target.value)}
          />

          <label className="visually-hidden" htmlFor="history-to">
            {t('history.filters.to')}
          </label>
          <input
            id="history-to"
            className="input"
            type="date"
            value={filters.to ?? ''}
            onChange={(event) => setFilter('to', event.target.value)}
          />
        </div>

        {hasFilters ? (
          <button type="button" className="button button--ghost" onClick={() => setFilters({})}>
            {t('history.filters.clear')}
          </button>
        ) : null}
      </div>

      {history.isPending ? <Spinner label={t('app.loading')} /> : null}
      {history.error ? <ErrorState error={history.error} onRetry={() => void history.refetch()} /> : null}

      {!history.isPending && !history.error && items.length === 0 ? (
        <EmptyState message={t('history.empty')} />
      ) : null}

      {items.length > 0 ? (
        <>
          {/* Only the visible rows exist in the DOM, so a long history stays responsive. */}
          <div className="virtual-scroll" ref={scrollRef}>
            <div className="virtual-scroll__inner" style={{ height: `${virtualizer.getTotalSize()}px` }}>
              {virtualizer.getVirtualItems().map((virtualRow) => {
                const item = items[virtualRow.index]
                if (item === undefined) {
                  return null
                }

                return (
                  <div
                    key={item.id}
                    ref={virtualizer.measureElement}
                    data-index={virtualRow.index}
                    className="virtual-scroll__row"
                    style={{ transform: `translateY(${virtualRow.start}px)` }}
                  >
                    <HistoryRow item={item} locale={i18n.language} linkToChore />
                  </div>
                )
              })}
            </div>
          </div>

          <p className="history-footer" role="status">
            {isFetchingNextPage ? t('history.loadingMore') : hasNextPage ? '' : t('history.end')}
          </p>
        </>
      ) : null}
    </>
  )
}

function HistoryRow({ item, locale, linkToChore }: { item: ChoreHistoryItem; locale: string; linkToChore?: boolean }) {
  const { t } = useTranslation()

  return (
    <article className="history-row">
      <div className="history-row__main">
        <h2 className="history-row__title">
          {linkToChore ? <Link to={`/chores/${item.choreDefinitionId}`}>{item.choreTitle}</Link> : item.choreTitle}
        </h2>
        <p className="history-row__meta">
          {t(`enums.source.${item.source}`)}
          {' · '}
          {item.completedByMemberName ?? t('history.unattributed')}
        </p>
      </div>
      <div className="history-row__when">
        <span className="history-row__date">{formatCalendarDate(item.completedDate, locale)}</span>
        <span className="history-row__recorded">{t('history.recordedAt', { moment: formatDateTime(item.recordedAtUtc, locale) })}</span>
      </div>
    </article>
  )
}
