import { Bell, ShieldAlert } from 'lucide-react'
import { useEffect, useState } from 'react'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { getAdminNotifications } from '../services/adminService.js'
import { getManagerNotifications } from '../services/managerService.js'
import { formatLocalDate } from '../utils/dateTime.js'

function AdminNotificationsPage({ roleArea = 'admin' }) {
  const [notifications, setNotifications] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    const load = roleArea === 'manager' ? getManagerNotifications : getAdminNotifications
    load(controller.signal).then(setNotifications).catch((requestError) => {
      if (requestError.name !== 'AbortError') setError(requestError.message)
    })
    return () => controller.abort()
  }, [roleArea])

  if (error) return <ErrorState message={error} />
  if (!notifications) return <LoadingState message="Loading notifications…" />

  return (
    <>
      <section className="page-heading"><h2>Notifications</h2><p>System alerts, ticket escalations, account events, and assignment updates will appear here.</p></section>
      <section className="panel">{notifications.length === 0 ? <EmptyState title="No notifications yet" message="Duplicate review updates and other alerts will appear here." /> : <div className="notification-list">{notifications.map((notification) => <article className="notification-item" key={notification.id}><span className="notification-item__icon">{notification.type === 'DuplicateReview' ? <ShieldAlert size={20} /> : <Bell size={20} />}</span><div><h3>{notification.title}</h3><p>{notification.message}</p><small>{formatLocalDate(notification.createdDate)}</small></div></article>)}</div>}</section>
    </>
  )
}

export default AdminNotificationsPage
