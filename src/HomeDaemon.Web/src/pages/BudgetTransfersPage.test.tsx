import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { todayInTimeZone } from '../lib/format'
import { BudgetTransfersPage } from './BudgetTransfersPage'

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
    id: 'member-bob',
    name: 'Bob',
    color: null,
    isAssignable: true,
    isBudgetParticipant: true,
    todoListEntityId: null,
    personEntityId: null,
    calendarEntityIds: [],
  },
]

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

function stubApi() {
  const posts: unknown[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url
      const method = (init?.method ?? 'GET').toUpperCase()

      if (url.endsWith('/api/v1/auth/csrf')) {
        return jsonResponse({ token: 'test-token' })
      }

      if (url.endsWith('/api/v1/budget/transfers') && method === 'POST') {
        posts.push(init?.body ? (JSON.parse(init.body as string) as unknown) : null)
        return jsonResponse({ id: 'transfer-1' }, 201)
      }

      if (url.includes('/api/v1/budget/transfers') && method === 'GET') {
        return jsonResponse({ items: [], nextCursor: null, hasMore: false })
      }

      if (url.endsWith('/api/v1/budget/years')) {
        return jsonResponse([2026])
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

  return { posts }
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/budget/transfers']}>
        <BudgetTransfersPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

async function openDialog() {
  const actor = userEvent.setup()
  await actor.click(await screen.findByRole('button', { name: 'Add transfer' }))
  const dialog = await screen.findByRole('dialog', { name: 'New transfer' })
  return { actor, dialog }
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('BudgetTransfersPage', () => {
  it('creates a transfer whose settlement year can differ from the payment date', async () => {
    const { posts } = stubApi()
    renderPage()
    const { actor, dialog } = await openDialog()

    await actor.type(within(dialog).getByLabelText('Amount'), '40')
    const settlementYear = within(dialog).getByLabelText('Settlement year')
    await actor.clear(settlementYear)
    await actor.type(settlementYear, '2025')
    await actor.selectOptions(within(dialog).getByLabelText('From'), 'member-bob')
    await actor.selectOptions(within(dialog).getByLabelText('To'), 'member-alice')
    await actor.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(posts).toHaveLength(1)
    expect(posts[0]).toMatchObject({
      settlementYear: 2025,
      amountMinor: 4000,
      fromMemberId: 'member-bob',
      toMemberId: 'member-alice',
      description: null,
    })
    expect((posts[0] as { date: string }).date).toBe(todayInTimeZone('UTC'))
  })

  it('refuses the same person on both sides', async () => {
    const { posts } = stubApi()
    renderPage()
    const { actor, dialog } = await openDialog()

    await actor.type(within(dialog).getByLabelText('Amount'), '10')
    await actor.selectOptions(within(dialog).getByLabelText('From'), 'member-alice')
    await actor.selectOptions(within(dialog).getByLabelText('To'), 'member-alice')
    await actor.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('The sender and the receiver must be different people.')).toBeInTheDocument()
    expect(posts).toHaveLength(0)
  })

  it('exposes the dialog and its fields to assistive technology', async () => {
    stubApi()
    renderPage()
    const { dialog } = await openDialog()

    expect(dialog).toHaveAccessibleName('New transfer')
    expect(within(dialog).getByLabelText('Amount')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Date')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Settlement year')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('From')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('To')).toBeInTheDocument()
    expect(within(dialog).getByLabelText('Description')).toBeInTheDocument()
  })
})
