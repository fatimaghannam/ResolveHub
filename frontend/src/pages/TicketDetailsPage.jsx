import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { cancelTicket, getTicket } from '../services/ticketService.js'

function formatDate(value) {
  return value ? new Date(value).toLocaleString() : '—'
}

function TicketDetailsPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    getTicket(id, controller.signal).then(setTicket).catch((requestError) => {
      if (requestError.name !== 'AbortError') setError(requestError.status === 404 ? 'This ticket is unavailable.' : requestError.message)
    })
    return () => controller.abort()
  }, [id])

  useEffect(() => {
    if (!dialogOpen) return undefined
    const closeOnEscape = (event) => {
      if (event.key === 'Escape' && !saving) setDialogOpen(false)
    }
    document.addEventListener('keydown', closeOnEscape)
    return () => document.removeEventListener('keydown', closeOnEscape)
  }, [dialogOpen, saving])

  async function confirmCancel() {
    try {
      setSaving(true)
      await cancelTicket(id, reason)
      navigate('/employee/tickets', { replace: true })
    } catch (requestError) {
      setError(requestError.status === 409 ? 'This ticket can no longer be deleted because it has already been assigned or work has started.' : requestError.message)
      setDialogOpen(false)
    } finally { setSaving(false) }
  }

  if (error) return <ErrorState message={error} />
  if (!ticket) return <LoadingState message="Loading ticket details…" />

  return (
    <>
      {location.state?.notice && <div className="inline-alert inline-alert--success" role="status">{location.state.notice}</div>}
      <section className="page-heading page-heading--action"><div><span className="eyebrow">{ticket.ticketReferenceNumber}</span><h2>{ticket.title}</h2><p>Created {formatDate(ticket.createdDate)}</p></div><div className="heading-actions">{ticket.canEdit && <Link className="button button--secondary" to={`/employee/tickets/${id}/edit`}>Edit</Link>}{ticket.canDelete && <button className="button button--danger-outline" onClick={() => setDialogOpen(true)}>Cancel Ticket</button>}</div></section>
      {!ticket.canEdit && <div className="inline-alert">This ticket can no longer be edited because work has already started.</div>}
      <div className="details-grid">
        <section className="panel details-main"><h2>Issue Description</h2><p className="ticket-description">{ticket.description}</p></section>
        <aside className="panel details-side"><h2>Ticket Information</h2>
          <dl><div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div><div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div><div><dt>Category</dt><dd>{ticket.categoryName}</dd></div><div><dt>Created by</dt><dd>{ticket.createdByName}</dd></div><div><dt>Assigned to</dt><dd>{ticket.assignedToName ?? 'Unassigned'}</dd></div><div><dt>Last updated</dt><dd>{formatDate(ticket.updatedDate)}</dd></div></dl>
        </aside>
      </div>
      <Link className="back-link" to="/employee/tickets">← Back to My Tickets</Link>
      {dialogOpen && <div className="dialog-backdrop"><div className="dialog" role="dialog" aria-modal="true" aria-labelledby="details-cancel-title" aria-describedby="details-cancel-description"><h2 id="details-cancel-title">Cancel {ticket.ticketReferenceNumber}?</h2><p id="details-cancel-description">The ticket will be removed from your active list.</p><label><span>Reason (optional)</span><textarea maxLength="500" value={reason} onChange={(e) => setReason(e.target.value)} /></label><div className="dialog__actions"><button autoFocus type="button" className="button button--secondary" onClick={() => setDialogOpen(false)} disabled={saving}>Keep Ticket</button><button type="button" className="button button--danger" onClick={confirmCancel} disabled={saving}>{saving ? 'Cancelling…' : 'Confirm Cancellation'}</button></div></div></div>}
    </>
  )
}

export default TicketDetailsPage
