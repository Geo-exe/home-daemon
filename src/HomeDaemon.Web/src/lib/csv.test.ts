import { describe, expect, it } from 'vitest'
import { detectDelimiter, parseCsv } from './csv'

describe('parseCsv', () => {
  it('parses the exported budget template with quoted amounts', () => {
    const parsed = parseCsv(
      'Date,Category,SubCategory,Description,Value,Paid By,Planned\n03/02/2026,Intrattenimento,Streaming,NowTV,"€ 7,99",Fra,FALSE\n',
    )

    expect(parsed.headers).toEqual(['Date', 'Category', 'SubCategory', 'Description', 'Value', 'Paid By', 'Planned'])
    expect(parsed.rows).toHaveLength(1)
    expect(parsed.rows[0]).toEqual(['03/02/2026', 'Intrattenimento', 'Streaming', 'NowTV', '€ 7,99', 'Fra', 'FALSE'])
  })

  it('supports semicolon files, a BOM, escaped quotes and embedded newlines', () => {
    const parsed = parseCsv('\uFEFF"Date";"Note"\r\n"03/02/2026";"He said ""hi""\nand left"')

    expect(parsed.delimiter).toBe(';')
    expect(parsed.headers).toEqual(['Date', 'Note'])
    expect(parsed.rows).toEqual([['03/02/2026', 'He said "hi"\nand left']])
  })

  it('ignores blank trailing lines', () => {
    const parsed = parseCsv('a,b\n1,2\n,\n')

    expect(parsed.rows).toEqual([['1', '2']])
  })

  it('detects tab-separated exports', () => {
    expect(detectDelimiter('Date\tAmount\n03/02/2026\t7,99')).toBe('\t')
  })
})
