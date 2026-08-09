import { Bell } from 'lucide-react'

function AgentNotificationsPage() {
  return (
    <>
      <section className="page-heading">
        <h2>Notifications</h2>
        <p>Ticket assignments, employee replies, status reminders, and priority alerts will appear here.</p>
      </section>
      <section className="state-panel">
        <span className="placeholder-icon"><Bell size={28} aria-hidden="true" /></span>
        <h2>No notifications yet</h2>
        <p>Notifications will be connected when the Agent notification APIs are available.</p>
      </section>
    </>
  )
}

export default AgentNotificationsPage
