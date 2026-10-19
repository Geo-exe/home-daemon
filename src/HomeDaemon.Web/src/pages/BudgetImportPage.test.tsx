import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { SessionProvider } from '../auth/SessionProvider'
import { BudgetImportPage } from './BudgetImportPage'

const currentUser = {
  id: 'user-1',
  email: 'admin@example.test',
  displayName: 'Admin',
  householdMemberId: null,
  mustChangePassword: false,
  roles: ['Admin'],
}

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
    id: 'member-ceci',
    name: 'Ceci',
    color: null,
    isAssignable: true,
    isBudgetParticipant: true,
    todoListEntityId: null,
    personEntityId: null,
    calendarEntityIds: [],
  },
  {
    id: 'member-fra',
    name: 'Fra',
    color: null,
    isAssignable: true,
    isBudgetParticipant: true,
    todoListEntityId: null,
    personEntityId: null,
    calendarEntityIds: [],
  },
]

const categories = [
  {
    id: 'cat-spesa',
    name: 'Spesa',
    sortOrder: 0,
    subcategories: [{ id: 'sub-cibo', categoryId: 'cat-spesa', name: 'Cibo', sortOrder: 0 }],
  },
  {
    id: 'cat-entertainment',
    name: 'Intrattenimento',
    sortOrder: 1,
    subcategories: [],
  },
]

const csv = [
  'Date,Category,SubCategory,Description,Value,Paid By,Planned',
  '03/02/2026,Spesa,Cibo,NowTV,"€ 7,99",Ceci,FALSE',
  '04/02/2026,Intrattenimento,Streaming,Netflix,"€ 10,00",Fra,FALSE',
].join('\n')

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
}

function stubApi() {
  const imports: unknown[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url
      const method = (init?.method ?? 'GET').toUpperCase()

      if (url.endsWith('/api/v1/auth/me')) {
        return jsonResponse(currentUser)
      }
      if (url.endsWith('/api/v1/auth/csrf')) {
        return jsonResponse({ token: 'test-token' })
      }
      if (url.endsWith('/api/v1/budget/imports') && method === 'POST') {
        imports.push(init?.body ? (JSON.parse(init.body as string) as unknown) : null)
        return jsonResponse({ imported: 2, skipped: 0 })
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

  return { imports }
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <SessionProvider>
        <MemoryRouter initialEntries={['/budget/import']}>
          <BudgetImportPage />
        </MemoryRouter>
      </SessionProvider>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('BudgetImportPage', () => {
  it('maps the template, previews the rows and imports them', async () => {
    const { imports } = stubApi()
    const actor = userEvent.setup()
    renderPage()

    const file = new File([csv], 'to_import.csv', { type: 'text/csv' })
    await actor.upload(await screen.findByLabelText('CSV file'), file)

    expect(await screen.findByText('2 rows, 0 with problems.')).toBeInTheDocument()
    expect(screen.getByText(/2026-02-03/).closest('li')).toHaveTextContent('Spesa')

    await actor.click(screen.getByRole('button', { name: 'Import 2 expenses' }))

    expect(await screen.findByText('Imported 2 expenses. 0 already existed and were skipped.')).toBeInTheDocument()
    expect(imports).toHaveLength(1)
    expect(imports[0]).toMatchObject({
      rows: [
        {
          line: 2,
          date: '2026-02-03',
          amountMinor: 799,
          category: 'Spesa',
          subcategory: 'Cibo',
          paidByMemberId: 'member-ceci',
          description: 'NowTV',
        },
        {
          line: 3,
          date: '2026-02-04',
          amountMinor: 1000,
          category: 'Intrattenimento',
          subcategory: 'Streaming',
          paidByMemberId: 'member-fra',
          description: 'Netflix',
        },
      ],
      categories: [
        { sourceName: 'Spesa', categoryId: 'cat-spesa', create: false },
        { sourceName: 'Intrattenimento', categoryId: 'cat-entertainment', create: false },
      ],
      subcategories: [
        { categorySourceName: 'Spesa', sourceName: 'Cibo', subcategoryId: 'sub-cibo', create: false },
        {
          categorySourceName: 'Intrattenimento',
          sourceName: 'Streaming',
          subcategoryId: null,
          create: true,
        },
      ],
    })
  })

  it('blocks the import while a person or a row is unresolved', async () => {
    stubApi()
    const actor = userEvent.setup()
    renderPage()

    const file = new File([csv], 'to_import.csv', { type: 'text/csv' })
    await actor.upload(await screen.findByLabelText('CSV file'), file)

    const personSelect = await screen.findByLabelText('Ceci')
    await actor.selectOptions(personSelect, '')

    expect(screen.getByRole('button', { name: 'Import 2 expenses' })).toBeDisabled()
    expect(screen.getByText(/Choose a person for: Ceci/)).toBeInTheDocument()
  })

  it('shows the forbidden state to non-administrators', async () => {
    stubApi()
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url
        if (url.endsWith('/api/v1/auth/me')) {
          return jsonResponse({ ...currentUser, roles: ['User'] })
        }
        if (url.endsWith('/api/v1/auth/csrf')) {
          return jsonResponse({ token: 'test-token' })
        }
        return jsonResponse([])
      }),
    )

    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent('You do not have access to this action.')
    expect(screen.queryByLabelText('CSV file')).not.toBeInTheDocument()
  })
})
