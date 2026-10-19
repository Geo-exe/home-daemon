import { useEffect, useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useSession } from '../auth/session-context'
import { useOnlineStatus } from '../hooks/useOnlineStatus'
import { BrandMark } from './BrandMark'

const navItems = [
  { to: '/chores', labelKey: 'nav.chores' },
  { to: '/planner', labelKey: 'nav.planner' },
  { to: '/history', labelKey: 'nav.history' },
  { to: '/budget', labelKey: 'nav.budget' },
  { to: '/settings', labelKey: 'nav.settings' },
] as const

export function AppShell() {
  const { t } = useTranslation()
  const { user, signOut } = useSession()
  const isOnline = useOnlineStatus()
  const navigate = useNavigate()
  const [signingOut, setSigningOut] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)

  useEffect(() => {
    if (!menuOpen) {
      return
    }

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setMenuOpen(false)
      }
    }

    document.addEventListener('keydown', closeOnEscape)
    return () => document.removeEventListener('keydown', closeOnEscape)
  }, [menuOpen])

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

          <div className="app__bar-actions">
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

            <button
              type="button"
              className="app__menu-toggle"
              aria-expanded={menuOpen}
              aria-controls="app-nav"
              aria-label={menuOpen ? t('nav.closeMenu') : t('nav.openMenu')}
              onClick={() => setMenuOpen((open) => !open)}
            >
              <span className="app__menu-icon" aria-hidden="true">
                {menuOpen ? '×' : '☰'}
              </span>
              <span className="app__menu-label">{t('nav.menu')}</span>
            </button>
          </div>
        </div>

        <nav id="app-nav" className={`app__nav${menuOpen ? ' is-open' : ''}`} aria-label={t('nav.label')}>
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) => `app__nav-link${isActive ? ' is-active' : ''}`}
              onClick={() => setMenuOpen(false)}
            >
              {t(item.labelKey)}
            </NavLink>
          ))}
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
