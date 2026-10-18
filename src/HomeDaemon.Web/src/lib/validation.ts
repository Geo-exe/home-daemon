import { z } from 'zod'
import { parseAmountToMinor } from './money'

/**
 * Form-level validation. It mirrors the server rules so mistakes surface early, but the server
 * stays authoritative: every message is an i18n key, never a sentence.
 */

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const calendarDatePattern = /^\d{4}-\d{2}-\d{2}$/

export const newPasswordSchema = z
  .string()
  .min(10, 'password.tooShort')
  .regex(/[A-Z]/, 'password.needsUppercase')
  .regex(/[a-z]/, 'password.needsLowercase')
  .regex(/[0-9]/, 'password.needsDigit')

export const loginSchema = z.object({
  email: z.string().trim().regex(emailPattern, 'login.emailInvalid'),
  password: z.string().min(1, 'login.passwordRequired'),
})

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'changePassword.currentRequired'),
    newPassword: newPasswordSchema,
    confirmPassword: z.string(),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'changePassword.mismatch',
  })

export const recurrenceFrequencies = ['Daily', 'Weekly', 'Monthly', 'Yearly', 'FixedIntervalDays'] as const
export const recurrenceModes = ['Dynamic', 'Anchored'] as const
export const assignmentPolicies = ['Both', 'SpecificMember', 'Alternating'] as const

/**
 * The form keeps strings while the user types and is converted here, so an empty numeric input
 * is a validation message rather than a silent zero.
 */
export const choreFormSchema = z
  .object({
    title: z.string().trim().min(1, 'chores.validation.titleRequired').max(200, 'chores.validation.titleTooLong'),
    description: z.string().trim().max(2000, 'chores.validation.descriptionTooLong'),
    roomId: z.string(),
    isActive: z.boolean(),
    estimatedMinutes: z
      .string()
      .trim()
      .regex(/^\d+$/, 'chores.validation.durationInvalid')
      .transform(Number)
      .refine((value) => value > 0, 'chores.validation.durationPositive')
      .refine((value) => value <= 1440, 'chores.validation.durationTooLong'),
    effortWeight: z
      .string()
      .trim()
      .transform((value) => (value === '' ? null : value))
      .refine((value) => value === null || /^\d+(\.\d+)?$/.test(value), 'chores.validation.effortWeightInvalid')
      .transform((value) => (value === null ? null : Number(value))),
    preferSameRoomGrouping: z.boolean(),
    frequency: z.enum(recurrenceFrequencies, 'chores.validation.frequencyRequired'),
    interval: z
      .string()
      .trim()
      .regex(/^\d+$/, 'chores.validation.intervalInvalid')
      .transform(Number)
      .refine((value) => value >= 1 && value <= 365, 'chores.validation.intervalRange'),
    mode: z.enum(recurrenceModes, 'chores.validation.modeRequired'),
    anchorDate: z
      .string()
      .trim()
      .refine((value) => value === '' || calendarDatePattern.test(value), 'chores.validation.anchorDateInvalid'),
    assignmentPolicy: z.enum(assignmentPolicies, 'chores.validation.assignmentRequired'),
    fixedAssigneeMemberId: z.string(),
  })
  .refine((values) => values.assignmentPolicy !== 'SpecificMember' || values.fixedAssigneeMemberId !== '', {
    path: ['fixedAssigneeMemberId'],
    message: 'chores.validation.assigneeRequired',
  })

export type LoginValues = z.infer<typeof loginSchema>
export type ChangePasswordValues = z.infer<typeof changePasswordSchema>
export type ChoreFormValues = z.input<typeof choreFormSchema>
export type ChoreFormOutput = z.output<typeof choreFormSchema>

/**
 * Collapses a Zod error into one i18n key per field. The messages are keys, so the component
 * runs them through `t()`.
 */
export function issuesToFieldErrors(error: z.ZodError): Record<string, string> {
  const errors: Record<string, string> = {}

  for (const issue of error.issues) {
    const key = issue.path.join('.') || '_'
    errors[key] ??= issue.message
  }

  return errors
}

const timePattern = /^([01]\d|2[0-3]):[0-5]\d$/
const digitsOnly = /^\d+$/

/** Home Assistant entity lists are edited as free text, one identifier per line. */
export function splitIdentifierList(value: string): string[] {
  return value
    .split(/[\n,]/)
    .map((entry) => entry.trim())
    .filter((entry) => entry !== '')
}

export const roomFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'settings.rooms.validation.nameRequired')
    .max(200, 'settings.rooms.validation.nameTooLong'),
  sortOrder: z
    .string()
    .trim()
    .regex(digitsOnly, 'settings.rooms.validation.orderInvalid')
    .transform(Number)
    .refine((value) => value <= 9999, 'settings.rooms.validation.orderRange'),
})

export const memberFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'settings.members.validation.nameRequired')
    .max(200, 'settings.members.validation.nameTooLong'),
  color: z
    .string()
    .trim()
    .refine((value) => value === '' || /^#[0-9a-fA-F]{6}$/.test(value), 'settings.members.validation.colorInvalid'),
  isAssignable: z.boolean(),
  isBudgetParticipant: z.boolean(),
  todoListEntityId: z.string().trim().max(200, 'settings.members.validation.entityTooLong'),
  personEntityId: z.string().trim().max(200, 'settings.members.validation.entityTooLong'),
  calendarEntityIds: z.string().trim().max(2000, 'settings.members.validation.calendarsTooLong'),
})

