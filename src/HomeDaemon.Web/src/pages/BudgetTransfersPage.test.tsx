import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { BudgetTransferSuggestion } from '../api/types'
import { todayInTimeZone } from '../lib/format'
import { buildTransferShortcuts } from '../lib/transfers'
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

function member(id: string, name: string) {
  return {
    id,
    name,
    color: null,
    isAssignable: true,
    isBudgetParticipant: true,
    todoListEntityId: null,
    personEntityId: null,
    calendarEntityIds: [],
  }
}

const alice = member('member-alice', 'Alice')
const bob = member('member-bob', 'Bob')
const charlie = member('member-charlie', 'Charlie')

function suggestion(from: typeof alice, to: typeof alice): BudgetTransferSuggestion {
  return {
    fromMemberId: from.id,
    fromMemberName: from.name,
    toMemberId: to.id,
    toMemberName: to.name,
  }
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

function stubApi({
  members = [alice, bob],
  suggestions = [] as BudgetTransferSuggestion[],
}: { members?: unknown[]; suggestions?: BudgetTransferSuggestion[] } = {}) {
  const posts: unknown[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url
      const method = (init?.method ?? 'GET').toUpperCase()

      if (url.endsWith('/api/v1/auth/csrf')) {
        return jsonResponse({ token: 'test-token' })
      }

      if (url.includes('/api/v1/budget/transfers/suggestions') && method === 'GET') {
        return jsonResponse(suggestions)
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

describe('buildTransferShortcuts', () => {
  it('falls back to the two directions of a two-person household', () => {
    expect(buildTransferShortcuts([alice, bob], [])).toEqual([
      suggestion(alice, bob),
      suggestion(bob, alice),
    ])
  })

  it('keeps the most recent distinct directions and fills the rest', () => {
    const shortcuts = buildTransferShortcuts([alice, bob, charlie], [suggestion(charlie, alice)])

    expect(shortcuts[0]).toEqual(suggestion(charlie, alice))
    expect(shortcuts).toHaveLength(2)
    expect(shortcuts[1]).not.toEqual(suggestion(charlie, alice))
  })
})

describe('BudgetTransfersPage', () => {
  it('offers both directions with two participants and hides the manual pickers', async () => {
    const { posts } = stubApi()
    renderPage()
    const { actor, dialog } = await openDialog()

    const toAlice = within(dialog).getByRole('button', { name: 'Bob → Alice' })
    expect(within(dialog).getByRole('button', { name: 'Alice → Bob' })).toBeInTheDocument()
    expect(within(dialog).queryByLabelText('From')).not.toBeInTheDocument()
    expect(within(dialog).queryByLabelText('To')).not.toBeInTheDocument()

    await actor.click(toAlice)
    await actor.type(within(dialog).getByLabelText('Amount'), '40')
    const settlementYear = within(dialog).getByLabelText('Settlement year')
    await actor.clear(settlementYear)
    await actor.type(settlementYear, '2025')
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

  it('proposes the recent directions with more participants and keeps the pickers available', async () => {
    const { posts } = stubApi({
      members: [alice, bob, charlie],
      suggestions: [suggestion(charlie, alice), suggestion(bob, charlie)],
    })
    renderPage()
    const { actor, dialog } = await openDialog()

    expect(await within(dialog).findByRole('button', { name: 'Charlie → Alice' })).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Bob → Charlie' })).toBeInTheDocument()
    expect(within(dialog).getByText('Choose another pair')).toBeInTheDocument()

    await actor.click(within(dialog).getByRole('button', { name: 'Charlie → Alice' }))
    await actor.type(within(dialog).getByLabelText('Amount'), '12')
    await actor.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(posts[0]).toMatchObject({
      amountMinor: 1200,
      fromMemberId: 'member-charlie',
      toMemberId: 'member-alice',
    })
  })

  it('refuses the same person on both sides when using the manual pickers', async () => {
    const { posts } = stubApi({ members: [alice, bob, charlie] })
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
    stubApi({ members: [alice, bob, charlie] })
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
