import { ArrowLeft } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { getAdminTicket } from '../services/adminService.js'
import { getManagerTicket } from '../services/managerService.js'
import { formatLocalDate } from '../utils/dateTime.js'

function AdminTicketDetailsPage({ roleArea = 'admin' }) {
  const { ticketReference } = useParams()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    setTicket(null)
    setError('')
    setLoading(true)

    const loadTicket = roleArea === 'manager' ? getManagerTicket : getAdminTicket
    loadTicket(ticketReference, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) {
          setTicket(result)
        }
      })
      .catch((requestError) => {
        if (
          requestError.name !== 'AbortError' &&
          !controller.signal.aborted
        ) {
          setError(requestError.message)
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      })

    return () => controller.abort()
  }, [ticketReference, roleArea])

  if (loading) return <LoadingState message="Loading ticket details…" />
  if (error) return <ErrorState message={error} />
  if (!ticket) return <EmptyState title="Ticket not found" message="This ticket is not available." action={<Link className="button button--secondary" to={`/${roleArea}/tickets`}>Back to All Tickets</Link>} />
  return (
    <>
      <Link className="back-link back-link--top" to={`/${roleArea}/tickets`}><ArrowLeft size={18} />Back to All Tickets</Link>
      <section className="page-heading"><span className="eyebrow">{ticket.ticketReferenceNumber}</span><h2>{ticket.title}</h2><p>Created {formatLocalDate(ticket.createdDate)}</p></section>
      <div className="details-grid"><section className="panel"><h2>Ticket Summary</h2><p className="ticket-description">{ticket.description}</p></section><aside className="panel details-side"><h2>Ticket Information</h2><dl><div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div><div><dt>Category</dt><dd>{ticket.categoryName}</dd></div><div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div><div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div><div><dt>Assigned agent</dt><dd>{ticket.assignedAgentName ?? 'Unassigned'}</dd></div></dl></aside></div>
    </>
  )
}

export default AdminTicketDetailsPage
