import { useMemo, useState } from 'react'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { accountsQueryKey, listAccounts, resetAccountPassword, type TemporaryPassword, type UserAccount } from '../../api/accounts'
import { getMembers, membersQueryKey } from '../../api/household'
import { useSession } from '../../auth/session-context'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { EmptyState } from '../../components/EmptyState'
import { ErrorState } from '../../components/ErrorState'
import { PageHeader } from '../../components/PageHeader'
import { Spinner } from '../../components/Spinner'
import { useSetAccountActive } from '../../hooks/useAccountMutations'
import { useOnlineStatus } from '../../hooks/useOnlineStatus'
import { errorMessageKey } from '../../lib/errors'
import { AccountDialog } from './AccountDialog'
import { TemporaryPasswordPanel } from './TemporaryPasswordPanel'

const pageSize = 25

export function AccountsSettingsPage() {
  const { t } = useTranslation()
  const { user } = useSession()
  const isAdmin = user?.roles.includes('Admin') ?? false
  const isOnline = useOnlineStatus()

  const accounts = useInfiniteQuery({
    queryKey: accountsQueryKey,
    queryFn: ({ pageParam, signal }) => listAccounts(pageParam, pageSize, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    enabled: isAdmin,
  })

  const members = useQuery({
    queryKey: membersQueryKey,
    queryFn: ({ signal }) => getMembers(signal),
    enabled: isAdmin,
  })

  const setActive = useSetAccountActive()

  const [editing, setEditing] = useState<{ open: boolean; account: UserAccount | null }>({ open: false, account: null })
  const [temporaryPassword, setTemporaryPassword] = useState<TemporaryPassword | null>(null)
  const [pendingToggle, setPendingToggle] = useState<UserAccount | null>(null)
  const [pendingReset, setPendingReset] = useState<UserAccount | null>(null)
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const memberNames = useMemo(
    () => new Map((members.data ?? []).map((member) => [member.id, member.name])),
    [members.data],
  )

  const rows = useMemo(() => accounts.data?.pages.flatMap((page) => page.items) ?? [], [accounts.data])

  if (!isAdmin) {
    return <ErrorState error={new Error('forbidden')} />
  }

  const handleToggleActive = async () => {
    if (pendingToggle === null) {
      return
    }

    setErrorKey(null)
    try {
      await setActive.mutateAsync({ userId: pendingToggle.id, isActive: !pendingToggle.isActive })
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    } finally {
      setPendingToggle(null)
    }
  }

  const handleResetPassword = async () => {
    if (pendingReset === null) {
      return
    }

    setErrorKey(null)
    try {
      setTemporaryPassword(await resetAccountPassword(pendingReset.id))
      await accounts.refetch()
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    } finally {
      setPendingReset(null)
    }
  }

  return (
    <>
      <PageHeader
        title={t('settings.accounts.title')}
        subtitle={t('settings.accounts.subtitle')}
        actions={
          <button
            type="button"
            className="button button--primary"
            onClick={() => setEditing({ open: true, account: null })}
            disabled={!isOnline}
          >
            {t('settings.accounts.add')}
          </button>
        }
      />

      {temporaryPassword ? (
        <TemporaryPasswordPanel value={temporaryPassword} onDismiss={() => setTemporaryPassword(null)} />
      ) : null}

      {errorKey ? (
        <p className="notice notice--error" role="alert">
          {t(errorKey)}
        </p>
      ) : null}

      {accounts.isPending ? <Spinner label={t('app.loading')} /> : null}
      {accounts.error ? <ErrorState error={accounts.error} onRetry={() => void accounts.refetch()} /> : null}

      {accounts.data && rows.length === 0 ? <EmptyState message={t('settings.accounts.empty')} /> : null}

      {rows.length > 0 ? (
        <ul className="settings-list">
          {rows.map((account) => (
            <li className="settings-row" key={account.id}>
              <div className="settings-row__main">
                <span className="settings-row__title">
                  {account.displayName ?? account.email}
                  {account.id === user?.id ? <span className="badge">{t('settings.accounts.you')}</span> : null}
                </span>
                <span className="settings-row__meta">{account.email}</span>
                <span className="settings-row__meta">
                  {account.isActive ? t('settings.accounts.active') : t('settings.accounts.inactive')}
                  {account.roles.includes('Admin') ? ` · ${t('settings.accounts.administrator')}` : ''}
                  {account.householdMemberId
                    ? ` · ${memberNames.get(account.householdMemberId) ?? t('common.unknown')}`
                    : ''}
                  {account.mustChangePassword ? ` · ${t('settings.accounts.mustChange')}` : ''}
                </span>
              </div>

              <div className="settings-row__actions">
                <button
                  type="button"
                  className="button"
                  onClick={() => setEditing({ open: true, account })}
                  disabled={!isOnline}
                >
                  {t('chores.actions.edit')}
                </button>
                <button
                  type="button"
                  className="button"
                  onClick={() => setPendingReset(account)}
                  disabled={!isOnline}
                >
                  {t('settings.accounts.resetPassword')}
                </button>
                <button
                  type="button"
                  className={account.isActive ? 'button button--danger' : 'button'}
                  onClick={() => setPendingToggle(account)}
                  disabled={!isOnline || setActive.isPending}
                >
                  {account.isActive ? t('settings.accounts.deactivate') : t('settings.accounts.reactivate')}
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}

      {accounts.hasNextPage ? (
        <button
          type="button"
          className="button"
          onClick={() => {
            void accounts.fetchNextPage()
          }}
          disabled={accounts.isFetchingNextPage}
        >
          {t('settings.accounts.loadMore')}
        </button>
      ) : null}

      <AccountDialog
        account={editing.account}
        open={editing.open}
        onClose={() => setEditing({ open: false, account: null })}
        onCreated={setTemporaryPassword}
      />

      <ConfirmDialog
        open={pendingToggle !== null}
        title={
          pendingToggle?.isActive
            ? t('settings.accounts.deactivate')
            : t('settings.accounts.reactivate')
        }
        body={<p>{pendingToggle?.displayName ?? pendingToggle?.email}</p>}
        confirmLabel={pendingToggle?.isActive ? t('settings.accounts.deactivate') : t('settings.accounts.reactivate')}
        busyLabel={t('common.saving')}
        confirmTone={pendingToggle?.isActive ? 'danger' : 'primary'}
        busy={setActive.isPending}
        onConfirm={() => {
          void handleToggleActive()
        }}
        onCancel={() => setPendingToggle(null)}
      />

      <ConfirmDialog
        open={pendingReset !== null}
        title={t('settings.accounts.resetPassword')}
        body={
          <div className="stack">
            <p>{pendingReset?.displayName ?? pendingReset?.email}</p>
            <p className="field__hint">{t('settings.accounts.temporaryPasswordHint')}</p>
          </div>
        }
        confirmLabel={t('settings.accounts.resetPassword')}
        confirmTone="primary"
        busyLabel={t('common.saving')}
        onConfirm={() => {
          void handleResetPassword()
        }}
        onCancel={() => setPendingReset(null)}
      />
    </>
  )
}
