import { useMemo, useState } from 'react'
import { useInfiniteQuery } from '@tanstack/react-query'
import type { TFunction } from 'i18next'
import { useTranslation } from 'react-i18next'
import {
  homeAssistantLogsQueryKey,
  listHomeAssistantCallLogs,
  type HomeAssistantLogFilters,
} from '../../api/homeAssistantLogs'
import type { HomeAssistantCallLog, HomeAssistantCallStatus } from '../../api/types'
import { useSession } from '../../auth/session-context'
import { EmptyState } from '../../components/EmptyState'
import { ErrorState } from '../../components/ErrorState'
import { PageHeader } from '../../components/PageHeader'
import { Spinner } from '../../components/Spinner'
import { formatDateTime } from '../../lib/format'

const pageSize = 50

/**
 * Administrator-only diagnostic view of the calls HomeDaemon made to Home Assistant. It exists
 * so a failing integration can be understood without access to the Home Assistant host.
 */
export function HomeAssistantLogsSettingsPage() {
  const { t, i18n } = useTranslation()
  const { user } = useSession()
  const isAdmin = user?.roles.includes('Admin') ?? false

  const [status, setStatus] = useState<HomeAssistantCallStatus | ''>('')
  const [operation, setOperation] = useState('')
  const [entityId, setEntityId] = useState('')

  const filters = useMemo<HomeAssistantLogFilters>(
    () => ({
      status: status === '' ? undefined : status,
      operation: operation.trim() === '' ? undefined : operation.trim(),
      entityId: entityId.trim() === '' ? undefined : entityId.trim(),
    }),
    [status, operation, entityId],
  )

  const logs = useInfiniteQuery({
    queryKey: homeAssistantLogsQueryKey(filters),
    queryFn: ({ pageParam, signal }) => listHomeAssistantCallLogs(filters, pageParam, pageSize, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    enabled: isAdmin,
  })

  const rows = useMemo(() => logs.data?.pages.flatMap((page) => page.items) ?? [], [logs.data])

  if (!isAdmin) {
    return <ErrorState error={new Error('forbidden')} />
  }

  return (
    <>
      <PageHeader
        title={t('settings.homeAssistantLogs.title')}
        subtitle={t('settings.homeAssistantLogs.subtitle')}
        actions={
          <button
            type="button"
            className="button"
            onClick={() => {
              void logs.refetch()
            }}
            disabled={logs.isFetching}
          >
            {logs.isFetching ? t('app.loading') : t('common.refresh')}
          </button>
        }
      />

      <div className="filters filters--wrap">
        <div className="filters__group">
          <label className="visually-hidden" htmlFor="ha-log-status">
            {t('settings.homeAssistantLogs.status')}
          </label>
          <select
            id="ha-log-status"
            className="input"
            value={status}
            onChange={(event) => setStatus(event.target.value as HomeAssistantCallStatus | '')}
          >
            <option value="">{t('settings.homeAssistantLogs.anyStatus')}</option>
            <option value="Failed">{t('settings.homeAssistantLogs.failed')}</option>
            <option value="Succeeded">{t('settings.homeAssistantLogs.succeeded')}</option>
          </select>

          <label className="visually-hidden" htmlFor="ha-log-operation">
            {t('settings.homeAssistantLogs.operation')}
          </label>
          <input
            id="ha-log-operation"
            className="input"
            type="search"
            autoComplete="off"
            placeholder={t('settings.homeAssistantLogs.operation')}
            value={operation}
            onChange={(event) => setOperation(event.target.value)}
          />

          <label className="visually-hidden" htmlFor="ha-log-entity">
            {t('settings.homeAssistantLogs.entity')}
          </label>
          <input
            id="ha-log-entity"
            className="input"
            type="search"
            autoComplete="off"
            placeholder={t('settings.homeAssistantLogs.entity')}
            value={entityId}
            onChange={(event) => setEntityId(event.target.value)}
          />
        </div>
      </div>

      {logs.isPending ? <Spinner label={t('app.loading')} /> : null}
      {logs.error ? <ErrorState error={logs.error} onRetry={() => void logs.refetch()} /> : null}

      {logs.data && rows.length === 0 ? <EmptyState message={t('settings.homeAssistantLogs.empty')} /> : null}

      {rows.length > 0 ? (
        <ul className="settings-list">
          {rows.map((log) => (
            <li className="settings-row" key={log.id}>
              <div className="settings-row__main">
                <span className="settings-row__title">
                  <span
                    className={`badge ${log.status === 'Failed' ? 'badge--failed' : 'badge--succeeded'}`}
                  >
                    {log.status === 'Failed'
                      ? t('settings.homeAssistantLogs.failed')
                      : t('settings.homeAssistantLogs.succeeded')}
                  </span>
                  {log.operation}
                  {log.entityId ? ` · ${log.entityId}` : ''}
                </span>
                <span className="settings-row__meta">{logMeta(log, i18n.language, t)}</span>
                {log.errorMessage ? <span className="ha-log__error">{log.errorMessage}</span> : null}
              </div>
            </li>
          ))}
        </ul>
      ) : null}

      {logs.hasNextPage ? (
        <button
          type="button"
          className="button"
          onClick={() => {
            void logs.fetchNextPage()
          }}
          disabled={logs.isFetchingNextPage}
        >
          {logs.isFetchingNextPage ? t('app.loading') : t('common.loadMore')}
        </button>
      ) : null}
    </>
  )
}

function logMeta(log: HomeAssistantCallLog, locale: string, t: TFunction): string {
  const parts = [
    formatDateTime(log.occurredAtUtc, locale),
    log.method,
    t('settings.homeAssistantLogs.duration', { milliseconds: log.durationMs }),
  ]

  if (log.statusCode !== null) {
    parts.push(t('settings.homeAssistantLogs.statusCode', { code: log.statusCode }))
  }

  if (log.isTransient === true) {
    parts.push(t('settings.homeAssistantLogs.transient'))
  }

  return parts.join(' · ')
}
