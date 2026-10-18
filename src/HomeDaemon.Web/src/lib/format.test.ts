import { describe, expect, it } from 'vitest'
import { formatMinutes, parseCalendarDate, toCalendarDate, todayInTimeZone } from './format'

describe('parseCalendarDate', () => {
  it('keeps the calendar day regardless of the local offset', () => {
    const date = parseCalendarDate('2026-06-10')

    expect(date.getFullYear()).toBe(2026)
    expect(date.getMonth()).toBe(5)
    expect(date.getDate()).toBe(10)
  })

  it('round-trips through toCalendarDate', () => {
    expect(toCalendarDate(parseCalendarDate('2026-01-01'))).toBe('2026-01-01')
  })
})

describe('formatMinutes', () => {
  it('formats whole hours and minutes', () => {
    expect(formatMinutes(30, 'en')).toBe('30 min')
    expect(formatMinutes(60, 'en')).toBe('1 hr')
    expect(formatMinutes(90, 'en')).toBe('1 hr 30 min')
  })
})

describe('todayInTimeZone', () => {
  it('returns a calendar date in the wire format', () => {
    expect(todayInTimeZone('UTC')).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })

  it('falls back to the browser date for an unknown zone', () => {
    expect(todayInTimeZone('Not/AZone')).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })
})
