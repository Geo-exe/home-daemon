import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ReactNode } from 'react'
import { renderHook } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { budgetIncomesQueryKey, budgetSummaryQueryKey } from '../api/budget'
import { useUpsertBudgetIncome } from './useBudgetMutations'

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

function stubApi() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url
      const method = (init?.method ?? 'GET').toUpperCase()

      if (url.endsWith('/api/v1/auth/csrf')) {
        return jsonResponse({ token: 'test-token' })
      }

      if (url.endsWith('/api/v1/budget/incomes') && method === 'PUT') {
        return jsonResponse({ month: 3, amountMinor: 1000, incomeId: 'income-1' })
      }

      return jsonResponse([])
    }),
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useUpsertBudgetIncome', () => {
  it('invalidates the whole budget, the summary and the income grid of that year', async () => {
    stubApi()
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    )

    const { result } = renderHook(() => useUpsertBudgetIncome(), { wrapper })

    await result.current.mutateAsync({ memberId: 'member-alice', year: 2026, month: 3, amountMinor: 1000 })

    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['budget'] })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: budgetSummaryQueryKey(2026) })
    expect(invalidate).toHaveBeenCalledWith({ queryKey: budgetIncomesQueryKey(2026) })
  })
})
