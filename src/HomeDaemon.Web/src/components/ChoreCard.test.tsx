import { describe, expect, it } from 'vitest'
import { screen } from '@testing-library/react'
import type { ChoreListItem } from '../api/types'
import { renderWithProviders } from '../test/render'
import { ChoreCard } from './ChoreCard'

const chore: ChoreListItem = {
  id: 'chore-1',
  title: 'Clean oven',
  roomId: 'room-1',
  isActive: true,
  estimatedMinutes: 30,
  frequency: 'Weekly',
  interval: 1,
  mode: 'Dynamic',
  assignmentPolicy: 'Both',
  fixedAssigneeMemberId: null,
  lastCompletedDate: null,
  nextOccurrenceDate: '2026-06-17',
  nextDueDate: '2026-06-15',
  plannedMemberId: 'member-1',
  isLocked: false,
  scheduleState: 'Scheduled',
  unscheduledReason: null,
}

function renderCard(overrides: Partial<ChoreListItem> = {}) {
  return renderWithProviders(
    <ul>
      <ChoreCard chore={{ ...chore, ...overrides }} roomName="Kitchen" memberName="Alice" timeZoneId="UTC" />
    </ul>,
  )
}

describe('ChoreCard', () => {
  it('links to the detail and shows room, duration and the planned date', () => {
    renderCard()

    expect(screen.getByRole('link', { name: 'Clean oven' })).toHaveAttribute('href', '/chores/chore-1')
    expect(screen.getByText('Kitchen')).toBeInTheDocument()
    expect(screen.getByText('30 min')).toBeInTheDocument()
    expect(screen.getByText(/Planned for:/)).toBeInTheDocument()
    expect(screen.getByText(/2026/)).toBeInTheDocument()
    expect(screen.getByText('Alice')).toBeInTheDocument()
  })

  it('offers to pause an active chore and to resume a paused one', () => {
    const { unmount } = renderCard()
    expect(screen.getByRole('button', { name: 'Pause' })).toBeInTheDocument()
    unmount()

    renderCard({ isActive: false, scheduleState: 'Paused' })
    expect(screen.getByRole('button', { name: 'Resume' })).toBeInTheDocument()
    expect(screen.getAllByText('Paused').length).toBeGreaterThan(0)
  })

  it('marks a chore that a manual change locked', () => {
    renderCard({ isLocked: true })

    expect(screen.getByText('Locked by a manual change')).toBeInTheDocument()
  })
})
