import { describe, expect, it } from 'vitest'
import {
  buildImportRows,
  detectColumnMapping,
  distinctImportNames,
  parseImportAmountToMinor,
  parseImportDate,
} from './budget-import'
import { parseCsv } from './csv'

describe('detectColumnMapping', () => {
  it('recognizes the headers of the exported budget template', () => {
    const mapping = detectColumnMapping([
      'Date',
      'Category',
      'SubCategory',
      'Description',
      'Value',
      'Paid By',
      'Planned',
    ])

    expect(mapping).toEqual({
      date: 0,
      amount: 4,
      category: 1,
      subcategory: 2,
      description: 3,
      paidBy: 5,
    })
  })

  it('matches Italian headers as well', () => {
    const mapping = detectColumnMapping(['Data', 'Categoria', 'Sottocategoria', 'Descrizione', 'Importo', 'Pagato da'])

    expect(mapping.date).toBe(0)
    expect(mapping.category).toBe(1)
    expect(mapping.subcategory).toBe(2)
    expect(mapping.description).toBe(3)
    expect(mapping.amount).toBe(4)
    expect(mapping.paidBy).toBe(5)
  })
})

describe('parseImportDate', () => {
  it('normalizes day-first and ISO dates', () => {
    expect(parseImportDate('03/02/2026')).toBe('2026-02-03')
    expect(parseImportDate('3.2.2026')).toBe('2026-02-03')
    expect(parseImportDate('2026-02-03T10:00:00Z')).toBe('2026-02-03')
    expect(parseImportDate('2026/02/03')).toBe('2026-02-03')
    expect(parseImportDate('03/02/26')).toBe('2026-02-03')
  })

  it('rejects impossible dates', () => {
    expect(parseImportDate('31/02/2026')).toBeNull()
    expect(parseImportDate('not a date')).toBeNull()
    expect(parseImportDate('')).toBeNull()
  })
})

describe('parseImportAmountToMinor', () => {
  it('reads European and international amount formats', () => {
    expect(parseImportAmountToMinor('€ 7,99')).toBe(799)
    expect(parseImportAmountToMinor('7.99')).toBe(799)
    expect(parseImportAmountToMinor('1.234,56')).toBe(123456)
    expect(parseImportAmountToMinor('1,234.56')).toBe(123456)
    expect(parseImportAmountToMinor('1.234')).toBe(123400)
    expect(parseImportAmountToMinor('550,00')).toBe(55000)
  })

  it('rejects malformed or negative amounts', () => {
    expect(parseImportAmountToMinor('12,3,4')).toBeNull()
    expect(parseImportAmountToMinor('-5')).toBeNull()
    expect(parseImportAmountToMinor('abc')).toBeNull()
    expect(parseImportAmountToMinor('0.123')).toBeNull()
    expect(parseImportAmountToMinor('')).toBeNull()
  })
})

describe('buildImportRows', () => {
  it('normalizes mapped cells and reports bad values per line', () => {
    const csv = parseCsv(
      'Date,Category,SubCategory,Description,Value,Paid By\n03/02/2026,Spesa,Cibo,Carrefour,"€ 96,19",Ceci\nbad,,,No amount,abc,Fra\n',
    )
    const rows = buildImportRows(csv, detectColumnMapping(csv.headers))

    expect(rows[0]).toMatchObject({
      line: 2,
      date: '2026-02-03',
      amountMinor: 9619,
      category: 'Spesa',
      subcategory: 'Cibo',
      description: 'Carrefour',
      paidBy: 'Ceci',
      issues: [],
    })
    expect(rows[1]?.issues).toEqual(['date', 'amount', 'category'])
  })

  it('collects distinct names for the mapping screens', () => {
    const rows = buildImportRows(
      parseCsv('Date,Category,Value,Paid By\n03/02/2026,Spesa,7,Fra\n04/02/2026,spesa,8,Fra\n05/02/2026,Casa,9,Ceci\n'),
      detectColumnMapping(['Date', 'Category', 'Value', 'Paid By']),
    )

    expect(distinctImportNames(rows)).toEqual({
      payers: ['Fra', 'Ceci'],
      categories: ['Spesa', 'Casa'],
      subcategories: [],
    })
  })
})
