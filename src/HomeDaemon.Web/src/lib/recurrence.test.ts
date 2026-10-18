import { describe, expect, it } from 'vitest'
import { frequencyOptions, intervalLabelKey, recurrenceLabel, standardFrequencies } from './recurrence'

describe('recurrenceLabel', () => {
  it('names the unit when the interval is one', () => {
    expect(recurrenceLabel('Daily', 1)).toEqual({ key: 'chores.recurrence.daily' })
    expect(recurrenceLabel('Weekly', 1)).toEqual({ key: 'chores.recurrence.weekly' })
    expect(recurrenceLabel('Monthly', 1)).toEqual({ key: 'chores.recurrence.monthly' })
    expect(recurrenceLabel('Yearly', 1)).toEqual({ key: 'chores.recurrence.yearly' })
  })

  it('passes the count for larger intervals', () => {
    expect(recurrenceLabel('Monthly', 3)).toEqual({ key: 'chores.recurrence.everyMonths', options: { count: 3 } })
    expect(recurrenceLabel('Daily', 2)).toEqual({ key: 'chores.recurrence.everyDays', options: { count: 2 } })
  })

  it('always describes a fixed day interval with its count', () => {
    expect(recurrenceLabel('FixedIntervalDays', 1)).toEqual({
      key: 'chores.recurrence.fixedDays',
      options: { count: 1 },
    })
  })
})

describe('intervalLabelKey', () => {
  it('names the unit the interval counts in', () => {
    expect(intervalLabelKey('Daily')).toBe('chores.field.intervalDays')
    expect(intervalLabelKey('Weekly')).toBe('chores.field.intervalWeeks')
    expect(intervalLabelKey('Monthly')).toBe('chores.field.intervalMonths')
    expect(intervalLabelKey('Yearly')).toBe('chores.field.intervalYears')
  })

  it('treats a fixed day interval as days', () => {
    expect(intervalLabelKey('FixedIntervalDays')).toBe('chores.field.intervalDays')
  })
})

describe('frequencyOptions', () => {
  it('offers the four units for a new chore', () => {
    expect(frequencyOptions('Daily')).toEqual(standardFrequencies)
    expect(frequencyOptions('Daily')).toHaveLength(4)
  })

  it('keeps a fixed day interval selectable while editing one', () => {
    expect(frequencyOptions('FixedIntervalDays')).toEqual([...standardFrequencies, 'FixedIntervalDays'])
  })
})
