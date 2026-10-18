import { describe, expect, it } from 'vitest'
import {
  accountFormSchema,
  householdFormSchema,
  issuesToFieldErrors,
  memberFormSchema,
  roomFormSchema,
  splitIdentifierList,
} from './validation'

describe('splitIdentifierList', () => {
  it('accepts one identifier per line, commas and stray blanks', () => {
    expect(splitIdentifierList('calendar.a\ncalendar.b')).toEqual(['calendar.a', 'calendar.b'])
    expect(splitIdentifierList('calendar.a, calendar.b')).toEqual(['calendar.a', 'calendar.b'])
    expect(splitIdentifierList('  \n calendar.a \n\n')).toEqual(['calendar.a'])
    expect(splitIdentifierList('')).toEqual([])
  })
})

describe('roomFormSchema', () => {
  it('converts the typed order', () => {
    const result = roomFormSchema.safeParse({ name: 'Kitchen', sortOrder: '3' })

    expect(result.success).toBe(true)
    expect(result.data?.sortOrder).toBe(3)
  })

  it('rejects a blank name and a negative order', () => {
    expect(roomFormSchema.safeParse({ name: '  ', sortOrder: '0' }).success).toBe(false)
    expect(roomFormSchema.safeParse({ name: 'Kitchen', sortOrder: '-1' }).success).toBe(false)
  })
})

describe('memberFormSchema', () => {
  const valid = {
    name: 'Alice',
    color: '',
    isAssignable: true,
    todoListEntityId: 'todo.alice',
    personEntityId: '',
    calendarEntityIds: '',
  }

  it('accepts an empty colour and a hex colour', () => {
    expect(memberFormSchema.safeParse(valid).success).toBe(true)
    expect(memberFormSchema.safeParse({ ...valid, color: '#4F46E5' }).success).toBe(true)
  })

  it('rejects a colour that is not hex', () => {
    expect(memberFormSchema.safeParse({ ...valid, color: 'indigo' }).success).toBe(false)
  })
})

describe('householdFormSchema', () => {
  const valid = {
    name: 'Home',
    timeZoneId: 'Europe/Rome',
    planningHorizonDays: '30',
    dailyWindowStart: '08:00',
    dailyWindowEnd: '21:00',
    maxChoreMinutesPerWeekday: '',
    maxChoreMinutesPerWeekendDay: '240',
  }

  it('turns empty caps into null and keeps typed ones', () => {
    const result = householdFormSchema.safeParse(valid)

    expect(result.success).toBe(true)
    expect(result.data?.maxChoreMinutesPerWeekday).toBeNull()
    expect(result.data?.maxChoreMinutesPerWeekendDay).toBe(240)
  })

  it('requires the window to end after it starts', () => {
    const result = householdFormSchema.safeParse({ ...valid, dailyWindowStart: '21:00', dailyWindowEnd: '08:00' })

    expect(result.success).toBe(false)
    expect(issuesToFieldErrors(result.error!)).toHaveProperty(
      'dailyWindowEnd',
      'settings.household.validation.windowOrder',
    )
  })

  it('validates the horizon range', () => {
    expect(householdFormSchema.safeParse({ ...valid, planningHorizonDays: '0' }).success).toBe(false)
    expect(householdFormSchema.safeParse({ ...valid, planningHorizonDays: '400' }).success).toBe(false)
  })

  it('validates the time format', () => {
    expect(householdFormSchema.safeParse({ ...valid, dailyWindowStart: '8:00' }).success).toBe(false)
  })
})

describe('accountFormSchema', () => {
  it('validates the email and keeps the rest optional', () => {
    expect(accountFormSchema.safeParse({ email: 'a@b.co', displayName: '', householdMemberId: '', isAdmin: false }).success).toBe(true)
    expect(accountFormSchema.safeParse({ email: 'not-an-email', displayName: '', householdMemberId: '', isAdmin: false }).success).toBe(false)
  })
})
