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
import {
  clearStoredAuth,
  getStoredAuth,
  isItAgent,
} from '../../services/authStorage.js'
import '../../styles/dashboard.css'

const employeeNavigation = [
  { id: 'dashboard', to: '/employee/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'tickets', to: '/employee/tickets', label: 'My Tickets', icon: Ticket },
  { id: 'create', to: '/employee/tickets/create', label: 'Create Ticket', icon: PlusCircle },
  { to: '/employee/coming-soon', label: 'Notifications', icon: Bell, soon: true },
  { to: '/employee/coming-soon', label: 'Profile', icon: CircleUserRound, soon: true },
]

const agentNavigation = [
  { id: 'dashboard', to: '/agent/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'tickets', to: '/agent/tickets', label: 'Assigned Tickets', icon: Ticket },
  { id: 'notifications', to: '/agent/notifications', label: 'Notifications', icon: Bell },
  { id: 'profile', to: '/agent/profile', label: 'Profile', icon: CircleUserRound },
]

function isNavigationActive(id, pathname, agent) {
  const base = agent ? '/agent' : '/employee'
  if (id === 'dashboard') return pathname === `${base}/dashboard`
  if (id === 'create') return pathname === `${base}/tickets/create`
  if (id === 'tickets' && agent) return pathname.startsWith('/agent/tickets')
  if (id === 'tickets') {
    return (
      pathname === '/employee/tickets' ||
      pathname.startsWith('/employee/tickets/drafts') ||
      /^\/employee\/tickets\/\d+(\/edit)?$/.test(pathname)
    )
  }
  return pathname === `${base}/${id}`
}

function getPageTitle(pathname, agent) {
  if (agent) {
    if (/\/agent\/tickets\/[^/]+$/.test(pathname)) return 'Ticket Details'
    if (pathname.endsWith('/tickets')) return 'Assigned Tickets'
    if (pathname.endsWith('/notifications')) return 'Notifications'
    if (pathname.endsWith('/profile')) return 'Profile'
    return 'Dashboard'
  }
  if (pathname.includes('/tickets/drafts')) return 'Ticket Drafts'
  if (pathname.includes('/tickets/create')) return 'Create Ticket'
  if (pathname.includes('/edit')) return 'Edit Ticket'
  if (pathname.match(/\/tickets\/\d+$/)) return 'Ticket Details'
  if (pathname.endsWith('/tickets')) return 'My Tickets'
  if (pathname.endsWith('/dashboard')) return 'Dashboard'
  return 'Coming Soon'
}

function DashboardLayout() {
  const [isDesktopCollapsed, setIsDesktopCollapsed] = useState(false)
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)
  const navigate = useNavigate()
  const location = useLocation()
  const auth = getStoredAuth()
  const user = auth?.user
  const agent = isItAgent(auth)
  const navigation = agent ? agentNavigation : employeeNavigation
  const roleLabel = agent ? 'IT Support Agent' : 'Employee'
  const fullName = [user?.firstName, user?.lastName].filter(Boolean).join(' ')
  const sidebarId = 'dashboard-sidebar'

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

  const pageTitle = getPageTitle(location.pathname, agent)

  return (
    <div className={`dashboard-shell ${isDesktopCollapsed ? 'dashboard-shell--collapsed' : ''}`}>
      <button
        className="mobile-backdrop"
        aria-label="Close navigation"
        onClick={() => setIsMobileSidebarOpen(false)}
        hidden={!isMobileSidebarOpen}
      />
      <aside
        id={sidebarId}
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
            aria-controls={sidebarId}
          >
            {isDesktopCollapsed ? <PanelLeftOpen size={20} /> : <PanelLeftClose size={20} />}
          </button>
          <button
            type="button"
            className="icon-button sidebar__mobile-close"
            onClick={() => setIsMobileSidebarOpen(false)}
            aria-label="Close navigation"
            aria-controls={sidebarId}
          >
            <X size={20} />
          </button>
        </div>
        <nav aria-label={`${roleLabel} navigation`}>
          {navigation.map(({ id, to, label, icon: Icon, soon }, index) => {
            const active = isNavigationActive(id, location.pathname, agent)
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
            aria-controls={sidebarId}
          >
            <Menu size={22} />
          </button>
          <h1>{pageTitle}</h1>
          <div className="topbar__user">
            <span className="avatar">{user?.firstName?.[0]?.toUpperCase() ?? roleLabel[0]}</span>
            <span>
              <strong>{fullName || user?.email || roleLabel}</strong>
              <small>{roleLabel}</small>
            </span>
          </div>
        </header>
        <main className="dashboard-content">
          <Outlet context={{ user, role: roleLabel }} />
        </main>
      </div>
    </div>
  )
}

export default DashboardLayout
