import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { SessionContext, type SessionValue } from './session-context'
import { RequireHousehold } from './RequireHousehold'
import type { CurrentUser } from '../api/types'

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

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

function sessionFor(roles: string[]): SessionValue {
  const user: CurrentUser = {
    id: 'user-1',
    email: 'admin@example.test',
    displayName: 'Admin',
    householdMemberId: null,
    mustChangePassword: false,
    roles,
  }

  return {
    user,
    isLoading: false,
    signIn: vi.fn(),
    signOut: vi.fn(),
    reload: vi.fn(),
  }
}

function stubApi({ householdExists }: { householdExists: boolean }) {
  const posts: unknown[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url
      const method = (init?.method ?? 'GET').toUpperCase()

      if (url.endsWith('/api/v1/auth/csrf')) {
        return jsonResponse({ token: 'test-token' })
      }

      if (url.endsWith('/api/v1/households') && method === 'POST') {
        posts.push(init?.body ? (JSON.parse(init.body as string) as unknown) : null)
        return jsonResponse(household, 201)
      }

      if (url.endsWith('/api/v1/household') && method === 'GET') {
        if (householdExists) {
          return jsonResponse(household)
        }

        return jsonResponse({ title: 'Not found', status: 404 }, 404)
      }

      return jsonResponse([])
    }),
  )

  return { posts }
}

function renderGate(roles: string[]) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <SessionContext.Provider value={sessionFor(roles)}>
        <MemoryRouter initialEntries={['/chores']}>
          <Routes>
            <Route element={<RequireHousehold />}>
              <Route path="/chores" element={<p>chores content</p>} />
            </Route>
            <Route path="/settings/members" element={<p>people content</p>} />
          </Routes>
        </MemoryRouter>
      </SessionContext.Provider>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('RequireHousehold', () => {
  it('renders the protected content when the household exists', async () => {
    stubApi({ householdExists: true })
    renderGate(['Admin'])

    expect(await screen.findByText('chores content')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Create your household' })).not.toBeInTheDocument()
  })

  it('opens the setup screen on a fresh instance and creates the household', async () => {
    const { posts } = stubApi({ householdExists: false })
    renderGate(['Admin'])

    expect(await screen.findByRole('heading', { name: 'Create your household' })).toBeInTheDocument()

    const actor = userEvent.setup()
    await actor.type(screen.getByLabelText('Household name'), 'Our home')
    await actor.click(screen.getByRole('button', { name: 'Create household' }))

    expect(posts).toHaveLength(1)
    expect(posts[0]).toMatchObject({ name: 'Our home', currency: 'EUR' })
    expect((posts[0] as { timeZoneId: string }).timeZoneId).not.toBe('')

    // Creating the household is followed by adding its people.
    expect(await screen.findByText('people content')).toBeInTheDocument()
  })

  it('asks a non-administrator to have the household created for them', async () => {
    stubApi({ householdExists: false })
    renderGate(['User'])

    expect(await screen.findByText(/administrator has to create the household/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Create household' })).not.toBeInTheDocument()
  })
})
