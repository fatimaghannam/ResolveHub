import { ArrowLeft, MessageSquareText, NotebookPen, RefreshCw } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { EmptyState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { agentTickets, mockAssignedAgent } from '../data/agentDashboardMockData.js'
import { formatLocalDate } from '../utils/dateTime.js'
import { formatTicketReference } from '../utils/ticketReference.js'

function AgentTicketDetailsPage() {
  const { id } = useParams()
  const ticket = agentTickets.find((item) => formatTicketReference(item) === id)

  if (!ticket) {
    return <EmptyState title="Ticket not found" message="This temporary ticket is not available." action={<Link className="button button--secondary" to="/agent/tickets">Back to Assigned Tickets</Link>} />
  }

  return (
    <>
      <Link className="back-link back-link--top" to="/agent/tickets"><ArrowLeft size={18} aria-hidden="true" />Back to Assigned Tickets</Link>
      <section className="page-heading">
        <span className="eyebrow">{formatTicketReference(ticket)}</span>
        <h2>{ticket.title}</h2>
        <p>Created {formatLocalDate(ticket.createdDate)}</p>
      </section>
      <div className="details-grid">
        <section className="panel">
          <h2>Original Ticket Information</h2>
          <p className="ticket-description">{ticket.description}</p>
          <div className="coming-soon-grid">
            <article><RefreshCw size={20} aria-hidden="true" /><strong>Status updates</strong><span>Coming soon</span></article>
            <article><MessageSquareText size={20} aria-hidden="true" /><strong>Employee comments</strong><span>Coming soon</span></article>
            <article><NotebookPen size={20} aria-hidden="true" /><strong>Internal notes</strong><span>Coming soon</span></article>
          </div>
        </section>
        <aside className="panel details-side">
          <h2>Ticket Information</h2>
          <dl>
            <div><dt>Requester</dt><dd>{ticket.requester}</dd></div>
            <div><dt>Category</dt><dd>{ticket.category}</dd></div>
            <div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priority} /></dd></div>
            <div><dt>Status</dt><dd><TicketStatusBadge value={ticket.status} /></dd></div>
            <div><dt>Created</dt><dd>{formatLocalDate(ticket.createdDate)}</dd></div>
            <div><dt>Assigned agent</dt><dd>{mockAssignedAgent}</dd></div>
          </dl>
        </aside>
      </div>
    </>
  )
}

export default AgentTicketDetailsPage
