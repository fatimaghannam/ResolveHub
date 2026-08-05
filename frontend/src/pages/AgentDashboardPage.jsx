import { useEffect, useState } from 'react'
import {
  AlertTriangle,
  CheckCircle2,
  CircleAlert,
  Clock3,
  ListChecks,
  PauseCircle,
} from 'lucide-react'
import { Link, useOutletContext } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { getAgentDashboard } from '../services/agentTicketService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'
import { formatTicketReference } from '../utils/ticketReference.js'

const statIcons = [ListChecks, Clock3, PauseCircle, AlertTriangle, CircleAlert, CheckCircle2]
const statDefinitions = [
  ['Active Assigned Tickets', 'activeAssignedTickets', 'blue'],
  ['In Progress', 'inProgress', 'cyan'],
  ['Pending', 'pending', 'amber'],
  ['High Priority Open', 'highPriorityOpen', 'amber'],
  ['Critical Open', 'criticalOpen', 'red'],
  ['Resolved This Month', 'resolvedThisMonth', 'green'],
]

function AgentDashboardPage() {
  const { user } = useOutletContext()
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setData(null)
    setError('')
    getAgentDashboard(controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setData(result)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) {
          setError(requestError.message)
        }
      })
    return () => controller.abort()
  }, [reload])

  return (
    <>
      <section className="page-heading page-heading--compact">
        <h2>Welcome back, {user?.firstName ?? 'Agent'}</h2>
      </section>

      {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
      {!error && !data && <LoadingState message="Loading your assigned workload…" />}
      {data && (
        <>
          <section className="stat-grid stat-grid--six" aria-label="Assigned ticket statistics">
            {statDefinitions.map(([label, field, tone], index) => {
              const Icon = statIcons[index]
              return (
                <article className="stat-card" key={field}>
                  <span className={`stat-card__icon tone-${tone}`}><Icon size={22} aria-hidden="true" /></span>
                  <span><small>{label}</small><strong>{data[field]}</strong></span>
                </article>
              )
            })}
          </section>

          <section className="panel dashboard-section">
            <div className="panel__heading">
              <div><h2>Priority Attention</h2><p>Critical and high-priority tickets that require immediate review.</p></div>
            </div>
            {data.priorityAttentionTickets.length === 0 ? (
              <EmptyState title="No priority tickets" message="No critical or high-priority assigned tickets currently need attention." />
            ) : (
              <div className="priority-list">
                {data.priorityAttentionTickets.map((ticket) => (
                  <article className="priority-item" key={ticket.id}>
                    <div className="priority-item__summary">
                      <strong>{formatTicketReference(ticket)}</strong>
                      <span>{ticket.title}</span>
                      <small>Requested by {ticket.requesterName} · {formatLocalDateTime(ticket.createdDate)}</small>
                    </div>
                    <div className="priority-item__badges">
                      <TicketPriorityBadge value={ticket.priorityName} />
                      <TicketStatusBadge value={ticket.statusName} />
                    </div>
                    <Link className="table-action" to={`/agent/tickets/${formatTicketReference(ticket)}`}>View</Link>
                  </article>
                ))}
              </div>
            )}
          </section>

          <section className="panel">
            <div className="panel__heading">
              <div><h2>My Assigned Tickets</h2><p>Your most recently assigned support requests.</p></div>
              <Link to="/agent/tickets">View all assigned tickets</Link>
            </div>
            {data.recentAssignedTickets.length === 0 ? (
              <EmptyState title="No assigned tickets" message="Tickets assigned to you will appear here." />
            ) : (
              <div className="table-scroll">
                <table className="ticket-table agent-dashboard-ticket-table">
                  <thead><tr><th>Ticket</th><th>Requester</th><th>Priority</th><th>Status</th><th>Created</th><th>Action</th></tr></thead>
                  <tbody>
                    {data.recentAssignedTickets.map((ticket) => (
                      <tr key={ticket.id}>
                        <td className="ticket-summary-cell">
                          <strong>{formatTicketReference(ticket)}</strong>
                          <span>{ticket.title}</span>
                          <small>{ticket.categoryName}</small>
                        </td>
                        <td>{ticket.requesterName}</td>
                        <td><TicketPriorityBadge value={ticket.priorityName} /></td>
                        <td><TicketStatusBadge value={ticket.statusName} /></td>
                        <td>{formatLocalDateTime(ticket.createdDate)}</td>
                        <td><Link className="table-action" to={`/agent/tickets/${formatTicketReference(ticket)}`}>View</Link></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
    </>
  )
}

export default AgentDashboardPage
