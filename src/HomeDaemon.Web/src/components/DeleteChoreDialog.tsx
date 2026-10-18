import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDeleteChore } from '../hooks/useChoreMutations'
import { errorMessageKey } from '../lib/errors'
import { ConfirmDialog } from './ConfirmDialog'

interface DeleteChoreDialogProps {
  open: boolean
  choreId: string
  choreTitle: string
  onClose: () => void
  onDeleted?: () => void
}

/**
 * Deletion is soft: the chore leaves the catalog and the plan while its completion history
 * stays. The body text says so, because the action looks destructive.
 */
export function DeleteChoreDialog({ open, choreId, choreTitle, onClose, onDeleted }: DeleteChoreDialogProps) {
  const { t } = useTranslation()
  const remove = useDeleteChore()
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const handleConfirm = async () => {
    setErrorKey(null)
    try {
      await remove.mutateAsync(choreId)
      onClose()
      onDeleted?.()
    } catch (error) {
      setErrorKey(errorMessageKey(error))
    }
  }

  return (
    <ConfirmDialog
      open={open}
      title={t('chores.delete.title', { title: choreTitle })}
      busy={remove.isPending}
      confirmLabel={t('chores.delete.confirm')}
      busyLabel={t('common.deleting')}
      onConfirm={() => {
        void handleConfirm()
      }}
      onCancel={onClose}
      body={
        <div className="stack">
          <p>{t('chores.delete.body')}</p>
          {errorKey ? <p className="notice notice--error" role="alert">{t(errorKey)}</p> : null}
        </div>
      }
    />
  )
}
