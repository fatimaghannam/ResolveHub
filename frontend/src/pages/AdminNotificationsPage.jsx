import { Bell, ShieldAlert } from 'lucide-react'
import { adminNotifications } from '../data/index.js'
import { formatLocalDate } from '../utils/dateTime.js'

function AdminNotificationsPage() {
  return (
    <>
      <section className="page-heading"><h2>Notifications</h2><p>System alerts, ticket escalations, account events, and assignment updates will appear here.</p></section>
      <section className="panel"><div className="notification-list">{adminNotifications.map((notification, index) => <article className="notification-item" key={notification.id}><span className="notification-item__icon">{index === 0 ? <ShieldAlert size={20} /> : <Bell size={20} />}</span><div><h3>{notification.title}</h3><p>{notification.message}</p><small>{formatLocalDate(notification.timestamp)}</small></div></article>)}</div></section>
    </>
  )
}

export default AdminNotificationsPage
