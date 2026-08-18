import { Bell } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { getNotifications, markAllNotificationsRead, markNotificationRead } from '../../services/notificationService.js'
import { notifyNotificationsChanged, subscribeToNotificationsChanged } from '../../services/notificationEvents.js'
import { formatRelativeTime } from '../../utils/dateTime.js'
import { notificationTarget } from '../../utils/notificationRoutes.js'

function NotificationBell({ roleArea }) {
  const navigate = useNavigate()
  const wrapperRef = useRef(null)
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState([])

  async function load() {
    try { setItems(await getNotifications(100)) } catch { }
  }

  useEffect(() => {
    load()
    const unsubscribe = subscribeToNotificationsChanged(load)
    function close(event) {
      if (!wrapperRef.current?.contains(event.target)) setOpen(false)
    }
    function closeForOtherHeaderMenu(event) {
      if (event.detail !== 'notifications') setOpen(false)
    }
    document.addEventListener('pointerdown', close)
    window.addEventListener('resolvehub:header-menu-open', closeForOtherHeaderMenu)
    return () => {
      unsubscribe()
      document.removeEventListener('pointerdown', close)
      window.removeEventListener('resolvehub:header-menu-open', closeForOtherHeaderMenu)
    }
  }, [])

  async function toggle() {
    const next = !open
    setOpen(next)
    if (next) {
      window.dispatchEvent(new CustomEvent('resolvehub:header-menu-open', { detail: 'notifications' }))
      await load()
    }
  }

  async function select(item) {
    if (!item.isRead) await markNotificationRead(item.id)
    setItems((current) => current.map((entry) => entry.id === item.id ? { ...entry, isRead: true } : entry))
    notifyNotificationsChanged()
    setOpen(false)
    navigate(notificationTarget(item, roleArea), { state: { from: 'notifications' } })
  }

  async function markAll() {
    await markAllNotificationsRead()
    setItems((current) => current.map((item) => ({ ...item, isRead: true })))
    notifyNotificationsChanged()
  }

  const unreadItems = items.filter((item) => !item.isRead)
  const unread = unreadItems.length
  return <div className="notification-bell" ref={wrapperRef}>
    <button className="notification-bell__button" type="button" aria-label={`Notifications${unread ? `, ${unread} unread` : ''}`} aria-expanded={open} onClick={toggle}>
      <Bell size={20} />{unread > 0 && <span>{unread > 9 ? '9+' : unread}</span>}
    </button>
    {open && <div className="notification-dropdown">
      <header><strong>Notifications</strong>{unread > 0 && <button type="button" onClick={markAll}>Mark all as read</button>}</header>
      <div>{unreadItems.length === 0 ? <div className="notification-dropdown__empty"><strong>You're all caught up.</strong><span>No unread notifications.</span></div> : unreadItems.slice(0, 5).map((item) =>
        <button className={item.isRead ? '' : 'is-unread'} type="button" key={item.id} onClick={() => select(item)}>
          {!item.isRead && <i className="notification-unread-dot" />}<span><strong>{item.title}</strong><time>{formatRelativeTime(item.createdDate)}</time></span>
        </button>)}</div>
      <Link to={`/${roleArea}/notifications`} onClick={() => setOpen(false)}>View all notifications</Link>
    </div>}
  </div>
}

export default NotificationBell
