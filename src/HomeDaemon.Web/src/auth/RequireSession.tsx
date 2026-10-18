import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useSession } from '../auth/session-context'
import { Spinner } from '../components/Spinner'

/**
 * Gate for everything that needs a session. It also enforces the mandatory password change on
 * the client so the user is not sent to a screen the API would refuse; the server enforces the
 * same rule independently.
 */
export function RequireSession() {
  const { t } = useTranslation()
  const { user, isLoading } = useSession()
  const location = useLocation()

  if (isLoading) {
    return <Spinner label={t('app.loading')} />
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (user.mustChangePassword && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />
  }

  return <Outlet />
}
