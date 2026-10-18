import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { App } from '../App'
import { SessionProvider } from './SessionProvider'

const user = {
  id: 'user-1',
  email: 'alice@example.test',
  displayName: 'Alice',
  householdMemberId: null,
  mustChangePassword: false,
  roles: ['Admin'],
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

/**
 * A fetch stub that behaves like the API around the session: `/auth/me` reports the signed-in
 * account until the sign-out call succeeds.
 */
function stubApi() {
  const state = { signedIn: true }

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url
      const method = (init?.method ?? 'GET').toUpperCase()

      if (url.endsWith('/api/v1/auth/me')) {
        return state.signedIn ? jsonResponse(user) : new Response(null, { status: 401 })
      }

      if (url.endsWith('/api/v1/auth/logout') && method === 'POST') {
        state.signedIn = false
        return new Response(null, { status: 204 })
      }

      if (url.endsWith('/api/v1/auth/csrf')) {
        return jsonResponse({ token: 'test-token' })
      }

      return jsonResponse([])
    }),
  )

  return state
}

function renderApp() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/chores']}>
        <SessionProvider>
          <App />
        </SessionProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('signing out', () => {
  it('returns to the sign-in screen', async () => {
    stubApi()
    const actor = userEvent.setup()
    renderApp()

    const signOut = await screen.findByRole('button', { name: 'Sign out' })
    await actor.click(signOut)

    // The sign-in form proves the app left the protected area instead of bouncing back.
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Sign out' })).not.toBeInTheDocument()
  })

  it('does not show the previous session while the sign-out is in flight', async () => {
    stubApi()
    const actor = userEvent.setup()
    renderApp()

    await actor.click(await screen.findByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.queryByText('alice@example.test')).not.toBeInTheDocument()
  })
})
