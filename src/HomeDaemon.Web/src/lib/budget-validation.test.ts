import { describe, expect, it } from 'vitest'
import {
  budgetCategoryFormSchema,
  budgetExpenseFormSchema,
  budgetIncomeFormSchema,
  budgetTransferFormSchema,
  issuesToFieldErrors,
} from './validation'

const expense = {
  date: '2026-06-10',
  amount: '12.50',
  categoryId: 'category-1',
  subcategoryId: '',
  paidByMemberId: 'member-1',
  description: '',
}

describe('budgetExpenseFormSchema', () => {
  it('accepts a well-formed expense', () => {
    expect(budgetExpenseFormSchema.safeParse(expense).success).toBe(true)
  })

  it('rejects a zero or malformed amount', () => {
    expect(budgetExpenseFormSchema.safeParse({ ...expense, amount: '0' }).success).toBe(false)
    expect(budgetExpenseFormSchema.safeParse({ ...expense, amount: 'abc' }).success).toBe(false)
  })

  it('requires a category and a payer', () => {
    const result = budgetExpenseFormSchema.safeParse({ ...expense, categoryId: '', paidByMemberId: '' })

    expect(issuesToFieldErrors(result.error!)).toMatchObject({
      categoryId: 'budget.validation.categoryRequired',
      paidByMemberId: 'budget.validation.payerRequired',
    })
  })
})

describe('budgetTransferFormSchema', () => {
  const transfer = {
    date: '2026-06-10',
    amount: '10',
    settlementYear: '2026',
    fromMemberId: 'a',
    toMemberId: 'b',
    description: '',
  }

  it('accepts a well-formed transfer', () => {
    expect(budgetTransferFormSchema.safeParse(transfer).success).toBe(true)
  })

  it('refuses the same person on both sides', () => {
    const result = budgetTransferFormSchema.safeParse({ ...transfer, toMemberId: 'a' })

    expect(issuesToFieldErrors(result.error!)).toHaveProperty('toMemberId', 'budget.validation.samePerson')
  })

  it('refuses a settlement year that is not four digits', () => {
    expect(budgetTransferFormSchema.safeParse({ ...transfer, settlementYear: '26' }).success).toBe(false)
  })
})

describe('budgetIncomeFormSchema', () => {
  it('accepts an empty cell and an explicit zero', () => {
    expect(budgetIncomeFormSchema.safeParse({ amount: '' }).success).toBe(true)
    expect(budgetIncomeFormSchema.safeParse({ amount: '0' }).success).toBe(true)
  })

  it('rejects a negative amount', () => {
    expect(budgetIncomeFormSchema.safeParse({ amount: '-1' }).success).toBe(false)
  })
})

describe('budgetCategoryFormSchema', () => {
  it('converts the order and rejects a blank name', () => {
    const result = budgetCategoryFormSchema.safeParse({ name: 'Food', sortOrder: '2' })

    expect(result.success).toBe(true)
    expect(result.data?.sortOrder).toBe(2)
    expect(budgetCategoryFormSchema.safeParse({ name: '  ', sortOrder: '0' }).success).toBe(false)
  })
})
