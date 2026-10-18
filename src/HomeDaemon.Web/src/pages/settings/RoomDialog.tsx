import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { Room } from '../../api/types'
import { FormDialog } from '../../components/FormDialog'
import { Field } from '../../components/Field'
import { useCreateRoom, useUpdateRoom } from '../../hooks/useHouseholdMutations'
import { useOnlineStatus } from '../../hooks/useOnlineStatus'
import { errorMessageKey, fieldProblems } from '../../lib/errors'
import { issuesToFieldErrors, roomFormSchema, type RoomFormValues } from '../../lib/validation'

interface RoomDialogProps {
  /** Null creates a room; a room edits it. Closed when both are null/undefined. */
  room: Room | null
  open: boolean
  onClose: () => void
}

export function RoomDialog({ room, open, onClose }: RoomDialogProps) {
  if (!open) {
    return null
  }

  return <RoomForm key={room?.id ?? 'new'} room={room} onClose={onClose} />
}

function RoomForm({ room, onClose }: { room: Room | null; onClose: () => void }) {
  const { t } = useTranslation()
  const isOnline = useOnlineStatus()
  const create = useCreateRoom()
  const update = useUpdateRoom()

  const [values, setValues] = useState<RoomFormValues>({
    name: room?.name ?? '',
    sortOrder: String(room?.sortOrder ?? 0),
  })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const busy = create.isPending || update.isPending
  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)

  const handleSubmit = async () => {
    setErrorKey(null)

    const parsed = roomFormSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setFieldErrors({})
    const model = { name: parsed.data.name, sortOrder: parsed.data.sortOrder }

    try {
      if (room) {
        await update.mutateAsync({ roomId: room.id, model })
      } else {
        await create.mutateAsync(model)
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
    }
  }

  return (
    <FormDialog
      open
      title={room ? t('settings.rooms.editTitle') : t('settings.rooms.newTitle')}
      submitLabel={t('common.save')}
      busy={busy}
      errorKey={errorKey}
      onCancel={onClose}
      onSubmit={() => {
        void handleSubmit()
      }}
    >
      <div className="stack">
        <Field label={t('settings.rooms.name')} htmlFor="room-name" error={errorFor('name')}>
          <input
            id="room-name"
            className="input"
            type="text"
            required
            maxLength={200}
            value={values.name}
            onChange={(event) => setValues((current) => ({ ...current, name: event.target.value }))}
          />
        </Field>

        <Field label={t('settings.rooms.sortOrder')} htmlFor="room-order" error={errorFor('sortOrder')}>
          <input
            id="room-order"
            className="input"
            type="number"
            inputMode="numeric"
            min={0}
            max={9999}
            value={values.sortOrder}
            onChange={(event) => setValues((current) => ({ ...current, sortOrder: event.target.value }))}
          />
        </Field>

        {!isOnline ? <p className="notice notice--warning">{t('app.offline')}</p> : null}
      </div>
    </FormDialog>
  )
}
