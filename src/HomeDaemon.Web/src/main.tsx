import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ApiError } from './api/client'
import { App } from './App'
import { SessionProvider } from './auth/SessionProvider'
import { registerServiceWorker } from './pwa'
import './i18n'
import './styles/tokens.css'
import './styles/app.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Client-side problems (auth, validation, missing data) are not worth retrying.
      retry: (failureCount, error) => (error instanceof ApiError && error.status < 500 ? false : failureCount < 2),
      refetchOnWindowFocus: false,
      staleTime: 15_000,
    },
  },
})

const container = document.getElementById('root')
if (container === null) {
  throw new Error('The root container is missing from index.html.')
}

createRoot(container).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <SessionProvider>
          <App />
        </SessionProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)

registerServiceWorker()
