import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { SessionContext, type SessionValue } from '../../auth/session-context'
import type { CurrentUser } from '../../api/types'
import { SettingsLayout } from './SettingsLayout'

function sessionFor(roles: string[]): SessionValue {
  const user: CurrentUser = {
    id: 'user-1',
    email: 'alice@example.test',
    displayName: 'Alice',
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

function renderLayout(roles: string[]) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <SessionContext.Provider value={sessionFor(roles)}>
        <MemoryRouter initialEntries={['/settings/household']}>
          <Routes>
            <Route path="/settings" element={<SettingsLayout />}>
              <Route path="household" element={<p>household content</p>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </SessionContext.Provider>
    </QueryClientProvider>,
  )
}

describe('SettingsLayout', () => {
  it('shows the shared tabs and renders the active child', () => {
    renderLayout(['User'])

    expect(screen.getByRole('link', { name: 'Household' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Rooms' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'People' })).toBeInTheDocument()
    expect(screen.getByText('household content')).toBeInTheDocument()
  })

  it('hides the administrator tabs from people who are not administrators', () => {
    renderLayout(['User'])

    expect(screen.queryByRole('link', { name: 'Accounts' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Home Assistant logs' })).not.toBeInTheDocument()
  })

  it('offers the administrator tabs to administrators', () => {
    renderLayout(['Admin'])

    expect(screen.getByRole('link', { name: 'Accounts' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Home Assistant logs' })).toBeInTheDocument()
  })
})
