import { describe, expect, it } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ChoreFormPage } from './ChoreFormPage'

function renderNewChoreForm() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/chores/new']}>
        <Routes>
          <Route path="/chores/new" element={<ChoreFormPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('ChoreFormPage recurrence fields', () => {
  it('offers the four recurrence units by name', () => {
    renderNewChoreForm()

    const frequency = screen.getByLabelText('Repeats')
    expect(within(frequency).getAllByRole('option').map((option) => option.textContent)).toEqual([
      'Daily',
      'Weekly',
      'Monthly',
      'Yearly',
    ])
  })

  it('names the unit the interval counts in, and follows the choice', async () => {
    const user = userEvent.setup()
    renderNewChoreForm()

    expect(screen.getByLabelText('Repeat every (days)')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Repeats'), 'Monthly')

    expect(screen.getByLabelText('Repeat every (months)')).toBeInTheDocument()
    expect(screen.queryByLabelText('Repeat every (days)')).not.toBeInTheDocument()
  })
})
