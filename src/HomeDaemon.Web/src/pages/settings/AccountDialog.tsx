import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { createAccount, updateAccount, type AccountWriteModel, type UserAccount } from '../../api/accounts'
import { getMembers, membersQueryKey } from '../../api/household'
import { FormDialog } from '../../components/FormDialog'
import { Field } from '../../components/Field'
import { useOnlineStatus } from '../../hooks/useOnlineStatus'
import { errorMessageKey, fieldProblems } from '../../lib/errors'
import { accountFormSchema, issuesToFieldErrors, type AccountFormValues } from '../../lib/validation'

interface AccountDialogProps {
  /** Null creates an account; an account edits it. */
  account: UserAccount | null
  open: boolean
  onClose: () => void
  /** Called with the one-time password returned when a new account is created. */
  onCreated: (value: Awaited<ReturnType<typeof createAccount>>) => void
}

export function AccountDialog({ account, open, onClose, onCreated }: AccountDialogProps) {
  if (!open) {
    return null
  }

  return <AccountForm key={account?.id ?? 'new'} account={account} onClose={onClose} onCreated={onCreated} />
}

function AccountForm({
  account,
  onClose,
  onCreated,
}: {
  account: UserAccount | null
  onClose: () => void
  onCreated: AccountDialogProps['onCreated']
}) {
  const { t } = useTranslation()
  const isOnline = useOnlineStatus()
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })

  const [values, setValues] = useState<AccountFormValues>({
    email: account?.email ?? '',
    displayName: account?.displayName ?? '',
    householdMemberId: account?.householdMemberId ?? '',
    isAdmin: account?.roles.includes('Admin') ?? false,
  })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [errorKey, setErrorKey] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)
  const setField = <K extends keyof AccountFormValues>(key: K, value: AccountFormValues[K]) =>
    setValues((current) => ({ ...current, [key]: value }))

  const handleSubmit = async () => {
    setErrorKey(null)

    const parsed = accountFormSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setFieldErrors({})
    setBusy(true)

    const model: AccountWriteModel = {
      email: parsed.data.email,
      displayName: parsed.data.displayName === '' ? null : parsed.data.displayName,
      householdMemberId: parsed.data.householdMemberId === '' ? null : parsed.data.householdMemberId,
      isAdmin: parsed.data.isAdmin,
    }

    try {
      if (account) {
        await updateAccount(account.id, model)
      } else {
        onCreated(await createAccount(model))
      }

      onClose()
    } catch (error) {
      const problems = fieldProblems(error)
      if (Object.keys(problems).length > 0) {
        setFieldErrors(
          Object.fromEntries(Object.entries(problems).map(([field, messages]) => [field, messages[0] ?? 'errors.generic'])),
        )
      } else {
        setErrorKey(errorMessageKey(error))
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormDialog
      open
      title={account ? t('settings.accounts.editTitle') : t('settings.accounts.newTitle')}
      submitLabel={t('common.save')}
      busy={busy}
      errorKey={errorKey}
      onCancel={onClose}
      onSubmit={() => {
        void handleSubmit()
      }}
    >
      <div className="stack">
        <Field label={t('settings.accounts.email')} htmlFor="account-email" error={errorFor('email')}>
          <input
            id="account-email"
            className="input"
            type="email"
            inputMode="email"
            autoComplete="off"
            required
            maxLength={256}
            value={values.email}
            onChange={(event) => setField('email', event.target.value)}
          />
        </Field>

        <Field label={t('settings.accounts.displayName')} htmlFor="account-name" error={errorFor('displayName')}>
          <input
            id="account-name"
            className="input"
            type="text"
            maxLength={200}
            value={values.displayName}
            onChange={(event) => setField('displayName', event.target.value)}
          />
        </Field>

        <Field label={t('settings.accounts.linkedMember')} htmlFor="account-member">
          <select
            id="account-member"
            className="input"
            value={values.householdMemberId}
            onChange={(event) => setField('householdMemberId', event.target.value)}
          >
            <option value="">{t('settings.accounts.noLink')}</option>
            {(members.data ?? []).map((member) => (
              <option key={member.id} value={member.id}>
                {member.name}
              </option>
            ))}
          </select>
        </Field>

        <label className="checkbox">
          <input type="checkbox" checked={values.isAdmin} onChange={(event) => setField('isAdmin', event.target.checked)} />
          <span>{t('settings.accounts.administrator')}</span>
        </label>

        {!isOnline ? <p className="notice notice--warning">{t('app.offline')}</p> : null}
      </div>
    </FormDialog>
  )
}
