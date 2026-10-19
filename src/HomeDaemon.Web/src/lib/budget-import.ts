/**
 * Column mapping and value parsing for the budget CSV importer.
 *
 * The importer is deliberately agnostic about the file layout: the user maps a column to every
 * field, and the defaults below only try to save a few taps. Dates are normalized to ISO and
 * amounts to integer minor units before anything reaches the API.
 */

import type { ParsedCsv } from './csv'

export type ImportField = 'date' | 'amount' | 'category' | 'subcategory' | 'description' | 'paidBy'

export const importFields: readonly ImportField[] = [
  'date',
  'amount',
  'category',
  'subcategory',
  'description',
  'paidBy',
]

/** Required fields must be mapped before the preview can be built. */
export const requiredImportFields: readonly ImportField[] = ['date', 'amount', 'category', 'paidBy']

export type ColumnMapping = Record<ImportField, number | null>

const aliases: Record<ImportField, string[]> = {
  date: ['date', 'data', 'giorno', 'day'],
  amount: ['amount', 'value', 'importo', 'valore', 'totale', 'total', 'price', 'costo', 'spesa'],
  category: ['category', 'categoria', 'categoria spesa', 'expense category'],
  subcategory: ['subcategory', 'sottocategoria', 'subcategoria', 'sub category'],
  description: ['description', 'descrizione', 'note', 'notes', 'dettaglio', 'dettagli', 'memo'],
  paidBy: ['paid by', 'paidby', 'pagato da', 'pagante', 'payer', 'person', 'persona', 'membro'],
}

export function emptyColumnMapping(): ColumnMapping {
  return {
    date: null,
    amount: null,
    category: null,
    subcategory: null,
    description: null,
    paidBy: null,
  }
}

/** Best-effort mapping from the header labels; every choice can still be changed in the UI. */
export function detectColumnMapping(headers: readonly string[]): ColumnMapping {
  const normalized = headers.map(normalizeHeader)
  const mapping = emptyColumnMapping()
  const used = new Set<number>()

  for (const field of importFields) {
    const exact = normalized.findIndex(
      (header, index) => !used.has(index) && aliases[field].some((alias) => header === alias),
    )

    if (exact >= 0) {
      mapping[field] = exact
      used.add(exact)
    }
  }

  for (const field of importFields) {
    if (mapping[field] !== null) {
      continue
    }

    const partial = normalized.findIndex(
      (header, index) =>
        !used.has(index) &&
        aliases[field].some((alias) => alias.length >= 4 && header.includes(alias)),
    )

    if (partial >= 0) {
      mapping[field] = partial
      used.add(partial)
    }
  }

  return mapping
}

function normalizeHeader(header: string): string {
  return header
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
}

/** Parses `dd/mm/yyyy` and `yyyy-mm-dd` (with an optional time part) into an ISO date. */
export function parseImportDate(value: string): string | null {
  const text = value.trim()
  if (text === '') {
    return null
  }

  const iso = /^(\d{4})-(\d{1,2})-(\d{1,2})(?:[T ].*)?$/.exec(text)
  if (iso) {
    return toIsoDate(Number(iso[1]), Number(iso[2]), Number(iso[3]))
  }

  const yearFirst = /^(\d{4})[/.](\d{1,2})[/.](\d{1,2})$/.exec(text)
  if (yearFirst) {
    return toIsoDate(Number(yearFirst[1]), Number(yearFirst[2]), Number(yearFirst[3]))
  }

  const dayFirst = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{2}|\d{4})$/.exec(text)
  if (dayFirst) {
    const yearText = dayFirst[3] ?? ''
    const year = yearText.length === 2 ? 2000 + Number(yearText) : Number(yearText)
    return toIsoDate(year, Number(dayFirst[2]), Number(dayFirst[1]))
  }

  return null
}

function toIsoDate(year: number, month: number, day: number): string | null {
  if (year < 1900 || year > 2200 || month < 1 || month > 12 || day < 1 || day > 31) {
    return null
  }

  const date = new Date(Date.UTC(year, month - 1, day))
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) {
    return null
  }

  return `${year.toString().padStart(4, '0')}-${month.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`
}

/**
 * Reads an amount written for humans: `7,99`, `7.99`, `€ 7,99`, `1.234,56` or `1,234.56`.
 * Returns null for anything that is not a non-negative number with at most two decimals.
 */
