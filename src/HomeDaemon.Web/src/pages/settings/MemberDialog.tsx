import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { HouseholdMember } from '../../api/types'
import { FormDialog } from '../../components/FormDialog'
import { Field } from '../../components/Field'
import { useCreateMember, useUpdateMember } from '../../hooks/useHouseholdMutations'
import { useOnlineStatus } from '../../hooks/useOnlineStatus'
import { errorMessageKey, fieldProblems } from '../../lib/errors'
import { issuesToFieldErrors, memberFormSchema, splitIdentifierList, type MemberFormValues } from '../../lib/validation'

interface MemberDialogProps {
  /** Null creates a person; a member edits them. */
  member: HouseholdMember | null
  open: boolean
  onClose: () => void
}

export function MemberDialog({ member, open, onClose }: MemberDialogProps) {
  if (!open) {
    return null
  }

  return <MemberForm key={member?.id ?? 'new'} member={member} onClose={onClose} />
}

function MemberForm({ member, onClose }: { member: HouseholdMember | null; onClose: () => void }) {
  const { t } = useTranslation()
  const isOnline = useOnlineStatus()
  const create = useCreateMember()
  const update = useUpdateMember()

  const [values, setValues] = useState<MemberFormValues>({
    name: member?.name ?? '',
    color: member?.color ?? '',
    isAssignable: member?.isAssignable ?? true,
    isBudgetParticipant: member?.isBudgetParticipant ?? false,
    todoListEntityId: member?.todoListEntityId ?? '',
    personEntityId: member?.personEntityId ?? '',
    calendarEntityIds: (member?.calendarEntityIds ?? []).join('\n'),
  })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [errorKey, setErrorKey] = useState<string | null>(null)

  const busy = create.isPending || update.isPending
  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)
  const setField = <K extends keyof MemberFormValues>(key: K, value: MemberFormValues[K]) =>
    setValues((current) => ({ ...current, [key]: value }))

  const handleSubmit = async () => {
    setErrorKey(null)

    const parsed = memberFormSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setFieldErrors({})

    const model = {
      name: parsed.data.name,
      color: parsed.data.color === '' ? null : parsed.data.color,
      isAssignable: parsed.data.isAssignable,
      isBudgetParticipant: parsed.data.isBudgetParticipant,
      todoListEntityId: parsed.data.todoListEntityId === '' ? null : parsed.data.todoListEntityId,
      personEntityId: parsed.data.personEntityId === '' ? null : parsed.data.personEntityId,
      calendarEntityIds: splitIdentifierList(parsed.data.calendarEntityIds),
    }

    try {
      if (member) {
        await update.mutateAsync({ memberId: member.id, model })
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
      title={member ? t('settings.members.editTitle') : t('settings.members.newTitle')}
      submitLabel={t('common.save')}
      busy={busy}
      errorKey={errorKey}
      onCancel={onClose}
      onSubmit={() => {
        void handleSubmit()
      }}
    >
      <div className="stack">
        <Field label={t('settings.members.name')} htmlFor="member-name" error={errorFor('name')}>
          <input
            id="member-name"
            className="input"
            type="text"
            required
            maxLength={200}
            value={values.name}
            onChange={(event) => setField('name', event.target.value)}
          />
        </Field>

        <Field
          label={t('settings.members.color')}
          htmlFor="member-color"
          hint={t('settings.members.colorHint')}
          error={errorFor('color')}
        >
          <input
            id="member-color"
            className="input"
            type="text"
            placeholder="#4F46E5"
            spellCheck={false}
            value={values.color}
            onChange={(event) => setField('color', event.target.value)}
          />
        </Field>

        <label className="checkbox">
          <input
            type="checkbox"
            checked={values.isAssignable}
            onChange={(event) => setField('isAssignable', event.target.checked)}
          />
          <span>{t('settings.members.assignable')}</span>
        </label>

        <label className="checkbox">
          <input
            type="checkbox"
            checked={values.isBudgetParticipant}
            onChange={(event) => setField('isBudgetParticipant', event.target.checked)}
          />
          <span>{t('settings.members.budgetParticipant')}</span>
        </label>

        <Field
          label={t('settings.members.todoList')}
          htmlFor="member-todo-list"
          hint={t('settings.members.todoListHint')}
          error={errorFor('todoListEntityId')}
        >
          <input
            id="member-todo-list"
            className="input"
            type="text"
            spellCheck={false}
            placeholder="todo.alice"
            value={values.todoListEntityId}
            onChange={(event) => setField('todoListEntityId', event.target.value)}
          />
        </Field>

        <Field label={t('settings.members.personEntity')} htmlFor="member-person" error={errorFor('personEntityId')}>
          <input
            id="member-person"
            className="input"
            type="text"
            spellCheck={false}
            placeholder="person.alice"
            value={values.personEntityId}
            onChange={(event) => setField('personEntityId', event.target.value)}
          />
        </Field>

        <Field
          label={t('settings.members.calendars')}
          htmlFor="member-calendars"
          hint={t('settings.members.calendarsHint')}
          error={errorFor('calendarEntityIds')}
        >
          <textarea
            id="member-calendars"
            className="input input--textarea"
            rows={3}
            spellCheck={false}
            value={values.calendarEntityIds}
            onChange={(event) => setField('calendarEntityIds', event.target.value)}
          />
        </Field>

        {!isOnline ? <p className="notice notice--warning">{t('app.offline')}</p> : null}
      </div>
    </FormDialog>
  )
}
