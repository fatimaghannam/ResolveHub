import { useEffect, useState } from 'react'
import {
  Bell,
  CircleUserRound,
  LayoutDashboard,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  PlusCircle,
  Ticket,
  X,
} from 'lucide-react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { clearStoredAuth, getStoredAuth } from '../../services/authStorage.js'
import '../../styles/dashboard.css'

const navigation = [
  { id: 'dashboard', to: '/employee/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'tickets', to: '/employee/tickets', label: 'My Tickets', icon: Ticket },
  { id: 'create', to: '/employee/tickets/create', label: 'Create Ticket', icon: PlusCircle },
  { to: '/employee/coming-soon', label: 'Notifications', icon: Bell, soon: true },
  { to: '/employee/coming-soon', label: 'Profile', icon: CircleUserRound, soon: true },
]

function isNavigationActive(id, pathname) {
  if (id === 'dashboard') return pathname === '/employee/dashboard'
  if (id === 'create') return pathname === '/employee/tickets/create'
  if (id === 'tickets') {
    return (
      pathname === '/employee/tickets' ||
      pathname.startsWith('/employee/tickets/drafts') ||
      /^\/employee\/tickets\/\d+(\/edit)?$/.test(pathname)
    )
  }
  return false
}

function DashboardLayout() {
  const [isDesktopCollapsed, setIsDesktopCollapsed] = useState(false)
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)
  const navigate = useNavigate()
  const location = useLocation()
  const auth = getStoredAuth()
  const user = auth?.user

  useEffect(() => {
    if (!isMobileSidebarOpen) return undefined

    const closeOnEscape = (event) => {
      if (event.key === 'Escape') setIsMobileSidebarOpen(false)
    }
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', closeOnEscape)

    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [isMobileSidebarOpen])

  function logout() {
    clearStoredAuth()
    navigate('/login', { replace: true })
  }

  const pageTitle = location.pathname.includes('/tickets/drafts')
    ? 'Ticket Drafts'
    : location.pathname.includes('/tickets/create')
    ? 'Create Ticket'
    : location.pathname.includes('/edit')
      ? 'Edit Ticket'
      : location.pathname.match(/\/tickets\/\d+$/)
        ? 'Ticket Details'
        : location.pathname.endsWith('/tickets')
          ? 'My Tickets'
          : location.pathname.endsWith('/dashboard')
            ? 'Dashboard'
            : 'Coming Soon'

  return (
    <div className={`dashboard-shell ${isDesktopCollapsed ? 'dashboard-shell--collapsed' : ''}`}>
      <button
        className="mobile-backdrop"
        aria-label="Close navigation"
        onClick={() => setIsMobileSidebarOpen(false)}
        hidden={!isMobileSidebarOpen}
      />
      <aside
        id="employee-sidebar"
        className={`sidebar ${isMobileSidebarOpen ? 'sidebar--open' : ''}`}
      >
        <div className="sidebar__brand">
          <img
            className="sidebar__logo"
            src="/favicon.png"
            alt="ResolveHub"
            draggable="false"
          />
          <span className="sidebar__wordmark sidebar-text">ResolveHub</span>
          <button
            type="button"
            className="icon-button sidebar__desktop-toggle"
            onClick={() => setIsDesktopCollapsed((current) => !current)}
            aria-label={isDesktopCollapsed ? 'Expand navigation' : 'Collapse navigation'}
            aria-expanded={!isDesktopCollapsed}
            aria-controls="employee-sidebar"
          >
            {isDesktopCollapsed ? <PanelLeftOpen size={20} /> : <PanelLeftClose size={20} />}
          </button>
          <button
            type="button"
            className="icon-button sidebar__mobile-close"
            onClick={() => setIsMobileSidebarOpen(false)}
            aria-label="Close navigation"
            aria-controls="employee-sidebar"
          >
            <X size={20} />
          </button>
        </div>
        <nav aria-label="Employee navigation">
          {navigation.map(({ id, to, label, icon: Icon, soon }, index) => {
            const active = isNavigationActive(id, location.pathname)
            return (
              <Link
                key={`${label}-${index}`}
                to={to}
                onClick={() => setIsMobileSidebarOpen(false)}
                aria-label={label}
                aria-current={active ? 'page' : undefined}
                title={isDesktopCollapsed ? label : undefined}
                className={`sidebar-link ${active ? 'sidebar-link--active' : ''}`}
              >
                <Icon size={19} aria-hidden="true" />
                <span className="sidebar-text">{label}</span>
                {soon && <small className="sidebar-text">Soon</small>}
              </Link>
            )
          })}
        </nav>
        <button
          type="button"
          className="sidebar-link sidebar__logout"
          onClick={logout}
          aria-label="Logout"
          title={isDesktopCollapsed ? 'Logout' : undefined}
        >
          <LogOut size={19} aria-hidden="true" />
          <span className="sidebar-text">Logout</span>
        </button>
      </aside>

      <div className="dashboard-main">
        <header className="topbar">
          <button
            className="icon-button topbar__menu"
            onClick={() => setIsMobileSidebarOpen(true)}
            aria-label="Open navigation"
            aria-expanded={isMobileSidebarOpen}
            aria-controls="employee-sidebar"
          >
            <Menu size={22} />
          </button>
          <h1>{pageTitle}</h1>
          <div className="topbar__user">
            <span className="avatar">{user?.firstName?.[0] ?? 'E'}</span>
            <span>
              <strong>{user ? `${user.firstName} ${user.lastName}` : 'Employee'}</strong>
              <small>Employee</small>
            </span>
          </div>
        </header>
        <main className="dashboard-content">
          <Outlet context={{ user }} />
        </main>
      </div>
    </div>
  )
}

export default DashboardLayout
