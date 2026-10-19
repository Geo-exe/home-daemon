/**
 * Small, dependency-free CSV reader used by the budget importer.
 *
 * It supports the two conventions spreadsheets export most often: comma and semicolon
 * separators, quoted fields (including embedded separators, doubled quotes and newlines) and a
 * UTF-8 BOM. The first non-empty record is treated as the header row.
 */

export type CsvDelimiter = ',' | ';' | '\t'

export interface ParsedCsv {
  delimiter: CsvDelimiter
  headers: string[]
  rows: string[][]
}

export function detectDelimiter(text: string): CsvDelimiter {
  const firstLine = text.replace(/^\uFEFF/, '').split(/\r?\n/, 1)[0] ?? ''
  const candidates: CsvDelimiter[] = [',', ';', '\t']

  return candidates.reduce((best, candidate) => {
    const count = countOutsideQuotes(firstLine, candidate)
    const bestCount = countOutsideQuotes(firstLine, best)
    return count > bestCount ? candidate : best
  }, ',' as CsvDelimiter)
}

export function parseCsv(text: string): ParsedCsv {
  const input = text.replace(/^\uFEFF/, '')
  const delimiter = detectDelimiter(input)
  const records: string[][] = []
  let record: string[] = []
  let field = ''
  let inQuotes = false

  const pushField = () => {
    record.push(field)
    field = ''
  }

  const pushRecord = () => {
    pushField()
    // Ignore blank lines, a common trailing artefact of spreadsheet exports.
    if (record.some((value) => value.trim() !== '')) {
      records.push(record)
    }
    record = []
  }

  for (let index = 0; index < input.length; index += 1) {
    const character = input[index]

    if (inQuotes) {
      if (character === '"') {
        if (input[index + 1] === '"') {
          field += '"'
          index += 1
        } else {
          inQuotes = false
        }
      } else {
        field += character
      }
      continue
    }

    if (character === '"' && field === '') {
      inQuotes = true
      continue
    }

    if (character === delimiter) {
      pushField()
      continue
    }

    if (character === '\r') {
      if (input[index + 1] === '\n') {
        index += 1
      }
      pushRecord()
      continue
    }

    if (character === '\n') {
      pushRecord()
      continue
    }

    field += character
  }

  // A file without a trailing newline still has a final record.
  if (field !== '' || record.length > 0) {
    pushRecord()
  }

  const [headerRecord = [], ...dataRecords] = records
  return {
    delimiter,
    headers: headerRecord.map((header) => header.trim()),
    rows: dataRecords,
  }
}

function countOutsideQuotes(line: string, character: string): number {
  let inQuotes = false
  let count = 0

  for (let index = 0; index < line.length; index += 1) {
    if (line[index] === '"') {
      if (inQuotes && line[index + 1] === '"') {
        index += 1
      } else {
        inQuotes = !inQuotes
      }
      continue
    }

    if (!inQuotes && line[index] === character) {
      count += 1
    }
  }

  return count
}
