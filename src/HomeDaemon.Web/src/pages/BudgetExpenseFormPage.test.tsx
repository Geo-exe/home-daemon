import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { SessionProvider } from '../auth/SessionProvider'
import { todayInTimeZone } from '../lib/format'
import { BudgetExpenseFormPage } from './BudgetExpenseFormPage'

const household = {
  id: 'household-1',
  name: 'Home',
  timeZoneId: 'UTC',
  currency: 'EUR',
  planningHorizonDays: 30,
  dailyWindowStart: '08:00:00',
  dailyWindowEnd: '21:00:00',
  maxChoreMinutesPerWeekday: null,
  maxChoreMinutesPerWeekendDay: null,
}

const categories = [
  {
    id: 'cat-food',
    name: 'Food',
    sortOrder: 0,
    subcategories: [{ id: 'sub-groceries', categoryId: 'cat-food', name: 'Groceries', sortOrder: 0 }],
  },
]

const members = [
  {
    id: 'member-alice',
    name: 'Alice',
    color: null,
    isAssignable: true,
    isBudgetParticipant: true,
    todoListEntityId: null,
    personEntityId: null,
    calendarEntityIds: [],
  },
  {
    id: 'member-guest',
    name: 'Guest',
    color: null,
    isAssignable: true,
    isBudgetParticipant: false,
    todoListEntityId: null,
    personEntityId: null,
    calendarEntityIds: [],
  },
]

const currentUser = {
  id: 'user-1',
  email: 'alice@example.test',
  displayName: 'Alice',
  householdMemberId: 'member-alice',
  mustChangePassword: false,
  roles: ['Admin'],
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

/** Records the JSON body of every expense creation so the test can assert the wire shape. */
function stubApi({ linkedMemberId = 'member-alice' }: { linkedMemberId?: string | null } = {}) {
  const posted: unknown[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url
      const method = (init?.method ?? 'GET').toUpperCase()

      if (url.endsWith('/api/v1/auth/me')) {
        return jsonResponse({ ...currentUser, householdMemberId: linkedMemberId })
      }

      if (url.endsWith('/api/v1/auth/csrf')) {
        return jsonResponse({ token: 'test-token' })
      }

      if (url.endsWith('/api/v1/budget/expenses') && method === 'POST') {
        posted.push(init?.body ? (JSON.parse(init.body as string) as unknown) : null)
        return jsonResponse({ id: 'expense-1' }, 201)
      }

      if (url.endsWith('/api/v1/budget/categories')) {
        return jsonResponse(categories)
      }

      if (url.endsWith('/api/v1/household/members')) {
        return jsonResponse(members)
      }

      if (url.endsWith('/api/v1/household')) {
        return jsonResponse(household)
      }

      return jsonResponse([])
    }),
  )

  return { posted }
}

function renderForm() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <SessionProvider>
        <MemoryRouter initialEntries={['/budget/expenses/new']}>
          <Routes>
            <Route path="/budget/expenses/new" element={<BudgetExpenseFormPage />} />
            <Route path="/budget/expenses" element={<div>Expenses list</div>} />
          </Routes>
        </MemoryRouter>
      </SessionProvider>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('BudgetExpenseFormPage', () => {
  it('prefills the date with today in the household time zone', async () => {
    stubApi()
    renderForm()

    const date = await screen.findByLabelText('Date')
    expect(date).toHaveValue(todayInTimeZone('UTC'))
  })

  it('preselects the signed-in person as the payer', async () => {
    stubApi()
    renderForm()

    const payer = await screen.findByRole('group', { name: 'Paid by' })
    const alice = await within(payer).findByRole('button', { name: 'Alice' })

    expect(alice).toHaveAttribute('aria-pressed', 'true')
  })

  it('offers only the people who take part in the budget as payer', async () => {
    stubApi()
    renderForm()

    const payer = await screen.findByRole('group', { name: 'Paid by' })
    await within(payer).findByRole('button', { name: 'Alice' })
    const names = within(payer)
      .getAllByRole('button')
      .map((button) => button.textContent)

    expect(names).toContain('Alice')
    expect(names).not.toContain('Guest')
  })

  it('refuses to save without a positive amount', async () => {
    const { posted } = stubApi()
    const actor = userEvent.setup()
    renderForm()

    await actor.click(await screen.findByRole('button', { name: 'Save' }))

    // The error is announced and attached to a field that stays reachable by its label.
    const alerts = await screen.findAllByRole('alert')
    expect(alerts.map((alert) => alert.textContent)).toContain('Enter an amount such as 12.50.')
    expect(screen.getByLabelText('Amount')).toBeInTheDocument()
    expect(posted).toHaveLength(0)
  })

  it('sends minor units, a null subcategory and a null description', async () => {
    const { posted } = stubApi()
    const actor = userEvent.setup()
    renderForm()

    await actor.type(await screen.findByLabelText('Amount'), '12.50')
    const category = screen.getByRole('group', { name: 'Category' })
    await actor.click(await within(category).findByRole('button', { name: 'Food' }))
    await actor.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Expenses list')).toBeInTheDocument()
    expect(posted).toHaveLength(1)
    expect(posted[0]).toMatchObject({
      amountMinor: 1250,
      categoryId: 'cat-food',
      subcategoryId: null,
      paidByMemberId: 'member-alice',
      description: null,
    })
  })

  it('keeps the date, category and payer when saving another expense', async () => {
    const { posted } = stubApi()
    const actor = userEvent.setup()
    renderForm()

    await actor.type(await screen.findByLabelText('Amount'), '5')
    const category = screen.getByRole('group', { name: 'Category' })
    const foodChip = await within(category).findByRole('button', { name: 'Food' })
    await actor.click(foodChip)
    await actor.click(screen.getByRole('button', { name: 'Save & add' }))

    // The form stays open and clears only the amount.
    expect(await screen.findByText('Expense saved. Add the next one.')).toBeInTheDocument()
    expect(screen.queryByText('Expenses list')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Amount')).toHaveValue('')
    expect(within(category).getByRole('button', { name: 'Food' })).toHaveAttribute('aria-pressed', 'true')

    await actor.type(screen.getByLabelText('Amount'), '7.50')
    await actor.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Expenses list')).toBeInTheDocument()
    expect(posted).toHaveLength(2)
    expect(posted[1]).toMatchObject({ amountMinor: 750, categoryId: 'cat-food', paidByMemberId: 'member-alice' })
  })
})
