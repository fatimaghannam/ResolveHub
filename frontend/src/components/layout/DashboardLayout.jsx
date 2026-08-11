import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import {
  Bell,
  ClipboardCheck,
  ChevronDown,
  CircleUserRound,
  FileClock,
  FilePlus2,
  Files,
  Inbox,
  LayoutDashboard,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  Moon,
  Sun,
  Tags,
  Ticket,
  Users,
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
  updateStoredUser,
} from '../../services/authStorage.js'
import UserAvatar from '../common/UserAvatar.jsx'
import '../../styles/dashboard.css'
import NotificationBell from './NotificationBell.jsx'
import { useTheme } from '../../theme/ThemeContext.jsx'

const employeeNavigation = [
  { id: 'dashboard', to: '/employee/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'tickets', to: '/employee/tickets', label: 'My Tickets', icon: Ticket },
  { id: 'create', to: '/employee/tickets/create', label: 'Create Ticket', icon: FilePlus2 },
  { id: 'notifications', to: '/employee/notifications', label: 'Notifications', icon: Bell },
]

const agentNavigation = [
  { id: 'dashboard', to: '/agent/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'tickets', to: '/agent/tickets/assigned', label: 'Assigned Tickets', icon: Ticket },
  { id: 'open-tickets', to: '/agent/tickets/open', label: 'Open Tickets', icon: Inbox },
  { id: 'notifications', to: '/agent/notifications', label: 'Notifications', icon: Bell },
]

const adminNavigation = [
  { id: 'dashboard', to: '/admin/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'tickets', to: '/admin/tickets', label: 'All Tickets', icon: Ticket },
  { id: 'my-tickets', to: '/admin/my-tickets', label: 'My Tickets', icon: Files },
  { id: 'create', to: '/admin/tickets/create', label: 'Create Ticket', icon: FilePlus2 },
  { id: 'assignments', to: '/admin/assignments', label: 'Ticket Assignments', icon: ClipboardCheck },
  { id: 'workload', to: '/admin/workload', label: 'Team Workload', icon: BarChart3 },
  { id: 'users', to: '/admin/users', label: 'Users', icon: Users },
  { id: 'categories', to: '/admin/categories', label: 'Categories', icon: Tags },
  { id: 'activity', to: '/admin/audit-log', label: 'System Audit Log', icon: FileClock },
  { id: 'notifications', to: '/admin/notifications', label: 'Notifications', icon: Bell },
]

const managerNavigation = [
  { id: 'dashboard', to: '/manager/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { id: 'tickets', to: '/manager/tickets', label: 'All Tickets', icon: Ticket },
  { id: 'assignments', to: '/manager/assignments', label: 'Ticket Assignments', icon: ClipboardCheck },
  { id: 'workload', to: '/manager/workload', label: 'Team Workload', icon: BarChart3 },
  { id: 'audit-log', to: '/manager/audit-log', label: 'System Audit Log', icon: FileClock },
  
  { id: 'notifications', to: '/manager/notifications', label: 'Notifications', icon: Bell },
]

function isNavigationActive(id, pathname, roleArea) {
  const base = `/${roleArea}`
  if (id === 'dashboard') return pathname === `${base}/dashboard`
  if (id === 'create') return pathname === `${base}/tickets/create`
  if (id === 'drafts') return pathname.startsWith(`${base}/tickets/drafts`)
  if (id === 'my-tickets') return pathname.startsWith(`${base}/my-tickets`)
  if (id === 'workload') return pathname.startsWith(`${base}/workload`)
  if (id === 'tickets' && roleArea === 'agent')
    return pathname === `${base}/tickets` || pathname === `${base}/tickets/assigned`
  if (id === 'open-tickets') return pathname === `${base}/tickets/open`
  if (id === 'tickets' && roleArea === 'admin') {
    return pathname.startsWith('/admin/tickets') &&
      pathname !== '/admin/tickets/create' &&
      !pathname.startsWith('/admin/tickets/drafts')
  }
  if (id === 'tickets' && roleArea === 'manager') {
    return pathname.startsWith('/manager/tickets')
  }
  if (id === 'users' && roleArea === 'admin') return pathname.startsWith('/admin/users')
  if (id === 'activity' && roleArea === 'admin') return pathname === '/admin/audit-log'
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
      '/manager/audit-log': 'System Audit Log',

      '/manager/notifications': 'Notifications',
      '/manager/profile': 'Profile',
    }
    if (pathname.startsWith('/manager/workload')) return 'Team Workload'
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
      '/admin/workload': 'Team Workload',
      '/admin/users': 'Users',
      '/admin/categories': 'Ticket Categories',
      '/admin/audit-log': 'System Audit Log',
      '/admin/notifications': 'Notifications',
      '/admin/profile': 'Profile',
    }
    if (pathname.startsWith('/admin/workload')) return 'Team Workload'
    return titles[pathname] ?? 'Dashboard'
  }
  if (roleArea === 'agent') {
    if (pathname === '/agent/dashboard') return 'Dashboard'
    if (pathname === '/agent/tickets' || pathname === '/agent/tickets/assigned') return 'Assigned Tickets'
    if (pathname === '/agent/tickets/open') return 'Open Tickets'
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
  return 'Notifications'
}

function DashboardLayout() {
  const { theme, toggleTheme, resetToAuthTheme } = useTheme()
  const [isDesktopCollapsed, setIsDesktopCollapsed] = useState(false)
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false)
  const [isAccountMenuOpen, setIsAccountMenuOpen] = useState(false)
  const accountMenuRef = useRef(null)
  const mainRef = useRef(null)
  const mainRectBeforeToggleRef = useRef(null)
  const mainTransitionRef = useRef(null)
  const navigate = useNavigate()
  const location = useLocation()
  const [user, setUser] = useState(() => getStoredAuth()?.user)
  const auth = { ...getStoredAuth(), user }
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

  useLayoutEffect(() => {
    const main = mainRef.current
    const previousRect = mainRectBeforeToggleRef.current
    mainRectBeforeToggleRef.current = null

    if (
      !main ||
      !previousRect ||
      window.matchMedia('(max-width: 820px)').matches ||
      window.matchMedia('(prefers-reduced-motion: reduce)').matches
    ) {
      return
    }

    const currentRect = main.getBoundingClientRect()
    const horizontalOffset = previousRect.left - currentRect.left

    if (Math.abs(horizontalOffset) < 1) return

    mainTransitionRef.current = main.animate(
      [
        { transform: `translate3d(${horizontalOffset}px, 0, 0)` },
        { transform: 'translate3d(0, 0, 0)' },
      ],
      {
        duration: 180,
        easing: 'cubic-bezier(.2, .8, .2, 1)',
      },
    )
  }, [isDesktopCollapsed])

  useEffect(() => () => mainTransitionRef.current?.cancel(), [])

  useEffect(() => {
    function closeAccountMenu(event) {
      if (event.type === 'keydown' && event.key !== 'Escape') return
      if (event.type === 'pointerdown' && accountMenuRef.current?.contains(event.target)) return
      setIsAccountMenuOpen(false)
    }
    function closeForOtherHeaderMenu(event) {
      if (event.detail !== 'account') setIsAccountMenuOpen(false)
    }
    document.addEventListener('pointerdown', closeAccountMenu)
    document.addEventListener('keydown', closeAccountMenu)
    window.addEventListener('resolvehub:header-menu-open', closeForOtherHeaderMenu)
    return () => {
      document.removeEventListener('pointerdown', closeAccountMenu)
      document.removeEventListener('keydown', closeAccountMenu)
      window.removeEventListener('resolvehub:header-menu-open', closeForOtherHeaderMenu)
    }
  }, [])

  useEffect(() => setIsAccountMenuOpen(false), [location.pathname])

  function toggleDesktopSidebar() {
    mainTransitionRef.current?.cancel()
    mainRectBeforeToggleRef.current = mainRef.current?.getBoundingClientRect() ?? null
    setIsDesktopCollapsed((current) => !current)
  }

  function logout() {
    setIsAccountMenuOpen(false)
    clearStoredAuth()
    resetToAuthTheme()
    navigate('/login', { replace: true })
  }

  function toggleAccountMenu() {
    setIsAccountMenuOpen((current) => {
      const next = !current
      if (next) window.dispatchEvent(new CustomEvent('resolvehub:header-menu-open', { detail: 'account' }))
      return next
    })
  }

  function updateUser(updates) {
    const updatedUser = updateStoredUser(updates)
    if (updatedUser) setUser(updatedUser)
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
            onClick={toggleDesktopSidebar}
            aria-label={isDesktopCollapsed ? 'Expand navigation' : 'Collapse navigation'}
            aria-expanded={!isDesktopCollapsed}
            aria-controls={sidebarId}
          >
            {isDesktopCollapsed ? <PanelLeftOpen size={16} /> : <PanelLeftClose size={16} />}
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
                data-tooltip={label}
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
      </aside>

      <div ref={mainRef} className="dashboard-main">
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
          <button
            className="theme-toggle"
            type="button"
            onClick={toggleTheme}
            aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
            aria-pressed={theme === 'dark'}
            title={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
          >
            {theme === 'dark' ? <Sun size={18} aria-hidden="true" /> : <Moon size={18} aria-hidden="true" />}
          </button>
          <NotificationBell roleArea={roleArea} />
          <div className="account-menu" ref={accountMenuRef}>
            <button
              className="topbar__user"
              type="button"
              onClick={toggleAccountMenu}
              aria-label={`Open ${fullName || roleLabel} account menu`}
              aria-haspopup="menu"
              aria-expanded={isAccountMenuOpen}
            >
              <UserAvatar
                className="avatar"
                firstName={user?.firstName}
                lastName={user?.lastName}
                imagePath={user?.profileImagePath}
                aria-hidden="true"
              />
              <span className="topbar__identity">
                <strong>{fullName || user?.email || roleLabel}</strong>
                <small>{roleLabel}</small>
              </span>
              <ChevronDown className={`account-menu__chevron ${isAccountMenuOpen ? 'account-menu__chevron--open' : ''}`} size={15} aria-hidden="true" />
            </button>
            {isAccountMenuOpen && <div className="account-dropdown" role="menu">
              <Link role="menuitem" to={`/${roleArea}/profile`} onClick={() => setIsAccountMenuOpen(false)}><CircleUserRound size={16} />Profile</Link>
              <button role="menuitem" className="account-dropdown__logout" type="button" onClick={logout}><LogOut size={16} />Logout</button>
            </div>}
          </div>
        </header>
        <div className="dashboard-scroll">
          <main className="dashboard-content">
            <Outlet context={{ user, role: roleLabel, updateUser }} />
          </main>
        </div>
      </div>
    </div>
  )
}

export default DashboardLayout
