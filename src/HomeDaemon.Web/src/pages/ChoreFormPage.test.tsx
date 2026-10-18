import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ChoreDetail } from '../api/types'
import { ChoreFormPage } from './ChoreFormPage'

const existingChore: ChoreDetail = {
  id: 'chore-1',
  householdId: 'household-1',
  title: 'Clean oven',
  description: null,
  roomId: null,
  isActive: true,
  estimatedMinutes: 30,
  effortWeight: null,
  preferSameRoomGrouping: false,
  frequency: 'Weekly',
  interval: 2,
  mode: 'Dynamic',
  anchorDate: null,
  assignmentPolicy: 'Both',
  fixedAssigneeMemberId: null,
  lastCompletedDate: null,
  lastCompletedByMemberId: null,
  nextOccurrenceDate: null,
  nextDueDate: null,
  plannedMemberId: null,
  isLocked: false,
  scheduleState: 'Unplanned',
  unscheduledReason: null,
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: '2026-06-01T00:00:00Z',
  recentCompletions: [],
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

function stubApi(chore: ChoreDetail = existingChore) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url

      if (url.includes('/api/v1/chores/')) {
        return jsonResponse(chore)
      }

      return jsonResponse([])
    }),
  )
}

function renderForm(route: string, path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path={path} element={<ChoreFormPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function advancedSection(): HTMLDetailsElement | null {
  return screen.getByText('More options').closest('details')
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ChoreFormPage structure', () => {
  it('groups the fields into labelled sections', () => {
    stubApi()
    renderForm('/chores/new', '/chores/new')

    expect(screen.getByRole('heading', { name: 'Basics' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Repeat' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Assignment' })).toBeInTheDocument()
    expect(screen.getByText('More options')).toBeInTheDocument()
  })

  it('keeps the once-per-chore options collapsed for a new chore', () => {
    stubApi()
    renderForm('/chores/new', '/chores/new')

    expect(advancedSection()?.open).toBe(false)
    // The section is exposed as a named group, so assistive technology announces it.
    expect(screen.getByRole('group', { name: 'Basics' })).toBeInTheDocument()
  })

  it('keeps the save action available at the bottom of the form', () => {
    stubApi()
    renderForm('/chores/new', '/chores/new')

    expect(screen.getByRole('button', { name: 'Create chore' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Cancel' })).toBeInTheDocument()
  })

  it('stays open after being expanded and then edited', async () => {
    stubApi()
    const actor = userEvent.setup()
    renderForm('/chores/new', '/chores/new')

    await actor.click(screen.getByText('More options'))
    expect(advancedSection()?.open).toBe(true)

    // A later render triggered by another field must not snap the section shut again.
    await actor.type(screen.getByLabelText('Title'), 'Clean oven')
    expect(advancedSection()?.open).toBe(true)
  })

  it('opens the advanced section when the chore already uses one of its options', async () => {
    stubApi({ ...existingChore, effortWeight: 1.5 })
    renderForm('/chores/chore-1/edit', '/chores/:choreId/edit')

    await screen.findByLabelText('Repeats')
    expect(advancedSection()?.open).toBe(true)
    expect(screen.getByLabelText('Effort weight')).toHaveValue(1.5)
  })

  it('leaves the advanced section closed for a chore that does not use those options', async () => {
    stubApi()
    renderForm('/chores/chore-1/edit', '/chores/:choreId/edit')

    await screen.findByLabelText('Repeats')
    expect(advancedSection()?.open).toBe(false)
  })
})

describe('ChoreFormPage recurrence fields', () => {
  it('offers the four recurrence units by name', () => {
    stubApi()
    renderForm('/chores/new', '/chores/new')

    const frequency = screen.getByLabelText('Repeats')
    expect(within(frequency).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'Daily',
      'Weekly',
      'Monthly',
      'Yearly',
    ])
  })

  it('names the unit the interval counts in, and follows the choice', async () => {
    stubApi()
    const actor = userEvent.setup()
    renderForm('/chores/new', '/chores/new')

    expect(screen.getByLabelText('Repeat every (days)')).toBeInTheDocument()

    await actor.selectOptions(screen.getByLabelText('Repeats'), 'Monthly')

    expect(screen.getByLabelText('Repeat every (months)')).toBeInTheDocument()
    expect(screen.queryByLabelText('Repeat every (days)')).not.toBeInTheDocument()
  })

  it('only asks for an anchor date when the schedule is fixed', async () => {
    stubApi()
    const actor = userEvent.setup()
    renderForm('/chores/new', '/chores/new')

    expect(screen.queryByLabelText('Anchored on')).not.toBeInTheDocument()

    await actor.selectOptions(screen.getByLabelText('Counting'), 'Anchored')

    expect(screen.getByLabelText('Anchored on')).toBeInTheDocument()
  })
})
