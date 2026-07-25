import { useEffect, useState } from 'react'
import { CheckCircle2, Clock3, FolderOpen, Ticket } from 'lucide-react'
import { Link, useOutletContext } from 'react-router-dom'
import { getDashboard } from '../services/ticketService.js'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'

function EmployeeDashboardPage() {
  const { user } = useOutletContext()
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    getDashboard(controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setData(result)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) setError(requestError.message)
      })
    return () => controller.abort()
  }, [reload])

  if (error) return <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />
  if (!data) return <LoadingState message="Loading your dashboard…" />

  const cards = [
    ['Total Tickets', data.totalTickets, Ticket, 'blue'],
    ['Open', data.openTickets, FolderOpen, 'cyan'],
    ['In Progress', data.inProgressTickets, Clock3, 'amber'],
    ['Resolved', data.resolvedTickets, CheckCircle2, 'green'],
  ]

  return (
    <>
      <section className="page-heading page-heading--action">
        <div><h2>Welcome back, {user?.firstName ?? 'Employee'}</h2><p>Here is an overview of your recent support activity.</p></div>
        <Link className="button button--primary" to="/employee/tickets/create">Create Ticket</Link>
      </section>
      <section className="stat-grid" aria-label="Ticket statistics">
        {cards.map(([label, value, Icon, tone]) => (
          <article className="stat-card" key={label}>
            <span className={`stat-card__icon tone-${tone}`}><Icon size={22} /></span>
            <span><small>{label}</small><strong>{value}</strong></span>
          </article>
        ))}
      </section>
      <section className="panel">
        <div className="panel__heading"><div><h2>Recent Tickets</h2><p>Your five most recently created tickets.</p></div><Link to="/employee/tickets">View all tickets</Link></div>
        {data.recentTickets.length === 0 ? (
          <EmptyState title="No tickets yet" message="Create your first support ticket when you need help." action={<Link className="button button--primary" to="/employee/tickets/create">Create Ticket</Link>} />
        ) : (
          <div className="table-scroll">
            <table className="ticket-table">
              <thead><tr><th>Ticket</th><th>Title</th><th>Priority</th><th>Status</th><th>Created</th><th /></tr></thead>
              <tbody>{data.recentTickets.map((ticket) => (
                <tr key={ticket.id}>
                  <td><strong>{ticket.ticketReferenceNumber}</strong></td><td>{ticket.title}</td>
                  <td><TicketPriorityBadge value={ticket.priorityName} /></td>
                  <td><TicketStatusBadge value={ticket.statusName} /></td>
                  <td>{new Date(ticket.createdDate).toLocaleDateString()}</td>
                  <td><Link className="table-action" to={`/employee/tickets/${ticket.id}`}>View</Link></td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}

export default EmployeeDashboardPage
