import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getMembers, membersQueryKey } from '../../api/household'
import type { HouseholdMember } from '../../api/types'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState } from '../../components/EmptyState'
import { ErrorState } from '../../components/ErrorState'
import { PageHeader } from '../../components/PageHeader'
import { Spinner } from '../../components/Spinner'
import { useDeleteMember } from '../../hooks/useHouseholdMutations'
import { useOnlineStatus } from '../../hooks/useOnlineStatus'
import { errorMessageKey } from '../../lib/errors'
import { MemberDialog } from './MemberDialog'

export function MembersSettingsPage() {
  const { t } = useTranslation()
  const isOnline = useOnlineStatus()
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })
  const remove = useDeleteMember()

  const [editing, setEditing] = useState<{ open: boolean; member: HouseholdMember | null }>({ open: false, member: null })
  const [pendingDelete, setPendingDelete] = useState<HouseholdMember | null>(null)
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const handleDelete = async () => {
    if (pendingDelete === null) {
      return
    }

    setErrorKey(null)
    try {
      await remove.mutateAsync(pendingDelete.id)
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    } finally {
      setPendingDelete(null)
    }
  }

  return (
    <>
      <PageHeader
        title={t('settings.members.title')}
        subtitle={t('settings.members.subtitle')}
        actions={
          <button
            type="button"
            className="button button--primary"
            onClick={() => setEditing({ open: true, member: null })}
            disabled={!isOnline}
          >
            {t('settings.members.add')}
          </button>
        }
      />

      {errorKey ? (
        <p className="notice notice--error" role="alert">
          {t(errorKey)}
        </p>
      ) : null}

      {members.isPending ? <Spinner label={t('app.loading')} /> : null}
      {members.error ? <ErrorState error={members.error} onRetry={() => void members.refetch()} /> : null}

      {members.data && members.data.length === 0 ? <EmptyState message={t('settings.members.empty')} /> : null}

      {members.data && members.data.length > 0 ? (
        <ul className="settings-list">
          {members.data.map((member) => (
            <li className="settings-row" key={member.id}>
              <div className="settings-row__main">
                <span className="settings-row__title">
                  {member.color ? (
                    <span className="swatch" style={{ backgroundColor: member.color }} aria-hidden="true" />
                  ) : null}
                  {member.name}
                </span>
                <span className="settings-row__meta">
                  {member.isAssignable ? t('settings.members.assignable') : t('settings.members.notAssignable')}
                  {member.isBudgetParticipant ? ` · ${t('settings.members.budgetParticipantShort')}` : ''}
                  {member.todoListEntityId ? ` · ${member.todoListEntityId}` : ''}
                  {member.calendarEntityIds.length > 0 ? ` · ${member.calendarEntityIds.length}` : ''}
                </span>
              </div>
              <div className="settings-row__actions">
                <button
                  type="button"
                  className="button"
                  onClick={() => setEditing({ open: true, member })}
                  disabled={!isOnline}
                >
                  {t('chores.actions.edit')}
                </button>
                <button
                  type="button"
                  className="button button--danger"
                  onClick={() => setPendingDelete(member)}
                  disabled={!isOnline}
                >
                  {t('settings.members.delete')}
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}

      <MemberDialog
        member={editing.member}
        open={editing.open}
        onClose={() => setEditing({ open: false, member: null })}
      />

      <ConfirmDialog
        open={pendingDelete !== null}
        title={t('settings.members.deleteTitle', { name: pendingDelete?.name ?? '' })}
        body={<p>{t('settings.members.deleteBody')}</p>}
        confirmLabel={t('settings.members.delete')}
        busy={remove.isPending}
        onConfirm={() => {
          void handleDelete()
        }}
        onCancel={() => setPendingDelete(null)}
      />
    </>
  )
}
