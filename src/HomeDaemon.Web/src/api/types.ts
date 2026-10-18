/**
 * Mirrors of the API contracts. Enums travel as strings; dates are calendar dates in
 * `yyyy-MM-dd` form and must never be shifted by the browser time zone.
 */

export type RecurrenceFrequency = 'Daily' | 'Weekly' | 'Monthly' | 'Yearly' | 'FixedIntervalDays'

export type RecurrenceMode = 'Dynamic' | 'Anchored'

export type AssignmentPolicy = 'Both' | 'SpecificMember' | 'Alternating'

export type ChoreScheduleState = 'Unplanned' | 'Scheduled' | 'Unscheduled' | 'Paused'

export type CompletionSource = 'HomeAssistant' | 'Api' | 'Manual'

export type CalendarDate = string

export interface CursorPage<T> {
  items: T[]
  nextCursor: string | null
  hasMore: boolean
}

export interface CurrentUser {
  id: string
  email: string
  displayName: string | null
  householdMemberId: string | null
  mustChangePassword: boolean
  roles: string[]
}

export interface Household {
  id: string
  name: string
  timeZoneId: string
  currency: string
  planningHorizonDays: number
  dailyWindowStart: string
  dailyWindowEnd: string
  maxChoreMinutesPerWeekday: number | null
  maxChoreMinutesPerWeekendDay: number | null
}

export interface Room {
  id: string
  name: string
  sortOrder: number
}

export interface HouseholdMember {
  id: string
  name: string
  color: string | null
  isAssignable: boolean
  isBudgetParticipant: boolean
  todoListEntityId: string | null
  personEntityId: string | null
  calendarEntityIds: string[]
}

export interface ChoreListItem {
  id: string
  title: string
  roomId: string | null
  isActive: boolean
  estimatedMinutes: number
  frequency: RecurrenceFrequency
  interval: number
  mode: RecurrenceMode
  assignmentPolicy: AssignmentPolicy
  fixedAssigneeMemberId: string | null
  lastCompletedDate: CalendarDate | null
  nextOccurrenceDate: CalendarDate | null
  nextDueDate: CalendarDate | null
  plannedMemberId: string | null
  isLocked: boolean
  scheduleState: ChoreScheduleState
  unscheduledReason: string | null
}

export interface ChoreCompletion {
  id: string
  completedDate: CalendarDate
  recordedAtUtc: string
  source: CompletionSource
  completedByMemberId: string | null
  completedByUserId: string | null
}

export interface ChoreDetail {
  id: string
  householdId: string
  title: string
  description: string | null
  roomId: string | null
  isActive: boolean
  estimatedMinutes: number
  effortWeight: number | null
  preferSameRoomGrouping: boolean
  frequency: RecurrenceFrequency
  interval: number
  mode: RecurrenceMode
  anchorDate: CalendarDate | null
  assignmentPolicy: AssignmentPolicy
  fixedAssigneeMemberId: string | null
  lastCompletedDate: CalendarDate | null
  lastCompletedByMemberId: string | null
  nextOccurrenceDate: CalendarDate | null
  nextDueDate: CalendarDate | null
  plannedMemberId: string | null
  isLocked: boolean
  scheduleState: ChoreScheduleState
  unscheduledReason: string | null
  createdAtUtc: string
  updatedAtUtc: string
  recentCompletions: ChoreCompletion[]
}

export interface ChoreWriteModel {
  title: string
  description: string | null
  roomId: string | null
  isActive: boolean
  estimatedMinutes: number
  effortWeight: number | null
  preferSameRoomGrouping: boolean
  frequency: RecurrenceFrequency
  interval: number
  mode: RecurrenceMode
  anchorDate: CalendarDate | null
  assignmentPolicy: AssignmentPolicy
  fixedAssigneeMemberId: string | null
}

export interface ChoreCompletionResult {
  choreId: string
  isDuplicate: boolean
}

export interface PlannerOccurrence {
  choreId: string
  title: string
  roomId: string | null
  estimatedMinutes: number
  memberId: string
  isLocked: boolean
  dueDate: CalendarDate
}

export interface PlannerMemberDay {
  memberId: string
  windowMinutes: number
  calendarOccupiedMinutes: number
  scheduledMinutes: number
  freeMinutes: number
  isAllDayBlocked: boolean
}

export interface PlannerDay {
  date: CalendarDate
  members: PlannerMemberDay[]
  occurrences: PlannerOccurrence[]
}

export interface PlannerUnscheduled {
  choreId: string
  title: string
  reason: string
}

export interface Planner {
  from: CalendarDate
  to: CalendarDate
  calendarDataAvailable: boolean
  days: PlannerDay[]
  unscheduled: PlannerUnscheduled[]
}

export type ScheduleRunStatus = 'Running' | 'Succeeded' | 'Failed'

export type ScheduleRunTrigger = 'Nightly' | 'Manual'

export interface ScheduleRun {
  id: string
  trigger: ScheduleRunTrigger
  status: ScheduleRunStatus
  startedAtUtc: string
  completedAtUtc: string | null
  scheduledCount: number
  unscheduledCount: number
  summary: string | null
  diagnostics: string | null
  errorMessage: string | null
}

export interface RecalculationAccepted {
  runId: string
  status: string
}

export interface ChoreHistoryItem {
  id: string
  choreDefinitionId: string
  choreTitle: string
  completedDate: CalendarDate
  recordedAtUtc: string
  source: CompletionSource
  completedByMemberId: string | null
  completedByMemberName: string | null
  completedByUserId: string | null
}

// ------------------------------------------------------------------ budget
//
// Money always travels as integer minor units (cents); the frontend only formats it for
// display. Dates are calendar dates in `yyyy-MM-dd` form.

export interface BudgetSubcategory {
  id: string
  categoryId: string
  name: string
  sortOrder: number
}

export interface BudgetCategory {
  id: string
  name: string
  sortOrder: number
  subcategories: BudgetSubcategory[]
}

/** One cell of the income grid; a null amount means the month was not entered. */
export interface BudgetIncomeMonth {
  month: number
  amountMinor: number | null
  incomeId: string | null
}

export interface BudgetMemberIncomes {
  memberId: string
  name: string
  months: BudgetIncomeMonth[]
}

export interface BudgetIncomes {
  year: number
  currency: string
  members: BudgetMemberIncomes[]
}

export interface BudgetExpense {
  id: string
  date: CalendarDate
  amountMinor: number
  categoryId: string
  categoryName: string
  subcategoryId: string | null
  subcategoryName: string | null
  paidByMemberId: string
  paidByMemberName: string
  description: string | null
}

export interface BudgetTransfer {
  id: string
  date: CalendarDate
  settlementYear: number
  amountMinor: number
  fromMemberId: string
  fromMemberName: string
  toMemberId: string
  toMemberName: string
  description: string | null
}

export interface BudgetMemberSummary {
  memberId: string
  name: string
  annualIncomeMinor: number
  share: number | null
  paidMinor: number
  dueMinor: number | null
  transferredOutMinor: number
  transferredInMinor: number
  balanceMinor: number | null
  missingMonths: number[]
}

export interface BudgetCategoryTotal {
  categoryId: string
  name: string
  amountMinor: number
}

export interface BudgetSummary {
  year: number
  currency: string
  totalIncomeMinor: number
  totalExpensesMinor: number
  proportionAvailable: boolean
  incomeComplete: boolean
  members: BudgetMemberSummary[]
  byCategory: BudgetCategoryTotal[]
}
