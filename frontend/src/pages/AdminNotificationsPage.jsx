import { Bell, CheckCheck, ShieldAlert } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import {
  getAdminNotifications,
  markAdminNotificationRead,
  markAllAdminNotificationsRead,
} from '../services/adminService.js'
import {
  getManagerNotifications,
  markManagerNotificationRead,
  markAllManagerNotificationsRead,
} from '../services/managerService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'

function AdminNotificationsPage({ roleArea = 'admin' }) {
  const [notifications, setNotifications] = useState(null)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    const load = roleArea === 'manager' ? getManagerNotifications : getAdminNotifications
    load(controller.signal).then(setNotifications).catch((requestError) => {
      if (requestError.name !== 'AbortError') setError(requestError.message)
    })
    return () => controller.abort()
  }, [roleArea])

  async function markRead(notification) {
    if (notification.isRead || saving) return
    setSaving(true)
    try {
      const request = roleArea === 'manager'
        ? markManagerNotificationRead
        : markAdminNotificationRead
      await request(notification.id)
      setNotifications((items) => items.map((item) =>
        item.id === notification.id ? { ...item, isRead: true } : item))
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setSaving(false)
    }
  }

  async function markAllRead() {
    if (saving || !notifications.some((item) => !item.isRead)) return
    setSaving(true)
    try {
      const request = roleArea === 'manager'
        ? markAllManagerNotificationsRead
        : markAllAdminNotificationsRead
      await request()
      setNotifications((items) => items.map((item) => ({ ...item, isRead: true })))
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setSaving(false)
    }
  }

  if (error) return <ErrorState message={error} />
  if (!notifications) return <LoadingState message="Loading notifications…" />

  const hasUnread = notifications.some((item) => !item.isRead)
  return (
    <>
      <section className="page-heading page-heading--action">
        <div><h2>Notifications</h2><p>System alerts, ticket escalations, account events, and assignment updates will appear here.</p></div>
        {hasUnread && <button className="button button--secondary" type="button" onClick={markAllRead} disabled={saving}><CheckCheck size={18} />Mark All as Read</button>}
      </section>
      <section className="panel">
        {notifications.length === 0
          ? <EmptyState title="No notifications yet" message="Duplicate review updates and other alerts will appear here." />
          : <div className="notification-list">{notifications.map((notification) => (
            <article className={`notification-item${notification.isRead ? '' : ' notification-item--unread'}`} key={notification.id}>
              <span className="notification-item__icon">{notification.type === 'DuplicateReview' ? <ShieldAlert size={20} /> : <Bell size={20} />}</span>
              <div className="notification-item__content">
                <h3>{notification.title}</h3>
                <p>{notification.message}</p>
                <small>{formatLocalDateTime(notification.createdDate)}</small>
                <div className="notification-item__actions">
                  {notification.ticketReferenceNumber && <Link to={`/${roleArea}/tickets/${notification.ticketReferenceNumber}`} onClick={() => markRead(notification)}>View Ticket</Link>}
                  {!notification.isRead && <button type="button" onClick={() => markRead(notification)} disabled={saving} aria-label={`Mark ${notification.title} as read`}>Mark as Read</button>}
                </div>
              </div>
            </article>
          ))}</div>}
      </section>
    </>
  )
}

export default AdminNotificationsPage
