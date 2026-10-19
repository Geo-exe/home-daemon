/**
 * Parsing and normalization for the chore import file.
 *
 * The expected layout is the export produced for the Grocy migration, but headers are matched
 * by name with a few aliases so a hand-edited file still works. Values are validated in the
 * browser for a fast preview; the API validates the batch again before writing anything.
 */

import type { AssignmentPolicy, RecurrenceFrequency, RecurrenceMode } from '../api/types'
import { parseImportDate } from './budget-import'
import type { ParsedCsv } from './csv'

export type ChoreImportIssue = 'title' | 'minutes' | 'interval' | 'completion' | 'created' | 'assignee'

export interface ChoreImportRowDraft {
  /** 1-based line in the original file. */
  line: number
  sourceId: string
  title: string
  description: string
  room: string
  estimatedMinutes: number | null
  isActive: boolean
  effortWeight: number | null
  preferSameRoomGrouping: boolean
  frequency: RecurrenceFrequency
  interval: number
  mode: RecurrenceMode
  anchorDate: string | null
  assignmentPolicy: AssignmentPolicy
  fixedAssignee: string
  /** Filled only when the row has no executions: the source creation date. */
  syntheticCompletionDate: string | null
  completions: string[]
  issues: ChoreImportIssue[]
}

export interface ChoreImportParseResult {
  rows: ChoreImportRowDraft[]
  missing: string[]
}

const aliases = {
  sourceId: ['grocy chore id', 'source id', 'source', 'id'],
  title: ['title', 'name', 'titolo', 'nome'],
  description: ['description', 'descrizione', 'note'],
  room: ['room', 'stanza', 'room key'],
  estimatedMinutes: ['estimated minutes', 'minutes', 'duration', 'durata', 'minuti'],
  isActive: ['is active', 'active', 'attiva'],
  effortWeight: ['effort weight', 'peso'],
  preferSameRoomGrouping: ['prefer same room grouping', 'group with the same room'],
  frequency: ['frequency', 'frequenza', 'recurrence'],
  interval: ['interval', 'intervallo'],
  mode: ['mode', 'modalita'],
  anchorDate: ['anchor date', 'anchor'],
  assignmentPolicy: ['assignment policy', 'policy'],
  fixedAssignee: ['fixed assignee', 'assignee', 'assegnatario'],
  completions: ['completion history', 'completion dates', 'completions', 'storico'],
  createdAt: ['grocy created at', 'created at', 'created', 'created date'],
} as const

type Field = keyof typeof aliases

const requiredFields: readonly Field[] = ['title', 'estimatedMinutes']

export const choreImportRequiredFields = requiredFields

function normalizeHeader(header: string): string {
  return header
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
}

function findColumn(headers: readonly string[], field: Field): number {
  const normalized = headers.map(normalizeHeader)
  const names = aliases[field]

  for (const name of names) {
    const exact = normalized.findIndex((header) => header === name)
    if (exact >= 0) {
      return exact
    }
  }

  return normalized.findIndex((header) => names.some((name) => name.length >= 4 && header.includes(name)))
}

export function parseChoreImport(csv: ParsedCsv): ChoreImportParseResult {
  const columns = Object.fromEntries(
    (Object.keys(aliases) as Field[]).map((field) => [field, findColumn(csv.headers, field)]),
  ) as Record<Field, number>

  const missing = requiredFields.filter((field) => columns[field] < 0)
  if (missing.length > 0) {
    return { rows: [], missing }
  }

  const rows = csv.rows.map((cells, index) => buildRow(cells, columns, index + 2))
  return { rows, missing: [] }
}

