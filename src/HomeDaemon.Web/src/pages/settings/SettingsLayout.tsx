import { NavLink, Outlet } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useSession } from '../../auth/session-context'
import { PageHeader } from '../../components/PageHeader'

const tabs = [
  { to: '/settings/household', labelKey: 'settings.tabs.household' },
  { to: '/settings/rooms', labelKey: 'settings.tabs.rooms' },
  { to: '/settings/members', labelKey: 'settings.tabs.members' },
  { to: '/settings/budget/categories', labelKey: 'settings.tabs.budget' },
] as const

export function SettingsLayout() {
  const { t } = useTranslation()
  const { user } = useSession()
  const isAdmin = user?.roles.includes('Admin') ?? false

  return (
    <>
      <PageHeader title={t('settings.title')} subtitle={t('settings.subtitle')} />

      <nav className="tabs" aria-label={t('settings.title')}>
        {tabs.map((tab) => (
          <NavLink
            key={tab.to}
            to={tab.to}
            className={({ isActive }) => `tabs__link${isActive ? ' is-active' : ''}`}
          >
            {t(tab.labelKey)}
          </NavLink>
        ))}

        {isAdmin ? (
          <NavLink
            to="/settings/home-assistant-logs"
            className={({ isActive }) => `tabs__link${isActive ? ' is-active' : ''}`}
          >
            {t('settings.tabs.homeAssistantLogs')}
          </NavLink>
        ) : null}

        {isAdmin ? (
          <NavLink
            to="/settings/accounts"
            className={({ isActive }) => `tabs__link${isActive ? ' is-active' : ''}`}
          >
            {t('settings.tabs.accounts')}
          </NavLink>
        ) : null}
      </nav>

      <Outlet />
    </>
  )
}