export function parseImportAmountToMinor(value: string): number | null {
  const text = value
    .trim()
    .replace(/[\s\u00a0]/g, '')
    .replace(/[€$£]/g, '')
    .replace(/'/g, '')

  if (text === '' || !/^[0-9.,]+$/.test(text)) {
    return null
  }

  const lastComma = text.lastIndexOf(',')
  const lastDot = text.lastIndexOf('.')
  let whole: string
  let fraction: string

  if (lastComma >= 0 && lastDot >= 0) {
    const decimalIndex = Math.max(lastComma, lastDot)
    whole = text.slice(0, decimalIndex).replace(/[.,]/g, '')
    fraction = text.slice(decimalIndex + 1)
  } else {
    const separatorIndex = Math.max(lastComma, lastDot)
    if (separatorIndex < 0) {
      whole = text
      fraction = ''
    } else {
      const separator = text[separatorIndex] ?? ''
      const occurrences = separator === '' ? 0 : text.split(separator).length - 1
      const after = text.slice(separatorIndex + 1)
      const before = text.slice(0, separatorIndex)

      if (occurrences > 1) {
        const groups = text.split(separator)
        const looksLikeThousands =
          (groups[0]?.length ?? 0) >= 1 && groups.slice(1).every((group) => group.length === 3)
        if (!looksLikeThousands) {
          return null
        }
        whole = groups.join('')
        fraction = ''
      } else if (after.length === 0) {
        whole = before
        fraction = ''
      } else if (after.length <= 2) {
        whole = before
        fraction = after
      } else if (after.length === 3 && before.length <= 3 && before !== '0') {
        whole = before + after
        fraction = ''
      } else {
        return null
      }
    }
  }

  if (!/^\d+$/.test(whole) || !/^\d{0,2}$/.test(fraction)) {
    return null
  }

  const minor = Number(whole) * 100 + Number((fraction + '00').slice(0, 2))
  return Number.isSafeInteger(minor) ? minor : null
}

export type ImportRowIssue = 'date' | 'amount' | 'category' | 'paidBy'

export interface ImportRowDraft {
  /** 1-based line in the original file, so the preview points at the right row. */
  line: number
  date: string | null
  amountMinor: number | null
  category: string
  subcategory: string
  description: string
  paidBy: string
  issues: ImportRowIssue[]
}

/** Turns the mapped cells into normalized rows without touching the network. */
export function buildImportRows(csv: ParsedCsv, mapping: ColumnMapping): ImportRowDraft[] {
  return csv.rows.map((cells, index) => {
    const read = (field: ImportField): string => {
      const column = mapping[field]
      return column === null ? '' : (cells[column] ?? '').trim()
    }

    const date = parseImportDate(read('date'))
    const amountMinor = parseImportAmountToMinor(read('amount'))
    const category = read('category')
    const paidBy = read('paidBy')
    const issues: ImportRowIssue[] = []

    if (date === null) {
      issues.push('date')
    }
    if (amountMinor === null || amountMinor <= 0) {
      issues.push('amount')
    }
    if (category === '') {
      issues.push('category')
    }
    if (paidBy === '') {
      issues.push('paidBy')
    }

    return {
      line: index + 2,
      date,
      amountMinor,
      category,
      subcategory: read('subcategory'),
      description: read('description'),
      paidBy,
      issues,
    }
  })
}

export interface DistinctNames {
  payers: string[]
  categories: string[]
  subcategories: Array<{ category: string; name: string }>
}

/** Distinct names in file order, compared case-insensitively and trimmed. */
export function distinctImportNames(rows: readonly ImportRowDraft[]): DistinctNames {
  const payers = new Map<string, string>()
  const categories = new Map<string, string>()
  const subcategories = new Map<string, { category: string; name: string }>()

  for (const row of rows) {
    const payerKey = row.paidBy.toLocaleLowerCase()
    if (!payers.has(payerKey)) {
      payers.set(payerKey, row.paidBy)
    }

    const categoryKey = row.category.toLocaleLowerCase()
    if (!categories.has(categoryKey)) {
      categories.set(categoryKey, row.category)
    }

    if (row.subcategory !== '') {
      const key = `${categoryKey}\u001f${row.subcategory.toLocaleLowerCase()}`
      if (!subcategories.has(key)) {
        subcategories.set(key, { category: row.category, name: row.subcategory })
      }
    }
  }

  return {
    payers: [...payers.values()],
    categories: [...categories.values()],
    subcategories: [...subcategories.values()],
  }
}
