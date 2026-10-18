import type { ReactElement } from 'react'
import { render, type RenderOptions } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'

/** Renders a component with the providers every screen expects, and no query retries. */
export function renderWithProviders(ui: ReactElement, options: RenderOptions & { route?: string } = {}) {
  const { route = '/', ...renderOptions } = options

  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
    </QueryClientProvider>,
    renderOptions,
  )
}
