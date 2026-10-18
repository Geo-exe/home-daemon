import { useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { choreQueryKey, choresQueryKey, createChore, getChore, updateChore } from '../api/chores'
import { getMembers, getRooms, membersQueryKey, roomsQueryKey } from '../api/household'
import type { ChoreDetail, ChoreWriteModel, HouseholdMember, Room } from '../api/types'
import { Field } from '../components/Field'
import { ErrorState } from '../components/ErrorState'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { describedBy } from '../lib/aria'
import { errorMessageKey, fieldProblems } from '../lib/errors'
import {
  assignmentPolicies,
  choreFormSchema,
  issuesToFieldErrors,
  recurrenceFrequencies,
  recurrenceModes,
  type ChoreFormValues,
} from '../lib/validation'

const emptyValues: ChoreFormValues = {
  title: '',
  description: '',
  roomId: '',
  isActive: true,
  estimatedMinutes: '30',
  effortWeight: '',
  preferSameRoomGrouping: false,
  frequency: 'Daily',
  interval: '1',
  mode: 'Dynamic',
  anchorDate: '',
  assignmentPolicy: 'Both',
  fixedAssigneeMemberId: '',
}

function toFormValues(chore: ChoreDetail): ChoreFormValues {
  return {
    title: chore.title,
    description: chore.description ?? '',
    roomId: chore.roomId ?? '',
    isActive: chore.isActive,
    estimatedMinutes: String(chore.estimatedMinutes),
    effortWeight: chore.effortWeight === null ? '' : String(chore.effortWeight),
    preferSameRoomGrouping: chore.preferSameRoomGrouping,
    frequency: chore.frequency,
    interval: String(chore.interval),
    mode: chore.mode,
    anchorDate: chore.anchorDate ?? '',
    assignmentPolicy: chore.assignmentPolicy,
    fixedAssigneeMemberId: chore.fixedAssigneeMemberId ?? '',
  }
}

/** The form is shared by creation and editing; the presence of an id decides which one it is. */
export function ChoreFormPage() {
  const { t } = useTranslation()
  const { choreId } = useParams()
  const isEditing = choreId !== undefined

  const rooms = useQuery({ queryKey: roomsQueryKey, queryFn: ({ signal }) => getRooms(signal) })
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })
  const chore = useQuery({
    queryKey: choreQueryKey(choreId ?? ''),
    queryFn: ({ signal }) => getChore(choreId!, signal),
    enabled: isEditing,
  })

  if (isEditing && chore.isPending) {
    return <Spinner label={t('app.loading')} />
  }

  if (isEditing && chore.error) {
    return <ErrorState error={chore.error} onRetry={() => void chore.refetch()} />
  }

  const initialValues = chore.data ? toFormValues(chore.data) : emptyValues

  return (
    // Keying on the chore id gives the editor fresh initial state without an effect.
    <ChoreFormEditor
      key={chore.data?.id ?? 'new'}
      choreId={choreId}
      initialValues={initialValues}
      rooms={rooms.data ?? []}
      members={members.data ?? []}
    />
  )
}

interface ChoreFormEditorProps {
  choreId?: string
  initialValues: ChoreFormValues
  rooms: Room[]
  members: HouseholdMember[]
}

