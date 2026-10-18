import { describe, expect, it } from 'vitest'
import { recurrenceLabel } from './recurrence'

describe('recurrenceLabel', () => {
  it('uses the singular key when the interval is one', () => {
    expect(recurrenceLabel('Daily', 1)).toEqual({ key: 'chores.recurrence.daily' })
    expect(recurrenceLabel('Weekly', 1)).toEqual({ key: 'chores.recurrence.weekly' })
  })

  it('passes the count for larger intervals', () => {
    expect(recurrenceLabel('Monthly', 3)).toEqual({ key: 'chores.recurrence.everyMonths', options: { count: 3 } })
  })

  it('always describes a fixed day interval with its count', () => {
    expect(recurrenceLabel('FixedIntervalDays', 1)).toEqual({
      key: 'chores.recurrence.fixedDays',
      options: { count: 1 },
    })
  })
})
