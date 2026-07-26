import {
  AlertTriangle,
  CheckCircle2,
  CircleAlert,
  Clock3,
  ListChecks,
  PauseCircle,
} from 'lucide-react'
import { Link, useOutletContext } from 'react-router-dom'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import {
  agentTicketStats,
  priorityAttentionTickets,
  recentAssignedTickets,
} from '../data/agentDashboardMockData.js'
import { formatLocalDate } from '../utils/dateTime.js'
import { formatTicketReference } from '../utils/ticketReference.js'

const statIcons = [ListChecks, Clock3, PauseCircle, AlertTriangle, CircleAlert, CheckCircle2]

function AgentDashboardPage() {
  const { user } = useOutletContext()

  return (
    <>
      <section className="page-heading page-heading--compact">
        <h2>Welcome back, {user?.firstName ?? 'Agent'}</h2>
      </section>

      <section className="stat-grid stat-grid--six" aria-label="Assigned ticket statistics">
        {agentTicketStats.map((stat, index) => {
          const Icon = statIcons[index]
          return (
            <article className="stat-card" key={stat.label}>
              <span className={`stat-card__icon tone-${stat.tone}`}><Icon size={22} aria-hidden="true" /></span>
              <span>
                <small>{stat.label}</small>
                <strong>{stat.value}</strong>
              </span>
            </article>
          )
        })}
      </section>

      <section className="panel dashboard-section">
        <div className="panel__heading">
          <div><h2>Priority Attention</h2><p>Critical and high-priority tickets that require immediate review.</p></div>
        </div>
        <div className="priority-list">
          {priorityAttentionTickets.map((ticket) => (
            <article className="priority-item" key={ticket.id}>
              <div className="priority-item__summary">
                <strong>{formatTicketReference(ticket)}</strong>
                <span>{ticket.title}</span>
                <small>Requested by {ticket.requester} · {formatLocalDate(ticket.createdDate)}</small>
              </div>
              <div className="priority-item__badges">
                <TicketPriorityBadge value={ticket.priority} />
                <TicketStatusBadge value={ticket.status} />
              </div>
              <Link className="table-action" to={`/agent/tickets/${formatTicketReference(ticket)}`}>View</Link>
            </article>
          ))}
        </div>
      </section>

      <section className="panel">
        <div className="panel__heading">
          <div><h2>My Assigned Tickets</h2><p>Your most recently assigned support requests.</p></div>
          <Link to="/agent/tickets">View all assigned tickets</Link>
        </div>
        <div className="table-scroll">
          <table className="ticket-table agent-dashboard-ticket-table">
            <thead><tr><th>Ticket</th><th>Requester</th><th>Priority</th><th>Status</th><th>Created</th><th>Action</th></tr></thead>
            <tbody>
              {recentAssignedTickets.map((ticket) => (
                <tr key={ticket.id}>
                  <td className="ticket-summary-cell">
                    <strong>{formatTicketReference(ticket)}</strong>
                    <span>{ticket.title}</span>
                    <small>{ticket.category}</small>
                  </td>
                  <td>{ticket.requester}</td>
                  <td><TicketPriorityBadge value={ticket.priority} /></td>
                  <td><TicketStatusBadge value={ticket.status} /></td>
                  <td>{formatLocalDate(ticket.createdDate)}</td>
                  <td><Link className="table-action" to={`/agent/tickets/${formatTicketReference(ticket)}`}>View</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </>
  )
}

export default AgentDashboardPage
