import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useSession } from '../auth/session-context'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { BrandMark } from './BrandMark'

export function AppShell() {
  const { t } = useTranslation()
  const { user, signOut } = useSession()
  const isOnline = useOnlineStatus()
  const navigate = useNavigate()
  const [signingOut, setSigningOut] = useState(false)

  const handleSignOut = async () => {
    setSigningOut(true)
    try {
      await signOut()
      await navigate('/login', { replace: true })
    } finally {
      setSigningOut(false)
    }
  }

  return (
    <div className="app">
      <a className="skip-link" href="#main">
        {t('nav.label')}
      </a>

      <header className="app__header">
        <div className="app__bar">
          <NavLink to="/chores" className="app__brand">
            <BrandMark />
            <span className="app__brand-name">{t('app.name')}</span>
          </NavLink>

          {user ? (
            <div className="app__account">
              <span className="app__account-name" title={user.email}>
                {user.displayName ?? user.email}
              </span>
              <button
                type="button"
                className="button button--ghost"
                onClick={() => {
                  void handleSignOut()
                }}
                disabled={signingOut}
              >
                {t('account.signOut')}
              </button>
            </div>
          ) : null}
        </div>

        <nav className="app__nav" aria-label={t('nav.label')}>
          <NavLink to="/chores" className={({ isActive }) => `app__nav-link${isActive ? ' is-active' : ''}`}>
            {t('nav.chores')}
          </NavLink>
          <NavLink to="/planner" className={({ isActive }) => `app__nav-link${isActive ? ' is-active' : ''}`}>
            {t('nav.planner')}
          </NavLink>
          <NavLink to="/history" className={({ isActive }) => `app__nav-link${isActive ? ' is-active' : ''}`}>
            {t('nav.history')}
          </NavLink>
          <NavLink to="/budget" className={({ isActive }) => `app__nav-link${isActive ? ' is-active' : ''}`}>
            {t('nav.budget')}
          </NavLink>
          <NavLink to="/settings" className={({ isActive }) => `app__nav-link${isActive ? ' is-active' : ''}`}>
            {t('nav.settings')}
          </NavLink>
        </nav>
      </header>

      {!isOnline ? (
        <p className="banner banner--warning" role="status">
          {t('app.offline')}
        </p>
      ) : null}

      <main className="app__main" id="main">
        <Outlet />
      </main>
    </div>
  )
}
