import { describe, expect, it } from 'vitest'
import { choreFormSchema, issuesToFieldErrors, newPasswordSchema } from './validation'

const validChore = {
  title: 'Clean oven',
  description: '',
  roomId: '',
  isActive: true,
  estimatedMinutes: '30',
  effortWeight: '',
  preferSameRoomGrouping: false,
  frequency: 'Daily' as const,
  interval: '1',
  mode: 'Dynamic' as const,
  anchorDate: '',
  assignmentPolicy: 'Both' as const,
  fixedAssigneeMemberId: '',
}

describe('choreFormSchema', () => {
  it('converts the typed strings into the API shape', () => {
    const result = choreFormSchema.safeParse({ ...validChore, effortWeight: '1.5', roomId: 'room-1' })

    expect(result.success).toBe(true)
    expect(result.data?.estimatedMinutes).toBe(30)
    expect(result.data?.interval).toBe(1)
    expect(result.data?.effortWeight).toBe(1.5)
    expect(result.data?.roomId).toBe('room-1')
  })

  it('requires a title', () => {
    const result = choreFormSchema.safeParse({ ...validChore, title: '   ' })

    expect(result.success).toBe(false)
    expect(issuesToFieldErrors(result.error!)).toHaveProperty('title', 'chores.validation.titleRequired')
  })

  it('rejects a non-positive duration', () => {
    const result = choreFormSchema.safeParse({ ...validChore, estimatedMinutes: '0' })

    expect(result.success).toBe(false)
    expect(issuesToFieldErrors(result.error!)).toHaveProperty('estimatedMinutes', 'chores.validation.durationPositive')
  })

  it('requires a person when the chore belongs to a single member', () => {
    const result = choreFormSchema.safeParse({ ...validChore, assignmentPolicy: 'SpecificMember' })

    expect(result.success).toBe(false)
    expect(issuesToFieldErrors(result.error!)).toHaveProperty(
      'fixedAssigneeMemberId',
      'chores.validation.assigneeRequired',
    )
  })

  it('accepts an empty numeric field only for the optional effort weight', () => {
    const result = choreFormSchema.safeParse({ ...validChore, interval: '' })

    expect(result.success).toBe(false)
    expect(issuesToFieldErrors(result.error!)).toHaveProperty('interval')
  })
})

describe('newPasswordSchema', () => {
  it('mirrors the server password policy', () => {
    expect(newPasswordSchema.safeParse('short1A').success).toBe(false)
    expect(newPasswordSchema.safeParse('alllowercase1').success).toBe(false)
    expect(newPasswordSchema.safeParse('ALLUPPERCASE1').success).toBe(false)
    expect(newPasswordSchema.safeParse('NoDigitsHereAtAll').success).toBe(false)
    expect(newPasswordSchema.safeParse('Good-Password1').success).toBe(true)
  })
})
