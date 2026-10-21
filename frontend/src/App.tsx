import { BrowserRouter, Link, NavLink, Route, Routes } from 'react-router'
import { AppIcon } from './components/AppIcon'
import { PageHeader } from './components/PageHeader'
import { OverviewPage } from './pages/OverviewPage'
import { TicketsPage } from './pages/TicketsPage'
import { TicketDetailsPage } from './pages/TicketDetailsPage'
import { LoginPage } from './pages/LoginPage'
import { useSession } from './auth/useSession'
import { setSession } from './auth/session'
import { useEffect } from 'react'
import './App.css'

function NotFoundPage() {
  return (
    <>
      <PageHeader eyebrow="PAGE NOT FOUND" title="Let's get you back" description="This page does not exist." />
      <Link className="button button-primary" to="/">Return to overview</Link>
    </>
  )
}

function App() {
  const session = useSession()
  useEffect(() => {
    if (!session) return
    const timer = setTimeout(() => setSession(null), Math.max(0, Date.parse(session.expiresAt) - Date.now()))
    return () => clearTimeout(timer)
  }, [session])
  if (!session) return <LoginPage />

  return (
    <BrowserRouter>
      <a className="skip-link" href="#main-content">Skip to main content</a>
      <div className="app-shell">
        <aside className="sidebar">
          <Link className="brand" to="/" aria-label="ServiceDesk overview">
            <span className="brand-mark"><AppIcon name="brand" /></span>
            <span>ServiceDesk<span className="brand-caption">STORE OPERATIONS</span></span>
          </Link>
          <div className="nav-heading">WORKSPACE</div>
          <nav className="primary-nav" aria-label="Main navigation">
            <NavLink to="/" end><AppIcon name="overview" />Overview</NavLink>
            <NavLink to="/tickets"><AppIcon name="tickets" />Tickets</NavLink>
          </nav>
          <div className="sidebar-footer">
            <span className="footer-mark"><AppIcon name="brand" /></span>
            <div><strong>Better maintenance.</strong><span>One request at a time.</span></div>
          </div>
        </aside>
        <div className="workspace">
          <header className="topbar">
            <span>Store maintenance workspace</span>
            <div className="session-actions"><span>{session.user.name} · {session.user.role === 'StoreManager' ? 'Store manager' : 'Admin'}</span>
              <button className="button button-secondary" type="button" onClick={() => setSession(null)}>Sign out</button></div>
          </header>
          <main id="main-content" className="main-content" tabIndex={-1}>
            <Routes>
              <Route path="/" element={<OverviewPage />} />
              <Route path="/tickets" element={<TicketsPage />} />
              <Route path="/tickets/:ticketId" element={<TicketDetailsPage />} />
              <Route path="*" element={<NotFoundPage />} />
            </Routes>
          </main>
          <footer className="workspace-footer">ServiceDesk <span>Maintenance, made manageable.</span></footer>
        </div>
      </div>
    </BrowserRouter>
  )
}

export default App
