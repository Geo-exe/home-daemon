import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import type { CurrentUser } from '../api/types'
import { SessionContext, type SessionValue } from '../auth/session-context'
import { AppShell } from './AppShell'

const user: CurrentUser = {
  id: 'user-1',
  email: 'alice@example.test',
  displayName: 'Alice',
  householdMemberId: 'member-alice',
  mustChangePassword: false,
  roles: ['User'],
}

function session(): SessionValue {
  return {
    user,
    isLoading: false,
    signIn: vi.fn(),
    signOut: vi.fn(),
    reload: vi.fn(),
  }
}

function renderShell() {
  return render(
    <SessionContext.Provider value={session()}>
      <MemoryRouter initialEntries={['/chores']}>
        <Routes>
          <Route element={<AppShell />}>
            <Route path="/chores" element={<p>chores content</p>} />
            <Route path="/planner" element={<p>planner content</p>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </SessionContext.Provider>,
  )
}

describe('AppShell', () => {
  it('opens and closes the mobile menu and leaves it on navigation', async () => {
    const actor = userEvent.setup()
    renderShell()

    const toggle = screen.getByRole('button', { name: 'Open the menu' })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')

    await actor.click(toggle)
    expect(screen.getByRole('button', { name: 'Close the menu' })).toHaveAttribute('aria-expanded', 'true')

    await actor.click(screen.getByRole('link', { name: 'Planner' }))

    expect(screen.getByText('planner content')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Open the menu' })).toHaveAttribute('aria-expanded', 'false')
  })

  it('lists every module in the navigation', () => {
    renderShell()

    const navigation = screen.getByRole('navigation', { name: 'Modules' })
    expect(navigation).toHaveTextContent('Chores')
    expect(navigation).toHaveTextContent('Planner')
    expect(navigation).toHaveTextContent('History')
    expect(navigation).toHaveTextContent('Budget')
    expect(navigation).toHaveTextContent('Settings')
  })
})
