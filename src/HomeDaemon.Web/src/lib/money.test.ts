import { describe, expect, it } from 'vitest'
import { formatMinor, minorToInputValue, parseAmountToMinor } from './money'

describe('parseAmountToMinor', () => {
  it('parses whole and decimal amounts with either separator', () => {
    expect(parseAmountToMinor('12')).toBe(1200)
    expect(parseAmountToMinor('12.5')).toBe(1250)
    expect(parseAmountToMinor('12.50')).toBe(1250)
    expect(parseAmountToMinor('12,34')).toBe(1234)
    expect(parseAmountToMinor('0')).toBe(0)
  })

  it('rejects input that is not a plain non-negative amount', () => {
    expect(parseAmountToMinor('')).toBeNull()
    expect(parseAmountToMinor('abc')).toBeNull()
    expect(parseAmountToMinor('1.234')).toBeNull()
    expect(parseAmountToMinor('-5')).toBeNull()
    expect(parseAmountToMinor('1e3')).toBeNull()
  })
})

describe('minorToInputValue', () => {
  it('renders minor units as an editable value', () => {
    expect(minorToInputValue(1250)).toBe('12.50')
    expect(minorToInputValue(0)).toBe('0.00')
    expect(minorToInputValue(null)).toBe('')
  })
})

describe('formatMinor', () => {
  it('formats using the household currency', () => {
    expect(formatMinor(1234, 'EUR', 'en-US')).toContain('12.34')
    expect(formatMinor(null, 'EUR', 'en-US')).toBeNull()
  })

  it('falls back when the currency code is not recognised', () => {
    expect(formatMinor(100, 'NOT-A-CODE', 'en-US')).toContain('1.00')
  })
})