function ChoreFormEditor({ choreId, initialValues, rooms, members }: ChoreFormEditorProps) {
  const { t } = useTranslation()
  const isEditing = choreId !== undefined
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const isOnline = useOnlineStatus()

  const [values, setValues] = useState<ChoreFormValues>(initialValues)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formErrorKey, setFormErrorKey] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const assignableMembers = useMemo(() => members.filter((member) => member.isAssignable), [members])

  const errorFor = (name: string): string | undefined => (fieldErrors[name] ? t(fieldErrors[name]!) : undefined)

  const update = <K extends keyof ChoreFormValues>(key: K, value: ChoreFormValues[K]) =>
    setValues((current) => ({ ...current, [key]: value }))

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFormErrorKey(null)

    const parsed = choreFormSchema.safeParse(values)
    if (!parsed.success) {
      setFieldErrors(issuesToFieldErrors(parsed.error))
      return
    }

    setFieldErrors({})
    setSubmitting(true)

    const data = parsed.data
    const model: ChoreWriteModel = {
      title: data.title,
      description: data.description === '' ? null : data.description,
      roomId: data.roomId === '' ? null : data.roomId,
      isActive: data.isActive,
      estimatedMinutes: data.estimatedMinutes,
      effortWeight: data.effortWeight,
      preferSameRoomGrouping: data.preferSameRoomGrouping,
      frequency: data.frequency,
      interval: data.interval,
      mode: data.mode,
      anchorDate: data.anchorDate === '' ? null : data.anchorDate,
      assignmentPolicy: data.assignmentPolicy,
      fixedAssigneeMemberId:
        data.assignmentPolicy === 'SpecificMember' && data.fixedAssigneeMemberId !== '' ? data.fixedAssigneeMemberId : null,
    }

    try {
      const saved = isEditing ? await updateChore(choreId, model) : await createChore(model)
      await queryClient.invalidateQueries({ queryKey: choresQueryKey })
      await navigate(`/chores/${saved.id}`, { replace: true })
    } catch (error) {
      const problems = fieldProblems(error)
      if (Object.keys(problems).length > 0) {
        setFieldErrors(
          Object.fromEntries(
            Object.entries(problems).map(([field, messages]) => [field, messages[0] ?? 'errors.generic']),
          ),
        )
      } else {
        setFormErrorKey(errorMessageKey(error))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>
      <PageHeader
        title={isEditing ? t('chores.detail.editTitle') : t('chores.detail.newTitle')}
        actions={
          <Link className="button" to={isEditing ? `/chores/${choreId}` : '/chores'}>
            {t('common.cancel')}
          </Link>
        }
      />

      {formErrorKey ? (
        <p className="notice notice--error" role="alert">
          {t(formErrorKey)}
        </p>
      ) : null}

      <form className="form form--grid" onSubmit={(event) => { void handleSubmit(event) }} noValidate>
        <div className="form__row form__row--wide">
          <Field label={t('chores.field.title')} htmlFor="chore-title" error={errorFor('title')}>
            <input
              id="chore-title"
              className="input"
              type="text"
              required
              maxLength={200}
              value={values.title}
              aria-describedby={describedBy('chore-title', undefined, errorFor('title'))}
              onChange={(event) => update('title', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row form__row--wide">
          <Field label={t('chores.field.description')} htmlFor="chore-description" error={errorFor('description')}>
            <textarea
              id="chore-description"
              className="input input--textarea"
              rows={3}
              maxLength={2000}
              value={values.description}
              onChange={(event) => update('description', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field label={t('chores.field.room')} htmlFor="chore-room">
            <select
              id="chore-room"
              className="input"
              value={values.roomId}
              onChange={(event) => update('roomId', event.target.value)}
            >
              <option value="">{t('chores.field.noRoom')}</option>
              {rooms.map((room) => (
                <option key={room.id} value={room.id}>
                  {room.name}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className="form__row">
          <Field label={t('chores.field.duration')} htmlFor="chore-duration" error={errorFor('estimatedMinutes')}>
            <input
              id="chore-duration"
              className="input"
              type="number"
              inputMode="numeric"
              min={1}
              max={1440}
              value={values.estimatedMinutes}
              aria-describedby={describedBy('chore-duration', undefined, errorFor('estimatedMinutes'))}
              onChange={(event) => update('estimatedMinutes', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field
            label={t('chores.field.effortWeight')}
            htmlFor="chore-effort"
            hint={t('chores.field.effortWeightHint')}
            error={errorFor('effortWeight')}
          >
            <input
              id="chore-effort"
              className="input"
              type="number"
              inputMode="decimal"
              min={0}
              step="0.5"
              value={values.effortWeight}
              aria-describedby={describedBy('chore-effort', t('chores.field.effortWeightHint'), errorFor('effortWeight'))}
              onChange={(event) => update('effortWeight', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field label={t('chores.field.frequency')} htmlFor="chore-frequency" error={errorFor('frequency')}>
            <select
              id="chore-frequency"
              className="input"
              value={values.frequency}
              onChange={(event) => update('frequency', event.target.value as ChoreFormValues['frequency'])}
            >
              {recurrenceFrequencies.map((frequency) => (
                <option key={frequency} value={frequency}>
                  {t(`enums.frequency.${frequency}`)}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className="form__row">
          <Field label={t('chores.field.interval')} htmlFor="chore-interval" error={errorFor('interval')}>
            <input
              id="chore-interval"
              className="input"
              type="number"
              inputMode="numeric"
              min={1}
              max={365}
              value={values.interval}
              aria-describedby={describedBy('chore-interval', undefined, errorFor('interval'))}
              onChange={(event) => update('interval', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field label={t('chores.field.mode')} htmlFor="chore-mode" error={errorFor('mode')}>
            <select
              id="chore-mode"
              className="input"
              value={values.mode}
              onChange={(event) => update('mode', event.target.value as ChoreFormValues['mode'])}
            >
              {recurrenceModes.map((mode) => (
                <option key={mode} value={mode}>
                  {t(`enums.mode.${mode}`)}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className="form__row">
          <Field
            label={t('chores.field.anchorDate')}
            htmlFor="chore-anchor"
            hint={t('chores.field.anchorDateHint')}
            error={errorFor('anchorDate')}
          >
            <input
              id="chore-anchor"
              className="input"
              type="date"
              value={values.anchorDate}
              aria-describedby={describedBy('chore-anchor', t('chores.field.anchorDateHint'), errorFor('anchorDate'))}
              onChange={(event) => update('anchorDate', event.target.value)}
            />
          </Field>
        </div>

        <div className="form__row">
          <Field label={t('chores.field.assignment')} htmlFor="chore-assignment" error={errorFor('assignmentPolicy')}>
            <select
              id="chore-assignment"
              className="input"
              value={values.assignmentPolicy}
              onChange={(event) => update('assignmentPolicy', event.target.value as ChoreFormValues['assignmentPolicy'])}
            >
              {assignmentPolicies.map((policy) => (
                <option key={policy} value={policy}>
                  {t(`enums.assignment.${policy}`)}
                </option>
              ))}
            </select>
          </Field>
        </div>

        {values.assignmentPolicy === 'SpecificMember' ? (
          <div className="form__row">
            <Field label={t('chores.field.assignee')} htmlFor="chore-assignee" error={errorFor('fixedAssigneeMemberId')}>
              <select
                id="chore-assignee"
                className="input"
                value={values.fixedAssigneeMemberId}
                onChange={(event) => update('fixedAssigneeMemberId', event.target.value)}
              >
                <option value="">—</option>
                {assignableMembers.map((member) => (
                  <option key={member.id} value={member.id}>
                    {member.name}
                  </option>
                ))}
              </select>
            </Field>
          </div>
        ) : null}

        <div className="form__row form__row--wide form__row--checks">
          <label className="checkbox">
            <input
              type="checkbox"
              checked={values.preferSameRoomGrouping}
              onChange={(event) => update('preferSameRoomGrouping', event.target.checked)}
            />
            <span>{t('chores.field.preferSameRoom')}</span>
          </label>

          <label className="checkbox">
            <input type="checkbox" checked={values.isActive} onChange={(event) => update('isActive', event.target.checked)} />
            <span>{t('chores.field.active')}</span>
          </label>
        </div>

        <div className="form__row form__row--wide">
          <button type="submit" className="button button--primary" disabled={submitting || !isOnline}>
            {submitting ? t('chores.actions.creating') : isEditing ? t('common.save') : t('chores.actions.create')}
          </button>
        </div>
      </form>
    </>
  )
}
