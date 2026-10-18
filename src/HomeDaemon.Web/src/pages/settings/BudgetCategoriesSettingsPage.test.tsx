import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BudgetCategoriesSettingsPage } from './BudgetCategoriesSettingsPage'

const categories = [
  {
    id: 'cat-food',
    name: 'Food',
    sortOrder: 0,
    subcategories: [
      { id: 'sub-groceries', categoryId: 'cat-food', name: 'Groceries', sortOrder: 0 },
      { id: 'sub-market', categoryId: 'cat-food', name: 'Market', sortOrder: 1 },
    ],
  },
  { id: 'cat-home', name: 'Home', sortOrder: 1, subcategories: [] },
]

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

function stubApi() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url

      if (url.endsWith('/api/v1/budget/categories')) {
        return jsonResponse(categories)
      }

      return jsonResponse([])
    }),
  )
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

  return render(
    <QueryClientProvider client={queryClient}>
      <BudgetCategoriesSettingsPage />
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('BudgetCategoriesSettingsPage', () => {
  it('summarises each category with the number of its subcategories', async () => {
    stubApi()
    renderPage()

    expect(await screen.findByText('2 subcategories · order 0')).toBeInTheDocument()
    expect(screen.getByText('0 subcategories · order 1')).toBeInTheDocument()
  })

  it('nests the subcategories in a named list under their category', async () => {
    stubApi()
    renderPage()

    const list = await screen.findByRole('list', { name: 'Subcategories of Food' })

    expect(within(list).getByText('Groceries')).toBeInTheDocument()
    expect(within(list).getByText('Market')).toBeInTheDocument()
    expect(within(list).getAllByRole('listitem')).toHaveLength(2)
  })

  it('says when a category has no subcategories yet', async () => {
    stubApi()
    renderPage()

    expect(await screen.findByText('No subcategories yet.')).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Subcategories of Home' })).not.toBeInTheDocument()
  })

  it('adds a subcategory in the context of its category', async () => {
    stubApi()
    const actor = userEvent.setup()
    renderPage()

    const food = (await screen.findByText('Food')).closest('li')!
    await actor.click(within(food).getByRole('button', { name: 'Add subcategory' }))

    const dialog = await screen.findByRole('dialog', { name: 'Add subcategory' })
    expect(within(dialog).getByText('Food')).toBeInTheDocument()
  })

  it('confirms the deletion of a single subcategory', async () => {
    stubApi()
    const actor = userEvent.setup()
    renderPage()

    const list = await screen.findByRole('list', { name: 'Subcategories of Food' })
    const groceries = within(list).getByText('Groceries').closest('li')!
    await actor.click(within(groceries).getByRole('button', { name: 'Delete' }))

    expect(await screen.findByRole('dialog', { name: 'Delete “Groceries”?' })).toBeInTheDocument()
  })
})
