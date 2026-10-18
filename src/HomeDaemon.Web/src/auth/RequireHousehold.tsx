import { Outlet } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getHousehold, householdQueryKey } from '../api/household'
import { ErrorState } from '../components/ErrorState'
import { Spinner } from '../components/Spinner'
import { isNotFound } from '../lib/errors'
import { OnboardingPage } from '../pages/OnboardingPage'

/**
 * Gate for everything that needs the household. A missing household is the normal state on a
 * fresh instance, so it opens the first-run setup instead of an error; any other failure stays
 * a retryable error.
 */
export function RequireHousehold() {
  const { t } = useTranslation()

  const household = useQuery({
    queryKey: householdQueryKey,
    queryFn: ({ signal }) => getHousehold(signal),
    retry: false,
  })

  if (household.isPending) {
    return <Spinner label={t('app.loading')} />
  }

  if (isNotFound(household.error)) {
    return <OnboardingPage />
  }

  if (household.error) {
    return <ErrorState error={household.error} onRetry={() => void household.refetch()} />
  }

  return <Outlet />
}
