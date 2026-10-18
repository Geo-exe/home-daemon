import { parseCalendarDate, toCalendarDate } from './format'
import type { CalendarDate } from '../api/types'

export function addDays(date: CalendarDate, days: number): CalendarDate {
  const parsed = parseCalendarDate(date)
  parsed.setDate(parsed.getDate() + days)
  return toCalendarDate(parsed)
}

/**
 * The planner asks for `horizonDays` calendar days starting at `from`. The server treats `to`
 * as exclusive, which matches adding whole days.
 */
export function plannerWindow(from: CalendarDate, horizonDays: number): { from: CalendarDate; to: CalendarDate } {
  const span = Math.max(1, horizonDays)
  return { from, to: addDays(from, span) }
}

/** Shifts a visible window, keeping its length. */
export function shiftWindow(
  from: CalendarDate,
  to: CalendarDate,
  days: number,
): { from: CalendarDate; to: CalendarDate } {
  return { from: addDays(from, days), to: addDays(to, days) }
}

export function daysBetween(from: CalendarDate, to: CalendarDate): number {
  const start = parseCalendarDate(from).getTime()
  const end = parseCalendarDate(to).getTime()
  return Math.max(1, Math.round((end - start) / 86_400_000))
}

/** Share of the day's window that is still free, clamped to a usable 0–1 range. */
export function freeRatio(freeMinutes: number, windowMinutes: number): number {
  if (windowMinutes <= 0) {
    return 0
  }

  return Math.min(1, Math.max(0, freeMinutes / windowMinutes))
}