function buildRow(cells: readonly string[], columns: Record<Field, number>, line: number): ChoreImportRowDraft {
  const read = (field: Field): string => {
    const column = columns[field]
    return column < 0 ? '' : (cells[column] ?? '').trim()
  }

  const issues: ChoreImportIssue[] = []
  const title = read('title')
  if (title === '') {
    issues.push('title')
  }

  const minutesText = read('estimatedMinutes')
  const estimatedMinutes = parsePositiveInteger(minutesText)
  if (estimatedMinutes === null) {
    issues.push('minutes')
  }

  const intervalText = read('interval')
  const interval = intervalText === '' ? 1 : parsePositiveInteger(intervalText)
  if (interval === null) {
    issues.push('interval')
  }

  const completions = parseCompletions(read('completions'))
  if (completions === null) {
    issues.push('completion')
  }

  const createdDate = parseImportDate(read('createdAt'))
  let syntheticCompletionDate: string | null = null
  if ((completions ?? []).length === 0) {
    if (createdDate === null) {
      issues.push('created')
    } else {
      syntheticCompletionDate = createdDate
    }
  }

  const assignmentPolicy = parseAssignmentPolicy(read('assignmentPolicy'), read('fixedAssignee'))
  if (assignmentPolicy === 'SpecificMember' && read('fixedAssignee') === '') {
    issues.push('assignee')
  }

  return {
    line,
    sourceId: read('sourceId') === '' ? String(line) : read('sourceId'),
    title,
    description: read('description'),
    room: read('room'),
    estimatedMinutes,
    isActive: parseBoolean(read('isActive'), true),
    effortWeight: parseOptionalNumber(read('effortWeight')),
    preferSameRoomGrouping: parseBoolean(read('preferSameRoomGrouping'), false),
    frequency: parseFrequency(read('frequency'), interval ?? 1),
    interval: interval ?? 1,
    mode: parseMode(read('mode')),
    anchorDate: parseImportDate(read('anchorDate')),
    assignmentPolicy,
    fixedAssignee: read('fixedAssignee'),
    syntheticCompletionDate,
    completions: completions ?? [],
    issues,
  }
}

function parsePositiveInteger(value: string): number | null {
  if (!/^\d+$/.test(value)) {
    return null
  }

  const parsed = Number(value)
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null
}

function parseOptionalNumber(value: string): number | null {
  if (value === '') {
    return null
  }

  const normalized = value.replace(',', '.')
  if (!/^\d+(\.\d+)?$/.test(normalized)) {
    return null
  }

  const parsed = Number(normalized)
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null
}

function parseBoolean(value: string, fallback: boolean): boolean {
  const normalized = value.trim().toLowerCase()
  if (['true', '1', 'yes', 'si', 'sì'].includes(normalized)) {
    return true
  }
  if (['false', '0', 'no'].includes(normalized)) {
    return false
  }
  return fallback
}

function parseFrequency(value: string, interval: number): RecurrenceFrequency {
  const normalized = value.trim().toLowerCase()
  if (normalized === 'daily') {
    return 'Daily'
  }
  if (normalized === 'weekly') {
    return 'Weekly'
  }
  if (normalized === 'monthly') {
    return 'Monthly'
  }
  if (normalized === 'yearly') {
    return 'Yearly'
  }
  if (normalized === 'fixedintervaldays' || normalized === 'fixed interval days') {
    return 'FixedIntervalDays'
  }

  return interval === 1 ? 'Daily' : 'FixedIntervalDays'
}

function parseMode(value: string): RecurrenceMode {
  return value.trim().toLowerCase() === 'anchored' ? 'Anchored' : 'Dynamic'
}

function parseAssignmentPolicy(value: string, assignee: string): AssignmentPolicy {
  const normalized = value.trim().toLowerCase()
  if (normalized === 'specificmember' || normalized === 'specific member') {
    return 'SpecificMember'
  }
  if (normalized === 'alternating') {
    return 'Alternating'
  }
  if (normalized === 'both') {
    return 'Both'
  }

  return assignee.trim() === '' ? 'Both' : 'SpecificMember'
}

/** Accepts `date|user;date|user` and a plain `date;date` list. */
function parseCompletions(value: string): string[] | null {
  if (value.trim() === '') {
    return []
  }

  const dates: string[] = []
  for (const entry of value.split(';')) {
    const raw = entry.split('|', 1)[0]?.trim() ?? ''
    if (raw === '') {
      continue
    }

    const date = parseImportDate(raw)
    if (date === null) {
      return null
    }

    if (!dates.includes(date)) {
      dates.push(date)
    }
  }

  return dates
}

export function distinctChoreRooms(rows: readonly ChoreImportRowDraft[]): string[] {
  return [...new Set(rows.map((row) => row.room).filter((room) => room !== ''))]
}

export function distinctChoreAssignees(rows: readonly ChoreImportRowDraft[]): string[] {
  return [...new Set(rows.map((row) => row.fixedAssignee).filter((name) => name !== ''))]
}
