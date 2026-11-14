import {
  BrowserRouter,
  Link,
  NavLink,
  Route,
  Routes,
  useLocation,
} from 'react-router'
import { useEffect, useRef, useState } from 'react'
import { AppIcon } from './components/AppIcon'
import { ActionMenu } from './components/ActionMenu'
import { PageHeader } from './components/PageHeader'
import { OverviewPage } from './pages/OverviewPage'
import { TicketsPage } from './pages/TicketsPage'
import { TicketDetailsPage } from './pages/TicketDetailsPage'
import { AdministrationPage } from './pages/AdministrationPage'
import { LoginPage } from './pages/LoginPage'
import { useSession } from './auth/useSession'
import { setSession } from './auth/session'
import { displayName } from './utils/displayNames'
import { AccountPage } from './pages/AccountPage'
import { NotificationInbox } from './components/NotificationInbox'
import './App.css'

function Navigation() {
  const session = useSession()!
  return (
    <>
      <Link className="brand" to="/" aria-label="ServiceDesk overview">
        <span className="brand-mark">
          <AppIcon name="brand" />
        </span>
        <span>
          ServiceDesk<span className="brand-caption">STORE OPERATIONS</span>
        </span>
      </Link>
      <div className="nav-heading">WORKSPACE</div>
      <nav className="primary-nav" aria-label="Workspace navigation">
        <NavLink to="/" end>
          <AppIcon name="overview" />
          Overview
        </NavLink>
        <NavLink to="/tickets">
          <AppIcon name="tickets" />
          Tickets
        </NavLink>
      </nav>
      {session.user.role === 'Admin' && (
        <>
          <div className="nav-heading">ADMINISTRATION</div>
          <nav className="primary-nav" aria-label="Administration navigation">
            <NavLink to="/admin/stores">
              <AppIcon name="store" />
              Stores
            </NavLink>
            <NavLink to="/admin/store-managers">
              <AppIcon name="users" />
              Store managers
            </NavLink>
            <NavLink to="/admin/technicians">
              <AppIcon name="tools" />
              Technicians
            </NavLink>
          </nav>
        </>
      )}
      <div className="sidebar-footer">
        <span className="workspace-dot" />
        <div>
          <strong>Store operations</strong>
          <span>Your maintenance workspace</span>
        </div>
      </div>
    </>
  )
}

function Workspace() {
  const session = useSession()!
  const location = useLocation()
  const [navPath, setNavPath] = useState<string | null>(null)
  const navOpen = navPath === location.pathname
  function setNavOpen(open: boolean) {
    setNavPath(open ? location.pathname : null)
  }
  const mobileNav = useRef<HTMLDialogElement>(null)
  const navTrigger = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    document.getElementById('main-content')?.focus()
  }, [location.pathname])
  useEffect(() => {
    const dialog = mobileNav.current!
    if (navOpen) dialog.showModal()
    else if (dialog.open) {
      dialog.close()
      navTrigger.current?.focus()
    }
  }, [navOpen])
  return (
    <>
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      <div className="app-shell">
        <aside className="sidebar">
          <Navigation />
        </aside>
        <dialog
          ref={mobileNav}
          className="mobile-nav"
          onCancel={(event) => {
            event.preventDefault()
          }}
        >
          <button
            className="button button-icon button-ghost mobile-nav-close"
            aria-label="Close navigation"
            onClick={() => setNavOpen(false)}
          >
            <AppIcon name="close" />
          </button>
          {navOpen && <Navigation />}
        </dialog>
        <div className="workspace">
          <header className="topbar">
            <div className="workspace-context">
              <button
                ref={navTrigger}
                className="button button-icon button-ghost mobile-nav-trigger"
                aria-label="Open navigation"
                aria-expanded={navOpen}
                onClick={() => setNavOpen(true)}
              >
                <AppIcon name="menu" />
              </button>
              <span className="context-icon">
                <AppIcon name="store" />
              </span>
              <span>
                {session.user.role === 'Admin'
                  ? 'All stores'
                  : displayName(
                      session.user.storeName,
                      `Store #${session.user.storeId}`,
                    )}
                <small>
                  {session.user.role === 'Admin'
                    ? 'Administration workspace'
                    : 'Store workspace'}
                </small>
              </span>
            </div>
            <div className="session-actions">
              <NotificationInbox />
              <span className="topbar-divider" />
              <ActionMenu
                label="Open user menu"
                className="user-menu"
                trigger={
                  <>
                    <span className="avatar">
                      {session.user.name
                        .split(' ')
                        .map((n) => n[0])
                        .slice(0, 2)
                        .join('')}
                    </span>
                    <span className="user-identity">
                      {session.user.name}
                      <small>
                        {session.user.role === 'Admin'
                          ? 'Administrator'
                          : 'Store manager'}
                      </small>
                    </span>
                    <span className="chevron">⌄</span>
                  </>
                }
              >
                <p className="menu-label">Your workspace</p>
                <Link to="/account">
                  <AppIcon name="user" />
                  Account settings
                </Link>
                <button type="button" onClick={() => setSession(null)}>
                  Sign out
                </button>
              </ActionMenu>
            </div>
          </header>
          <main id="main-content" className="main-content" tabIndex={-1}>
            <Routes>
              <Route path="/" element={<OverviewPage />} />
              <Route path="/account" element={<AccountPage />} />
              <Route path="/tickets" element={<TicketsPage />} />
              <Route
                path="/tickets/:ticketId"
                element={<TicketDetailsPage />}
              />
              <Route
                path="/admin/store-managers"
                element={<AdministrationPage kind="store-managers" />}
              />
              <Route
                path="/admin/stores"
                element={<AdministrationPage kind="stores" />}
              />
              <Route
                path="/admin/technicians"
                element={<AdministrationPage kind="external-technicians" />}
              />
              <Route
                path="*"
                element={
                  <>
                    <PageHeader
                      eyebrow="PAGE NOT FOUND"
                      title="This page is unavailable"
                      description="Check the address or return to your workspace."
                    />
                    <Link className="button button-primary" to="/">
                      Return to overview
                    </Link>
                  </>
                }
              />
            </Routes>
          </main>
          <footer className="workspace-footer">
            ServiceDesk<span>Store maintenance, connected.</span>
          </footer>
        </div>
      </div>
    </>
  )
}
function App() {
  const session = useSession()
  const [expired, setExpired] = useState(false)
  useEffect(() => {
    if (!session) return
    const timer = setTimeout(
      () => {
        setExpired(true)
        setSession(null)
      },
      Math.max(0, Date.parse(session.expiresAt) - Date.now()),
    )
    return () => clearTimeout(timer)
  }, [session])
  return !session ? (
    <LoginPage expired={expired} />
  ) : (
    <BrowserRouter>
      <Workspace />
    </BrowserRouter>
  )
}
export default App
