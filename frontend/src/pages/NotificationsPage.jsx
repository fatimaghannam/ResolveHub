import { Bell, CheckCheck } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { getNotifications, markAllNotificationsRead, markNotificationRead } from '../services/notificationService.js'
import { notifyNotificationsChanged } from '../services/notificationEvents.js'
import { formatLocalDateTime } from '../utils/dateTime.js'
import { notificationTarget } from '../utils/notificationRoutes.js'

function NotificationsPage({ roleArea }) {
  const navigate = useNavigate()
  const [notifications, setNotifications] = useState(null)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    getNotifications(100, controller.signal).then(setNotifications).catch((requestError) => {
      if (requestError.name !== 'AbortError') setError(requestError.message)
    })
    return () => controller.abort()
  }, [])

  async function openNotification(notification) {
    if (saving) return
    if (!notification.isRead) {
      setSaving(true)
      try {
        await markNotificationRead(notification.id)
        setNotifications((items) => items.map((item) => item.id === notification.id
          ? { ...item, isRead: true } : item))
        notifyNotificationsChanged()
      } catch (requestError) {
        setError(requestError.message)
        return
      } finally {
        setSaving(false)
      }
    }
    navigate(notificationTarget(notification, roleArea), { state: { from: 'notifications' } })
  }

  async function markAllRead() {
    if (saving) return
    setSaving(true)
    try {
      await markAllNotificationsRead()
      setNotifications((items) => items.map((item) => ({ ...item, isRead: true })))
      notifyNotificationsChanged()
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setSaving(false)
    }
  }

  if (error) return <ErrorState message={error} />
  if (!notifications) return <LoadingState message="Loading notifications…" />
  const hasUnread = notifications.some((item) => !item.isRead)

  return <>
    <section className="page-heading page-heading--action">
      <div><h2>Notifications</h2><p>Updates and requests that directly need your attention appear here.</p></div>
      {hasUnread && <button className="button button--secondary" type="button" onClick={markAllRead} disabled={saving}><CheckCheck size={18} />Mark all as read</button>}
    </section>
    <section className="panel notifications-panel">
      {notifications.length === 0
        ? <EmptyState title="No notifications yet" message="Relevant ticket and request updates will appear here." />
        : <div className="notification-list">{notifications.map((notification) =>
          <button className={`notification-item notification-item--button${notification.isRead ? '' : ' notification-item--unread'}`} type="button" key={notification.id} onClick={() => openNotification(notification)} disabled={saving}>
            <span className="notification-item__icon"><Bell size={20} /></span>
            <span className="notification-item__content">
              <span className="notification-item__title">{!notification.isRead && <i className="notification-unread-dot" />}{notification.title}</span>
              <span className="notification-item__message">{notification.message}</span>
            </span>
            <time className="notification-item__time" dateTime={notification.createdDate}>{formatLocalDateTime(notification.createdDate)}</time>
          </button>)}</div>}
    </section>
  </>
}

export default NotificationsPage
