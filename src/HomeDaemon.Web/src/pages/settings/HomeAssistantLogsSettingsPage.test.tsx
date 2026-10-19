import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { SessionProvider } from '../../auth/SessionProvider'
import { HomeAssistantLogsSettingsPage } from './HomeAssistantLogsSettingsPage'

const admin = {
  id: 'user-1',
  email: 'admin@example.test',
  displayName: 'Admin',
  householdMemberId: null,
  mustChangePassword: false,
  roles: ['Admin'],
}

const log = {
  id: 'log-1',
  occurredAtUtc: '2026-10-19T08:15:00Z',
  operation: 'todo.add_item',
  entityId: 'todo.alice',
  method: 'POST',
  status: 'Failed',
  statusCode: 401,
  durationMs: 12,
  errorMessage: 'Home Assistant returned 401 for todo/add_item.',
  isTransient: false,
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

function stubApi(roles: string[] = ['Admin']) {
  const requests: string[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url

      if (url.endsWith('/api/v1/auth/me')) {
        return jsonResponse({ ...admin, roles })
      }
      if (url.endsWith('/api/v1/auth/csrf')) {
        return jsonResponse({ token: 'test-token' })
      }
      if (url.includes('/api/v1/admin/home-assistant-logs')) {
        requests.push(url)
        const failedOnly = url.includes('status=Failed')
        return jsonResponse({ items: failedOnly ? [log] : [log], nextCursor: null, hasMore: false })
      }

      return jsonResponse({})
    }),
  )

  return { requests }
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <SessionProvider>
        <MemoryRouter initialEntries={['/settings/home-assistant-logs']}>
          <HomeAssistantLogsSettingsPage />
        </MemoryRouter>
      </SessionProvider>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('HomeAssistantLogsSettingsPage', () => {
  it('shows the recorded calls with their failure detail', async () => {
    stubApi()
    renderPage()

    expect(await screen.findByText(/todo\.add_item/)).toBeInTheDocument()
    expect(screen.getByText(/todo\.alice/)).toBeInTheDocument()
    expect(screen.getByText('Home Assistant returned 401 for todo/add_item.')).toBeInTheDocument()
    expect(screen.getAllByText('Failed').length).toBeGreaterThan(0)
  })

  it('filters by outcome', async () => {
    const { requests } = stubApi()
    const actor = userEvent.setup()
    renderPage()

    await screen.findByText(/todo\.add_item/)
    await actor.selectOptions(screen.getByLabelText('Outcome'), 'Failed')

    expect(requests.some((url) => url.includes('status=Failed'))).toBe(true)
  })

  it('hides itself from non-administrators', async () => {
    stubApi(['User'])
    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong')
    expect(screen.queryByLabelText('Outcome')).not.toBeInTheDocument()
  })
})
