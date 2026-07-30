import { ArrowLeft } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import {
  getAdminTicket,
  removeAdminDuplicateTicket,
} from '../services/adminService.js'
import { getManagerTicket } from '../services/managerService.js'
import { formatLocalDate } from '../utils/dateTime.js'

function AdminTicketDetailsPage({ roleArea = 'admin' }) {
  const { ticketReference } = useParams()
  const navigate = useNavigate()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [duplicateDialogOpen, setDuplicateDialogOpen] = useState(false)
  const [originalReference, setOriginalReference] = useState('')
  const [removingDuplicate, setRemovingDuplicate] = useState(false)
  const [duplicateError, setDuplicateError] = useState('')

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

  async function removeDuplicate() {
    if (!originalReference.trim() || removingDuplicate) return
    try {
      setRemovingDuplicate(true)
      setDuplicateError('')
      await removeAdminDuplicateTicket(
        ticket.ticketReferenceNumber,
        originalReference.trim(),
      )
      navigate('/admin/tickets', {
        replace: true,
        state: {
          notice: `${ticket.ticketReferenceNumber} was removed as a duplicate of ${originalReference.trim()}.`,
        },
      })
    } catch (requestError) {
      setDuplicateError(requestError.message)
    } finally {
      setRemovingDuplicate(false)
    }
  }

  return (
    <>
      <Link className="back-link back-link--top" to={`/${roleArea}/tickets`}><ArrowLeft size={18} />Back to All Tickets</Link>
      <section className="page-heading page-heading--action">
        <div><span className="eyebrow">{ticket.ticketReferenceNumber}</span><h2>{ticket.title}</h2><p>Created {formatLocalDate(ticket.createdDate)}</p></div>
        {roleArea === 'admin' && <button className="button button--danger-outline" type="button" onClick={() => { setDuplicateError(''); setDuplicateDialogOpen(true) }}>Remove Duplicate</button>}
      </section>
      <div className="details-grid"><section className="panel"><h2>Ticket Summary</h2><p className="ticket-description">{ticket.description}</p></section><aside className="panel details-side"><h2>Ticket Information</h2><dl><div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div><div><dt>Category</dt><dd>{ticket.categoryName}</dd></div><div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div><div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div><div><dt>Assigned agent</dt><dd>{ticket.assignedAgentName ?? 'Unassigned'}</dd></div>{ticket.resolvedDate && <div><dt>Resolved</dt><dd>{formatLocalDate(ticket.resolvedDate)}</dd></div>}{ticket.closedDate && <div><dt>Closed</dt><dd>{formatLocalDate(ticket.closedDate)}</dd></div>}</dl></aside></div>
      <section className="panel dashboard-section">
        <div className="panel__heading"><div><h2>Ticket History</h2><p>Read-only record of important ticket actions.</p></div></div>
        {ticket.history.length === 0
          ? <EmptyState title="No history yet" message="Ticket activity will appear here." />
          : <div className="table-scroll"><table className="ticket-table"><thead><tr><th>Action</th><th>Performed By</th><th>Description</th><th>Date</th></tr></thead><tbody>{ticket.history.map((item) => <tr key={item.id}><td><strong>{item.actionType}</strong></td><td>{item.performedByName}</td><td>{item.description ?? '—'}</td><td>{formatLocalDate(item.createdDate)}</td></tr>)}</tbody></table></div>}
      </section>
      {duplicateDialogOpen && <>
        <button className="dialog-backdrop" type="button" aria-label="Close duplicate-ticket confirmation" onClick={() => { if (!removingDuplicate) setDuplicateDialogOpen(false) }} />
        <section className="dialog" role="dialog" aria-modal="true" aria-labelledby="duplicate-title" aria-describedby="duplicate-description">
          <h2 id="duplicate-title">Remove duplicate ticket?</h2>
          <p id="duplicate-description">This will mark {ticket.ticketReferenceNumber} as a duplicate and preserve its history and audit evidence. Enter the original ticket reference to continue.</p>
          <label><span>Original ticket reference</span><input autoFocus value={originalReference} onChange={(event) => setOriginalReference(event.target.value)} placeholder="RH-2026-0001" disabled={removingDuplicate} /></label>
          {duplicateError && <div className="inline-alert inline-alert--error" role="alert">{duplicateError}</div>}
          <div className="dialog__actions"><button className="button button--secondary" type="button" onClick={() => setDuplicateDialogOpen(false)} disabled={removingDuplicate}>Cancel</button><button className="button button--danger" type="button" onClick={removeDuplicate} disabled={!originalReference.trim() || removingDuplicate}>{removingDuplicate ? 'Removing…' : 'Confirm Duplicate'}</button></div>
        </section>
      </>}
    </>
  )
}

export default AdminTicketDetailsPage
