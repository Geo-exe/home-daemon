import type { RecurrenceFrequency } from '../api/types'

export interface RecurrenceLabel {
  key: string
  options?: Record<string, number>
}

/**
 * Turns the stored frequency and interval into an i18n key. Keeping the wording in the
 * translation file means a new language does not need a new branch here.
 */
export function recurrenceLabel(frequency: RecurrenceFrequency, interval: number): RecurrenceLabel {
  switch (frequency) {
    case 'Daily':
      return interval === 1 ? { key: 'chores.recurrence.daily' } : { key: 'chores.recurrence.everyDays', options: { count: interval } }
    case 'Weekly':
      return interval === 1 ? { key: 'chores.recurrence.weekly' } : { key: 'chores.recurrence.everyWeeks', options: { count: interval } }
    case 'Monthly':
      return interval === 1 ? { key: 'chores.recurrence.monthly' } : { key: 'chores.recurrence.everyMonths', options: { count: interval } }
    case 'Yearly':
      return interval === 1 ? { key: 'chores.recurrence.yearly' } : { key: 'chores.recurrence.everyYears', options: { count: interval } }
    case 'FixedIntervalDays':
      return { key: 'chores.recurrence.fixedDays', options: { count: interval } }
  }
}
