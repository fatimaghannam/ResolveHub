import { ArrowLeft, MessageSquareText, NotebookPen, RefreshCw } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { getAgentTicketDetails } from '../services/agentTicketService.js'
import { formatLocalDate } from '../utils/dateTime.js'
import { formatTicketReference } from '../utils/ticketReference.js'

function AgentTicketDetailsPage() {
  const { id } = useParams()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState(null)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setTicket(null)
    setError(null)
    getAgentTicketDetails(id, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setTicket(result)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) {
          setError(requestError)
        }
      })
    return () => controller.abort()
  }, [id, reload])

  if (error?.status === 404) {
    return <EmptyState title="Ticket not found" message="This assigned ticket is not available." action={<Link className="button button--secondary" to="/agent/tickets">Back to Assigned Tickets</Link>} />
  }

  if (error) {
    return <ErrorState message={error.message} onRetry={() => setReload((value) => value + 1)} />
  }

  if (!ticket) {
    return <LoadingState message="Loading ticket details…" />
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
            <div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div>
            <div><dt>Category</dt><dd>{ticket.categoryName}</dd></div>
            <div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div>
            <div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div>
            <div><dt>Created</dt><dd>{formatLocalDate(ticket.createdDate)}</dd></div>
            <div><dt>Assigned agent</dt><dd>{ticket.assignedAgentName}</dd></div>
          </dl>
        </aside>
      </div>
    </>
  )
}

export default AgentTicketDetailsPage
