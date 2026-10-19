import { useMemo, useState, type ChangeEvent } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getHousehold, getMembers, getRooms, householdQueryKey, membersQueryKey, roomsQueryKey } from '../api/household'
import type { ChoreImportResult } from '../api/chores'
import { useSession } from '../auth/session-context'
import { ErrorState } from '../components/ErrorState'
import { PageHeader } from '../components/PageHeader'
import { Spinner } from '../components/Spinner'
import { useImportChores } from '../hooks/useChoreMutations'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import {
  distinctChoreAssignees,
  distinctChoreRooms,
  parseChoreImport,
  type ChoreImportRowDraft,
} from '../lib/chore-import'
import { parseCsv } from '../lib/csv'
import { errorMessageKey } from '../lib/errors'
import { recurrenceLabel } from '../lib/recurrence'

const CREATE = '__create__'
const NO_ROOM = '__none__'
const MAX_FILE_BYTES = 5 * 1024 * 1024

const nameKey = (value: string) => value.trim().toLocaleLowerCase()

/**
 * Administrator-only import of chore definitions and their execution history. The browser
 * parses and validates the file, then the API validates the whole batch again before writing.
 * Executions are recorded like Home Assistant callbacks, without a member or account.
 */
export function ChoresImportPage() {
  const { t } = useTranslation()
  const { user } = useSession()
  const isOnline = useOnlineStatus()
  const isAdmin = user?.roles.includes('Admin') ?? false

  const household = useQuery({ queryKey: householdQueryKey, queryFn: ({ signal }) => getHousehold(signal) })
  const rooms = useQuery({ queryKey: roomsQueryKey, queryFn: ({ signal }) => getRooms(signal) })
  const members = useQuery({ queryKey: membersQueryKey, queryFn: ({ signal }) => getMembers(signal) })
  const importChores = useImportChores()

  const [fileName, setFileName] = useState<string | null>(null)
  const [rows, setRows] = useState<ChoreImportRowDraft[] | null>(null)
  const [missing, setMissing] = useState<string[]>([])
  const [fileError, setFileError] = useState<string | null>(null)
  const [roomOverrides, setRoomOverrides] = useState<Record<string, string>>({})
  const [assigneeOverrides, setAssigneeOverrides] = useState<Record<string, string>>({})
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [result, setResult] = useState<ChoreImportResult | null>(null)

  const roomNames = useMemo(() => distinctChoreRooms(rows ?? []), [rows])
  const assigneeNames = useMemo(() => distinctChoreAssignees(rows ?? []), [rows])

  const roomResolutions = useMemo(() => {
    const defaults: Record<string, string> = {}
    for (const room of roomNames) {
      const match = (rooms.data ?? []).find((item) => nameKey(item.name) === nameKey(room))
      defaults[nameKey(room)] = match?.id ?? CREATE
    }
    return { ...defaults, ...roomOverrides }
  }, [roomNames, rooms.data, roomOverrides])

  const assigneeResolutions = useMemo(() => {
    const defaults: Record<string, string> = {}
    for (const assignee of assigneeNames) {
      const match = (members.data ?? []).find((member) => nameKey(member.name) === nameKey(assignee))
      defaults[nameKey(assignee)] = match?.id ?? ''
    }
    return { ...defaults, ...assigneeOverrides }
  }, [assigneeNames, members.data, assigneeOverrides])

  const invalidRows = (rows ?? []).filter((row) => row.issues.length > 0)
  const unresolvedAssignees = (rows ?? []).filter(
    (row) =>
      row.assignmentPolicy === 'SpecificMember' &&
      row.fixedAssignee !== '' &&
      !assigneeResolutions[nameKey(row.fixedAssignee)],
  )
  const dataReady = !household.isPending && !rooms.isPending && !members.isPending && !household.error && !rooms.error && !members.error
  const canImport =
    isOnline &&
    dataReady &&
    rows !== null &&
    rows.length > 0 &&
    missing.length === 0 &&
    invalidRows.length === 0 &&
    unresolvedAssignees.length === 0 &&
    !importChores.isPending

  const handleFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) {
      return
    }

    setFileError(null)
    setSubmitError(null)
    setResult(null)
    setRoomOverrides({})
    setAssigneeOverrides({})

    if (file.size > MAX_FILE_BYTES) {
      setFileError(t('chores.import.fileTooLarge'))
      return
    }

    try {
      const parsed = parseCsv(await file.text())
      if (parsed.headers.length === 0 || parsed.rows.length === 0) {
        setFileError(t('chores.import.emptyFile'))
        return
      }

      const preview = parseChoreImport(parsed)
      setFileName(file.name)
      setRows(preview.rows)
      setMissing(preview.missing)
    } catch {
      setFileError(t('chores.import.unreadableFile'))
    }
  }

  const handleImport = async () => {
    if (!canImport || rows === null) {
      return
    }

    setSubmitError(null)
    setResult(null)

    try {
      const response = await importChores.mutateAsync({
        rooms: roomNames.map((room) => {
          const value = roomResolutions[nameKey(room)] ?? CREATE
          if (value === CREATE) {
            return { sourceName: room, roomId: null, create: true }
          }
          if (value === NO_ROOM) {
            return { sourceName: room, roomId: null, create: false }
          }
          return { sourceName: room, roomId: value, create: false }
        }),
        chores: rows.map((row) => ({
          line: row.line,
          sourceId: row.sourceId,
          title: row.title,
          description: row.description === '' ? null : row.description,
          room: row.room === '' ? null : row.room,
          estimatedMinutes: row.estimatedMinutes!,
          isActive: row.isActive,
          effortWeight: row.effortWeight,
          preferSameRoomGrouping: row.preferSameRoomGrouping,
          frequency: row.frequency,
          interval: row.interval,
          mode: row.mode,
          anchorDate: row.anchorDate,
          assignmentPolicy: row.assignmentPolicy,
          fixedAssigneeMemberId:
            row.assignmentPolicy === 'SpecificMember'
              ? (assigneeResolutions[nameKey(row.fixedAssignee)] ?? null)
              : null,
          syntheticCompletionDate: row.completions.length === 0 ? row.syntheticCompletionDate : null,
          completions: row.completions,
        })),
      })

      setResult(response)
      setRoomOverrides({})
      setAssigneeOverrides({})
    } catch (error) {
      setSubmitError(errorMessageKey(error))
    }
  }

  if (!isAdmin) {
    return (
      <>
        <PageHeader title={t('chores.import.title')} subtitle={t('chores.import.subtitle')} />
        <p className="notice notice--error" role="alert">
          {t('errors.forbidden')}
        </p>
      </>
    )
  }

  return (
    <>
      <PageHeader
        title={t('chores.import.title')}
        subtitle={t('chores.import.subtitle')}
        actions={
          <Link className="button" to="/chores">
            {t('chores.import.backToChores')}
          </Link>
        }
      />

      <section className="card stack">
        <h2 className="form-section__title">{t('chores.import.chooseFile')}</h2>
        <p className="field__hint">{t('chores.import.fileHint')}</p>
        <input
          className="input"
          type="file"
          accept=".csv,text/csv"
          aria-label={t('chores.import.chooseFile')}
          onChange={(event) => {
            void handleFile(event)
          }}
        />
        {fileName ? <p className="field__hint">{fileName}</p> : null}
        {fileError ? (
          <p className="notice notice--error" role="alert">
            {fileError}
          </p>
        ) : null}
      </section>

      {rows && missing.length > 0 ? (
        <p className="notice notice--warning" role="status">
          {t('chores.import.missingColumns', {
            fields: missing.map((field) => t(`chores.import.field.${field}`)).join(', '),
          })}
        </p>
      ) : null}

      {rows && missing.length === 0 ? (
        <>
          {roomNames.length > 0 ? (
            <section className="card stack">
              <h2 className="form-section__title">{t('chores.import.mapRooms')}</h2>
              <p className="field__hint">{t('chores.import.mapRoomsHint')}</p>
              <div className="import-columns">
                {roomNames.map((room) => (
                  <label className="field" key={room}>
                    <span className="field__label">{room}</span>
                    <select
                      className="input"
                      value={roomResolutions[nameKey(room)] ?? CREATE}
                      onChange={(event) =>
                        setRoomOverrides((current) => ({ ...current, [nameKey(room)]: event.target.value }))
                      }
                    >
                      <option value={CREATE}>{t('chores.import.createRoom')}</option>
                      <option value={NO_ROOM}>{t('chores.import.noRoom')}</option>
                      {(rooms.data ?? []).map((item) => (
                        <option key={item.id} value={item.id}>
                          {item.name}
                        </option>
                      ))}
                    </select>
                  </label>
                ))}
              </div>
            </section>
          ) : null}

          {assigneeNames.length > 0 ? (
            <section className="card stack">
              <h2 className="form-section__title">{t('chores.import.mapAssignees')}</h2>
              <p className="field__hint">{t('chores.import.mapAssigneesHint')}</p>
              <div className="import-columns">
                {assigneeNames.map((assignee) => (
                  <label className="field" key={assignee}>
                    <span className="field__label">{assignee}</span>
                    <select
                      className="input"
                      value={assigneeResolutions[nameKey(assignee)] ?? ''}
                      onChange={(event) =>
                        setAssigneeOverrides((current) => ({ ...current, [nameKey(assignee)]: event.target.value }))
                      }
                    >
                      <option value="">{t('chores.import.noAssignee')}</option>
                      {(members.data ?? []).map((member) => (
                        <option key={member.id} value={member.id}>
                          {member.name}
                        </option>
                      ))}
                    </select>
                  </label>
                ))}
              </div>

              {unresolvedAssignees.length > 0 ? (
                <p className="field__error" role="alert">
                  {t('chores.import.unmatchedAssignees', {
                    names: [...new Set(unresolvedAssignees.map((row) => row.fixedAssignee))].join(', '),
                  })}
                </p>
              ) : null}
            </section>
          ) : null}

          <section className="card stack">
            <h2 className="form-section__title">{t('chores.import.preview')}</h2>
            <p className="field__hint">
              {t('chores.import.previewCount', { count: rows.length, invalid: invalidRows.length })}
            </p>

            <ul className="settings-list import-preview">
              {rows.map((row) => {
                const label = recurrenceLabel(row.frequency, row.interval)
                return (
                  <li className={`import-row${row.issues.length > 0 ? ' is-invalid' : ''}`} key={row.line}>
                    <span className="import-row__line">{t('chores.import.line', { line: row.line })}</span>
                    <span className="import-row__main">
                      {row.title || '—'} · {row.room || t('chores.import.noRoom')} ·{' '}
                      {t(label.key, label.options)} · {row.estimatedMinutes ?? '—'} min
                    </span>
                    <span className="import-row__payer">
                      {row.completions.length > 0
                        ? t('chores.import.completions', { count: row.completions.length })
                        : row.syntheticCompletionDate
                          ? t('chores.import.synthetic', { date: row.syntheticCompletionDate })
                          : t('chores.import.completions', { count: 0 })}
                    </span>
                    {row.issues.length > 0 ? (
                      <span className="import-row__error">
                        {row.issues.map((issue) => t(`chores.import.validation.${issue}`)).join(' ')}
                      </span>
                    ) : null}
                  </li>
                )
              })}
            </ul>

            {invalidRows.length > 0 ? (
              <p className="notice notice--error" role="alert">
                {t('chores.import.invalidRows', { count: invalidRows.length })}
              </p>
            ) : null}

            {submitError ? (
              <p className="notice notice--error" role="alert">
                {t(submitError)}
              </p>
            ) : null}

            {result ? (
              <>
                <p className="notice" role="status">
                  {t('chores.import.done', {
                    imported: result.imported,
                    completions: result.completionsImported,
                    skipped: result.skipped,
                  })}
                </p>
                {result.recalculationRunId ? (
                  <p className="field__hint">{t('chores.import.recalculationStarted')}</p>
                ) : null}
              </>
            ) : null}

            <div className="import-actions">
              <button
                type="button"
                className="button button--primary"
                disabled={!canImport}
                onClick={() => {
                  void handleImport()
                }}
              >
                {importChores.isPending
                  ? t('chores.import.importing')
                  : t('chores.import.submit', { count: rows.length })}
              </button>
            </div>
          </section>
        </>
      ) : null}

      {!dataReady ? <Spinner label={t('app.loading')} /> : null}
      {household.error ? <ErrorState error={household.error} onRetry={() => void household.refetch()} /> : null}
      {rooms.error ? <ErrorState error={rooms.error} onRetry={() => void rooms.refetch()} /> : null}
      {members.error ? <ErrorState error={members.error} onRetry={() => void members.refetch()} /> : null}
    </>
  )
}
