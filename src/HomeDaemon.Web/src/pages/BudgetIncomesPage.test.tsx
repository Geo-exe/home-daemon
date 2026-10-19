import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BudgetIncomesPage } from './BudgetIncomesPage'

function buildMonths() {
  const months = Array.from({ length: 12 }, (_, index) => ({
    month: index + 1,
    amountMinor: null as number | null,
    incomeId: null as string | null,
  }))

  // February already has a recorded income, so it can be cleared again.
  months[1] = { month: 2, amountMinor: 5000, incomeId: 'income-2' }
  return months
}

const incomes = {
  year: 2026,
  currency: 'EUR',
  members: [{ memberId: 'member-alice', name: 'Alice', months: buildMonths() }],
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

function stubApi() {
  const puts: unknown[] = []
  const deletes: string[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url
      const method = (init?.method ?? 'GET').toUpperCase()

      if (url.endsWith('/api/v1/auth/csrf')) {
        return jsonResponse({ token: 'test-token' })
      }

      if (url.endsWith('/api/v1/budget/years')) {
        return jsonResponse([2026])
      }

      if (url.endsWith('/api/v1/budget/incomes') && method === 'PUT') {
        puts.push(init?.body ? (JSON.parse(init.body as string) as unknown) : null)
        return jsonResponse({ month: 1, amountMinor: 150000, incomeId: 'income-1' })
      }

      if (url.includes('/api/v1/budget/incomes/') && method === 'DELETE') {
        deletes.push(url)
        return new Response(null, { status: 204 })
      }

      if (url.includes('/api/v1/budget/incomes') && method === 'GET') {
        return jsonResponse(incomes)
      }

      return jsonResponse([])
    }),
  )

  return { puts, deletes }
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/budget/incomes']}>
        <BudgetIncomesPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('BudgetIncomesPage', () => {
  it('saves the typed amount for that month when the field loses focus', async () => {
    const { puts } = stubApi()
    const actor = userEvent.setup()
    renderPage()

    const january = await screen.findByLabelText('January income')
    await actor.type(january, '1500')
    await actor.tab()

    expect(puts).toHaveLength(1)
    expect(puts[0]).toMatchObject({ memberId: 'member-alice', year: 2026, month: 1, amountMinor: 150000 })
  })

  it('clears a month that already had a value', async () => {
    const { puts, deletes } = stubApi()
    const actor = userEvent.setup()
    renderPage()

    const february = await screen.findByLabelText('February income')
    expect(february).toHaveValue('50.00')

    await actor.clear(february)
    await actor.tab()

    expect(puts).toHaveLength(0)
    expect(deletes).toHaveLength(1)
    expect(deletes[0]).toContain('income-2')
  })

  it('flags a malformed amount and does not call the API', async () => {
    const { puts } = stubApi()
    const actor = userEvent.setup()
    renderPage()

    const january = await screen.findByLabelText('January income')
    await actor.type(january, 'abc')
    await actor.tab()

    expect(january).toHaveAttribute('aria-invalid', 'true')
    expect(await screen.findByRole('alert')).toHaveTextContent('Enter an amount such as 12.50.')
    expect(puts).toHaveLength(0)
  })

  it('does not flag months that still have no income recorded', async () => {
    stubApi()
    renderPage()

    const january = await screen.findByLabelText('January income')

    expect(january.closest('li')).not.toHaveClass('is-missing')
  })
})
