import type { CalendarDate } from '../api/types'

/**
 * Parses a `yyyy-MM-dd` calendar date into a local `Date` at midnight. Building the date from
 * its parts avoids the classic off-by-one day caused by `new Date('2026-06-10')`, which is
 * interpreted as UTC.
 */
export function parseCalendarDate(value: CalendarDate): Date {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year ?? 1970, (month ?? 1) - 1, day ?? 1)
}

export function formatCalendarDate(value: CalendarDate | null, locale: string): string | null {
  if (!value) {
    return null
  }

  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(parseCalendarDate(value))
}

export function formatDateTime(value: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

/**
 * Today in the household's time zone, which is the date a completion belongs to. Falls back to
 * the browser's own date when the configured zone is not recognised.
 */
export function todayInTimeZone(timeZoneId: string): CalendarDate {
  try {
    // `en-CA` formats as yyyy-MM-dd, which is exactly the wire format.
    return new Intl.DateTimeFormat('en-CA', {
      timeZone: timeZoneId,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(new Date())
  } catch {
    return toCalendarDate(new Date())
  }
}

export function toCalendarDate(date: Date): CalendarDate {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function formatMinutes(minutes: number, locale: string): string {
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60

  if (hours === 0) {
    return new Intl.NumberFormat(locale, { style: 'unit', unit: 'minute', unitDisplay: 'short' }).format(rest)
  }

  const hourPart = new Intl.NumberFormat(locale, { style: 'unit', unit: 'hour', unitDisplay: 'short' }).format(hours)
  if (rest === 0) {
    return hourPart
  }

  const minutePart = new Intl.NumberFormat(locale, { style: 'unit', unit: 'minute', unitDisplay: 'short' }).format(rest)
  return `${hourPart} ${minutePart}`
}

export function formatTimeOnly(value: string): string {
  return value.slice(0, 5)
}
