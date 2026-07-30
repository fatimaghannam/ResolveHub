import { useEffect, useState } from 'react'
import {
  Bell,
  ClipboardCheck,
  CircleUserRound,
  FileClock,
  FilePlus2,
  Files,
  History,
  Inbox,
  LayoutDashboard,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  PlusCircle,
  Tags,
  Ticket,
  Users,
  Activity,
  BarChart3,
  X,
} from 'lucide-react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  clearStoredAuth,
  getStoredAuth,
  isAdministrator,
  isItAgent,
  isManager,
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
  { id: 'open-tickets', to: '/agent/tickets/open', label: 'Open Tickets', icon: Inbox },
  { id: 'ticket-history', to: '/agent/tickets/history', label: 'Ticket History', icon: History },
  { id: 'notifications', to: '/agent/notifications', label: 'Notifications', icon: Bell },
  { id: 'profile', to: '/agent/profile', label: 'Profile', icon: CircleUserRound },
]

const adminNavigation = [
  { id: 'dashboard', to: '/admin/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'tickets', to: '/admin/tickets', label: 'All Tickets', icon: Ticket },
  { id: 'my-tickets', to: '/admin/my-tickets', label: 'My Tickets', icon: Files },
  { id: 'create', to: '/admin/tickets/create', label: 'Create Ticket', icon: FilePlus2 },
  { id: 'assignments', to: '/admin/assignments', label: 'Ticket Assignments', icon: ClipboardCheck },
  { id: 'users', to: '/admin/users', label: 'Users', icon: Users },
  { id: 'categories', to: '/admin/categories', label: 'Categories', icon: Tags },
  { id: 'activity', to: '/admin/activity', label: 'Activity Logs', icon: FileClock },
  { id: 'notifications', to: '/admin/notifications', label: 'Notifications', icon: Bell },
  { id: 'profile', to: '/admin/profile', label: 'Profile', icon: CircleUserRound },
]

const managerNavigation = [
  { id: 'dashboard', to: '/manager/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'tickets', to: '/manager/tickets', label: 'All Tickets', icon: Ticket },
  { id: 'assignments', to: '/manager/assignments', label: 'Ticket Assignments', icon: ClipboardCheck },
  { id: 'workload', to: '/manager/workload', label: 'Team Workload', icon: BarChart3 },
  { id: 'activity', to: '/manager/activity', label: 'Activity', icon: Activity },
  { id: 'notifications', to: '/manager/notifications', label: 'Notifications', icon: Bell },
  { id: 'profile', to: '/manager/profile', label: 'Profile', icon: CircleUserRound },
]

function isNavigationActive(id, pathname, roleArea) {
  const base = `/${roleArea}`
  if (id === 'dashboard') return pathname === `${base}/dashboard`
  if (id === 'create') return pathname === `${base}/tickets/create`
  if (id === 'drafts') return pathname.startsWith(`${base}/tickets/drafts`)
  if (id === 'my-tickets') return pathname.startsWith(`${base}/my-tickets`)
  if (id === 'tickets' && roleArea === 'agent') return pathname === `${base}/tickets`
  if (id === 'open-tickets') return pathname === `${base}/tickets/open`
  if (id === 'ticket-history') return pathname === `${base}/tickets/history`
  if (id === 'tickets' && roleArea === 'admin') {
    return pathname.startsWith('/admin/tickets') &&
      pathname !== '/admin/tickets/create' &&
      !pathname.startsWith('/admin/tickets/drafts')
  }
  if (id === 'tickets' && roleArea === 'manager') {
    return pathname.startsWith('/manager/tickets')
  }
  if (id === 'users' && roleArea === 'admin') return pathname.startsWith('/admin/users')
  if (id === 'tickets') {
    return (
      pathname === '/employee/tickets' ||
      pathname.startsWith('/employee/tickets/drafts') ||
      /^\/employee\/tickets\/\d+(\/edit)?$/.test(pathname)
    )
  }
  return pathname === `${base}/${id}`
}

function getPageTitle(pathname, roleArea) {
  if (roleArea === 'manager') {
    if (/\/manager\/tickets\/[^/]+$/.test(pathname)) return 'Ticket Details'
    const titles = {
      '/manager/tickets': 'All Tickets',
      '/manager/assignments': 'Ticket Assignments',
      '/manager/workload': 'Team Workload',
      '/manager/activity': 'Ticket Activity',
      '/manager/notifications': 'Notifications',
      '/manager/profile': 'Profile',
    }
    return titles[pathname] ?? 'Dashboard'
  }
  if (roleArea === 'admin') {
    if (pathname.startsWith('/admin/my-tickets')) return 'My Tickets'
    if (pathname === '/admin/tickets/create') return 'Create Ticket'
    if (pathname.startsWith('/admin/tickets/drafts')) return 'Ticket Drafts'
    if (/\/admin\/tickets\/[^/]+$/.test(pathname)) return 'Ticket Details'
    if (/\/admin\/users\/[^/]+$/.test(pathname)) return 'User Details'
    const titles = {
      '/admin/tickets': 'All Tickets',
      '/admin/assignments': 'Ticket Assignments',
      '/admin/users': 'Users',
      '/admin/categories': 'Ticket Categories',
      '/admin/activity': 'Activity Logs',
      '/admin/notifications': 'Notifications',
      '/admin/profile': 'Profile',
    }
    return titles[pathname] ?? 'Dashboard'
  }
  if (roleArea === 'agent') {
    if (pathname === '/agent/dashboard') return 'Dashboard'
    if (pathname === '/agent/tickets') return 'Assigned Tickets'
    if (pathname === '/agent/tickets/open') return 'Open Tickets'
    if (pathname === '/agent/tickets/history') return 'Ticket History'
    if (/\/agent\/tickets\/[^/]+$/.test(pathname)) return 'Ticket Details'
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
  const admin = isAdministrator(auth)
  const manager = isManager(auth)
  const roleArea = admin ? 'admin' : manager ? 'manager' : agent ? 'agent' : 'employee'
  const navigation = admin ? adminNavigation : manager ? managerNavigation : agent ? agentNavigation : employeeNavigation
  const roleLabel = admin ? 'Administrator' : manager ? 'Manager' : agent ? 'IT Support Agent' : 'Employee'
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

  const pageTitle = getPageTitle(location.pathname, roleArea)

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
            const active = isNavigationActive(id, location.pathname, roleArea)
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