export const householdFormSchema = z
  .object({
    name: z
      .string()
      .trim()
      .min(1, 'settings.household.validation.nameRequired')
      .max(200, 'settings.household.validation.nameTooLong'),
    timeZoneId: z.string().trim().min(1, 'settings.household.validation.timeZoneRequired'),
    currency: z
      .string()
      .trim()
      .regex(/^[A-Za-z]{3}$/, 'settings.household.validation.currencyInvalid'),
    planningHorizonDays: z
      .string()
      .trim()
      .regex(digitsOnly, 'settings.household.validation.horizonInvalid')
      .transform(Number)
      .refine((value) => value >= 1 && value <= 365, 'settings.household.validation.horizonRange'),
    dailyWindowStart: z.string().trim().regex(timePattern, 'settings.household.validation.timeInvalid'),
    dailyWindowEnd: z.string().trim().regex(timePattern, 'settings.household.validation.timeInvalid'),
    maxChoreMinutesPerWeekday: z
      .string()
      .trim()
      .transform((value) => (value === '' ? null : value))
      .refine((value) => value === null || digitsOnly.test(value), 'settings.household.validation.capInvalid')
      .transform((value) => (value === null ? null : Number(value))),
    maxChoreMinutesPerWeekendDay: z
      .string()
      .trim()
      .transform((value) => (value === '' ? null : value))
      .refine((value) => value === null || digitsOnly.test(value), 'settings.household.validation.capInvalid')
      .transform((value) => (value === null ? null : Number(value))),
  })
  .refine((values) => values.dailyWindowStart < values.dailyWindowEnd, {
    path: ['dailyWindowEnd'],
    message: 'settings.household.validation.windowOrder',
  })

export const accountFormSchema = z.object({
  email: z.string().trim().regex(emailPattern, 'settings.accounts.validation.emailInvalid'),
  displayName: z
    .string()
    .trim()
    .max(200, 'settings.accounts.validation.displayNameTooLong'),
  householdMemberId: z.string(),
  isAdmin: z.boolean(),
})

export type RoomFormValues = z.input<typeof roomFormSchema>
export type MemberFormValues = z.input<typeof memberFormSchema>
export type HouseholdFormValues = z.input<typeof householdFormSchema>
export type AccountFormValues = z.input<typeof accountFormSchema>

/**
 * The first-run setup form. It collects only what is needed to create the household and reuses
 * the household settings messages so the wording stays consistent.
 */
export const householdOnboardingSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'settings.household.validation.nameRequired')
    .max(200, 'settings.household.validation.nameTooLong'),
  timeZoneId: z.string().trim().min(1, 'settings.household.validation.timeZoneRequired'),
  currency: z
    .string()
    .trim()
    .regex(/^[A-Za-z]{3}$/, 'settings.household.validation.currencyInvalid'),
})

export type HouseholdOnboardingValues = z.input<typeof householdOnboardingSchema>

// ------------------------------------------------------------------- budget

const budgetAmount = z
  .string()
  .trim()
  .refine((value) => parseAmountToMinor(value) !== null, 'budget.validation.amountInvalid')

const positiveBudgetAmount = budgetAmount.refine(
  (value) => (parseAmountToMinor(value) ?? 0) > 0,
  'budget.validation.amountPositive',
)

const budgetYear = z.string().trim().regex(/^\d{4}$/, 'budget.validation.yearInvalid')

const budgetDescription = z.string().trim().max(500, 'budget.validation.descriptionTooLong')

export const budgetExpenseFormSchema = z.object({
  date: z.string().trim().refine((value) => calendarDatePattern.test(value), 'budget.validation.dateRequired'),
  amount: positiveBudgetAmount,
  categoryId: z.string().min(1, 'budget.validation.categoryRequired'),
  subcategoryId: z.string(),
  paidByMemberId: z.string().min(1, 'budget.validation.payerRequired'),
  description: budgetDescription,
})

export const budgetTransferFormSchema = z
  .object({
    date: z.string().trim().refine((value) => calendarDatePattern.test(value), 'budget.validation.dateRequired'),
    amount: positiveBudgetAmount,
    settlementYear: budgetYear,
    fromMemberId: z.string().min(1, 'budget.validation.senderRequired'),
    toMemberId: z.string().min(1, 'budget.validation.receiverRequired'),
    description: budgetDescription,
  })
  .refine((values) => values.fromMemberId !== values.toMemberId, {
    path: ['toMemberId'],
    message: 'budget.validation.samePerson',
  })

export const budgetCategoryFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'budget.validation.nameRequired')
    .max(200, 'budget.validation.nameTooLong'),
  sortOrder: z
    .string()
    .trim()
    .regex(digitsOnly, 'budget.validation.orderInvalid')
    .transform(Number)
    .refine((value) => value <= 9999, 'budget.validation.orderRange'),
})

export const budgetSubcategoryFormSchema = budgetCategoryFormSchema

/** A month cell accepts any non-negative amount, including an explicit zero. */
export const budgetIncomeFormSchema = z.object({
  amount: z
    .string()
    .trim()
    .refine(
      (value) => value === '' || (parseAmountToMinor(value) ?? -1) >= 0,
      'budget.validation.amountInvalid',
    ),
})

export type BudgetExpenseFormValues = z.input<typeof budgetExpenseFormSchema>
export type BudgetTransferFormValues = z.input<typeof budgetTransferFormSchema>
export type BudgetCategoryFormValues = z.input<typeof budgetCategoryFormSchema>
export type BudgetSubcategoryFormValues = z.input<typeof budgetSubcategoryFormSchema>
export type BudgetIncomeFormValues = z.input<typeof budgetIncomeFormSchema>
