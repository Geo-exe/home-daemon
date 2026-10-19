import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { BudgetSummary } from '../api/types'
import { BudgetSummaryPage } from './BudgetSummaryPage'

const summary: BudgetSummary = {
  year: 2026,
  currency: 'EUR',
  totalIncomeMinor: 1000000,
  totalExpensesMinor: 1000000,
  proportionAvailable: true,
  incomeComplete: false,
  members: [
    {
      memberId: 'member-alice',
      name: 'Alice',
      annualIncomeMinor: 600000,
      share: 0.6,
      paidMinor: 1000000,
      dueMinor: 600000,
      transferredOutMinor: 0,
      transferredInMinor: 0,
      balanceMinor: 40000,
      missingMonths: [2, 3],
    },
    {
      memberId: 'member-bob',
      name: 'Bob',
      annualIncomeMinor: 400000,
      share: 0.4,
      paidMinor: 0,
      dueMinor: 400000,
      transferredOutMinor: 0,
      transferredInMinor: 0,
      balanceMinor: -40000,
      missingMonths: [],
    },
  ],
  byCategory: [{ categoryId: 'cat-food', name: 'Food', amountMinor: 1000000 }],
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

function stubApi(overrides: Partial<BudgetSummary> = {}) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url

      if (url.includes('/api/v1/budget/years')) {
        return jsonResponse([2026, 2025])
      }

      if (url.includes('/api/v1/budget/summary')) {
        return jsonResponse({ ...summary, ...overrides })
      }

      return jsonResponse([])
    }),
  )
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/budget']}>
        <BudgetSummaryPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('BudgetSummaryPage', () => {
  it('states who owes whom without nagging about incomplete income months', async () => {
    stubApi()
    renderPage()

    expect(await screen.findByText('Bob owes €400.00 to Alice')).toBeInTheDocument()
    expect(screen.queryByText(/provisional/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/months without income/i)).not.toBeInTheDocument()
  })

  it('shows each person share and balance', async () => {
    stubApi()
    renderPage()

    expect(await screen.findByText('60% of the year')).toBeInTheDocument()
    expect(screen.getByText('40% of the year')).toBeInTheDocument()
    expect(screen.getByText('€400.00')).toBeInTheDocument()
    expect(screen.queryByText(/months without income/i)).not.toBeInTheDocument()
  })

  it('explains when the shares cannot be computed yet', async () => {
    stubApi({
      proportionAvailable: false,
      members: summary.members.map((member) => ({
        ...member,
        share: null,
        dueMinor: null,
        balanceMinor: null,
      })),
    })
    renderPage()

    expect(await screen.findAllByText('No income recorded')).toHaveLength(2)
    expect(screen.getByText('Everything is settled for this year.')).toBeInTheDocument()
  })
})
