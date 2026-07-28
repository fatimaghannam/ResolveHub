import { ArrowLeft } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { getAdminTicket } from '../services/adminService.js'
import { formatLocalDate } from '../utils/dateTime.js'

function AdminTicketDetailsPage() {
  const { ticketReference } = useParams()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  useEffect(() => {
    const controller = new AbortController()
    getAdminTicket(ticketReference, controller.signal)
      .then(setTicket)
      .catch((requestError) => setError(requestError.message))
      .finally(() => setLoading(false))
    return () => controller.abort()
  }, [ticketReference])
  if (loading) return <LoadingState message="Loading ticket details…" />
  if (error) return <ErrorState message={error} />
  if (!ticket) return <EmptyState title="Ticket not found" message="This temporary ticket is not available." action={<Link className="button button--secondary" to="/admin/tickets">Back to All Tickets</Link>} />
  return (
    <>
      <Link className="back-link back-link--top" to="/admin/tickets"><ArrowLeft size={18} />Back to All Tickets</Link>
      <section className="page-heading"><span className="eyebrow">{ticket.ticketReferenceNumber}</span><h2>{ticket.title}</h2><p>Created {formatLocalDate(ticket.createdDate)}</p></section>
      <div className="details-grid"><section className="panel"><h2>Ticket Summary</h2><p className="ticket-description">{ticket.description}</p></section><aside className="panel details-side"><h2>Ticket Information</h2><dl><div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div><div><dt>Category</dt><dd>{ticket.categoryName}</dd></div><div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div><div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div><div><dt>Assigned agent</dt><dd>{ticket.assignedAgentName ?? 'Unassigned'}</dd></div></dl></aside></div>
    </>
  )
}

export default AdminTicketDetailsPage
