import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getRooms, roomsQueryKey } from '../../api/household'
import type { Room } from '../../api/types'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState } from '../../components/EmptyState'
import { ErrorState } from '../../components/ErrorState'
import { PageHeader } from '../../components/PageHeader'
import { Spinner } from '../../components/Spinner'
import { useDeleteRoom } from '../../hooks/useHouseholdMutations'
import { useOnlineStatus } from '../../hooks/useOnlineStatus'
import { errorMessageKey } from '../../lib/errors'
import { RoomDialog } from './RoomDialog'

export function RoomsSettingsPage() {
  const { t } = useTranslation()
  const isOnline = useOnlineStatus()
  const rooms = useQuery({ queryKey: roomsQueryKey, queryFn: ({ signal }) => getRooms(signal) })
  const remove = useDeleteRoom()

  const [editing, setEditing] = useState<{ open: boolean; room: Room | null }>({ open: false, room: null })
  const [pendingDelete, setPendingDelete] = useState<Room | null>(null)
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const handleDelete = async () => {
    if (pendingDelete === null) {
      return
    }

    setErrorKey(null)
    try {
      await remove.mutateAsync(pendingDelete.id)
      setPendingDelete(null)
    } catch (error) {
      setErrorKey(errorMessageKey(error))
      setPendingDelete(null)
    }
  }

  return (
    <>
      <PageHeader
        title={t('settings.rooms.title')}
        subtitle={t('settings.rooms.subtitle')}
        actions={
          <button
            type="button"
            className="button button--primary"
            onClick={() => setEditing({ open: true, room: null })}
            disabled={!isOnline}
          >
            {t('settings.rooms.add')}
          </button>
        }
      />

      {errorKey ? (
        <p className="notice notice--error" role="alert">
          {t(errorKey)}
        </p>
      ) : null}

      {rooms.isPending ? <Spinner label={t('app.loading')} /> : null}
      {rooms.error ? <ErrorState error={rooms.error} onRetry={() => void rooms.refetch()} /> : null}

      {rooms.data && rooms.data.length === 0 ? <EmptyState message={t('settings.rooms.empty')} /> : null}

      {rooms.data && rooms.data.length > 0 ? (
        <ul className="settings-list">
          {rooms.data.map((room) => (
            <li className="settings-row" key={room.id}>
              <div className="settings-row__main">
                <span className="settings-row__title">{room.name}</span>
                <span className="settings-row__meta">
                  {t('settings.rooms.sortOrder')}: {room.sortOrder}
                </span>
              </div>
              <div className="settings-row__actions">
                <button type="button" className="button" onClick={() => setEditing({ open: true, room })} disabled={!isOnline}>
                  {t('chores.actions.edit')}
                </button>
                <button
                  type="button"
                  className="button button--danger"
                  onClick={() => setPendingDelete(room)}
                  disabled={!isOnline}
                >
                  {t('settings.rooms.delete')}
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}

      <RoomDialog room={editing.room} open={editing.open} onClose={() => setEditing({ open: false, room: null })} />

      <ConfirmDialog
        open={pendingDelete !== null}
        title={t('settings.rooms.deleteTitle', { name: pendingDelete?.name ?? '' })}
        body={<p>{t('settings.rooms.deleteBody')}</p>}
        confirmLabel={t('settings.rooms.delete')}
        busy={remove.isPending}
        onConfirm={() => {
          void handleDelete()
        }}
        onCancel={() => setPendingDelete(null)}
      />
    </>
  )
}
