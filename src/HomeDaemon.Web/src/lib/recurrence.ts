import type { RecurrenceFrequency } from '../api/types'

export interface RecurrenceLabel {
  key: string
  options?: Record<string, number>
}

/**
 * Turns the stored frequency and interval into an i18n key. Keeping the wording in the
 * translation file means a new language does not need a new branch here.
 *
 * An interval of one is named after its unit ("Daily"); anything larger reads as a count
 * ("Every 3 months").
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

/** The unit a recurrence counts in, used to label the interval field. */
const intervalLabelKeys: Record<RecurrenceFrequency, string> = {
  Daily: 'chores.field.intervalDays',
  FixedIntervalDays: 'chores.field.intervalDays',
  Weekly: 'chores.field.intervalWeeks',
  Monthly: 'chores.field.intervalMonths',
  Yearly: 'chores.field.intervalYears',
}

export function intervalLabelKey(frequency: RecurrenceFrequency): string {
  return intervalLabelKeys[frequency]
}

/**
 * The frequencies offered when creating a chore. `FixedIntervalDays` counts in days exactly
 * like `Daily` does, so it is not a separate choice; it is only offered while editing a chore
 * that already uses it, so an existing value stays visible and is not silently rewritten.
 */
export const standardFrequencies: readonly RecurrenceFrequency[] = ['Daily', 'Weekly', 'Monthly', 'Yearly']

export function frequencyOptions(current: RecurrenceFrequency): readonly RecurrenceFrequency[] {
  return current === 'FixedIntervalDays' ? [...standardFrequencies, 'FixedIntervalDays'] : standardFrequencies
}
