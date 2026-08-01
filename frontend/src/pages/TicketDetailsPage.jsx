import { useCallback, useEffect, useState } from 'react'
import { ArrowLeft } from 'lucide-react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import TicketComments from '../components/tickets/TicketComments.jsx'
import {
  cancelTicket,
  downloadAttachment,
  getTicket,
} from '../services/ticketService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'
import { formatTicketReference } from '../utils/ticketReference.js'

function TicketDetailsPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)
  const [toast, setToast] = useState(() => {
    const notification = location.state?.toast
    return notification ? { id: Date.now(), ...notification } : null
  })
  const dismissToast = useCallback(() => setToast(null), [])
  const notify = useCallback((type, title, message) => {
    setToast({ id: Date.now(), type, title, message })
  }, [])

  useEffect(() => {
    if (!location.state?.toast) return
    const nextState = { ...location.state }
    delete nextState.toast
    navigate(location.pathname, {
      replace: true,
      state: Object.keys(nextState).length ? nextState : null,
    })
  }, [location.pathname, location.state, navigate])

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    setTicket(null)
    getTicket(id, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setTicket(result)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) {
          setError(requestError.status === 404 ? 'This ticket is unavailable.' : requestError.message)
        }
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
      navigate('/employee/tickets', {
        replace: true,
        state: {
          toast: {
            type: 'success',
            title: 'Ticket Cancelled',
            message: `${formatTicketReference(ticket)} was cancelled.`,
          },
        },
      })
    } catch (requestError) {
      notify(
        'error',
        'Unable to Cancel Ticket',
        requestError.status === 409
          ? 'This ticket can no longer be cancelled because it has already been assigned or work has started.'
          : requestError.message,
      )
      setDialogOpen(false)
    } finally { setSaving(false) }
  }

  async function download(file) {
    try {
      const blob = await downloadAttachment(id, file.id)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = file.fileName
      anchor.click()
      URL.revokeObjectURL(url)
    } catch (requestError) {
      notify('error', 'Unable to Download Attachment', requestError.message)
    }
  }

  function goBackToTickets() {
    navigate('/employee/tickets')
  }

  const toastRegion = toast && (
    <div className="app-toast-region">
      <Toast
        key={toast.id}
        type={toast.type}
        title={toast.title}
        message={toast.message}
        onDismiss={dismissToast}
      />
    </div>
  )

  if (error) return <>{toastRegion}<ErrorState message={error} /></>
  if (!ticket) return <>{toastRegion}<LoadingState message="Loading ticket details…" /></>

  return (
    <>
      {toastRegion}
      <button type="button" className="back-link back-link--top" onClick={goBackToTickets} aria-label="Back to My Tickets">
        <ArrowLeft size={17} aria-hidden="true" />
        <span>Back to My Tickets</span>
      </button>
      <section className="page-heading page-heading--action"><div><span className="eyebrow">{formatTicketReference(ticket)}</span><h2>{ticket.title}</h2><p>Created <time dateTime={ticket.createdDate} title="Displayed in your local time">{formatLocalDateTime(ticket.createdDate)}</time></p></div><div className="heading-actions">{ticket.canEdit && <Link className="button button--secondary" to={`/employee/tickets/${id}/edit`}>Edit</Link>}{ticket.canDelete && <button className="button button--danger-outline" onClick={() => setDialogOpen(true)}>Cancel Ticket</button>}</div></section>
      {!ticket.canEdit && <div className="inline-alert">This ticket can no longer be edited because work has already started.</div>}
      {ticket.statusName === 'Duplicate' && ticket.originalTicketReference && <section className="duplicate-info-panel" aria-labelledby="duplicate-info-title"><h2 id="duplicate-info-title">Duplicate Ticket</h2><p>This ticket was marked as a duplicate of:</p><Link className="duplicate-info-panel__link" to={`/employee/tickets/${ticket.originalTicketId}`}><strong>{ticket.originalTicketReference}</strong><span>{ticket.originalTicketTitle || 'View original ticket'}</span></Link></section>}
      <div className="details-grid">
        <section className="panel details-main"><h2>Issue Description</h2><p className="ticket-description">{ticket.description}</p>
          {ticket.resolutionSummary && <div className="resolution-summary"><strong>Resolution summary</strong><p>{ticket.resolutionSummary}</p></div>}
          <h2>Attachments</h2>
          {ticket.attachments.length === 0 ? <p>No attachments.</p> : ticket.attachments.map((file) => (
            <div className="attachment-row" key={file.id}><span>{file.fileName}</span><small>{Math.ceil(file.fileSizeBytes / 1024)} KB · <time dateTime={file.uploadedDate}>{formatLocalDateTime(file.uploadedDate)}</time></small><button type="button" onClick={() => download(file)}>Download</button></div>
          ))}
        </section>
        <aside className="panel details-side"><h2>Ticket Information</h2>
          <dl><div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div><div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div><div><dt>Category</dt><dd>{ticket.categoryName}</dd></div><div><dt>Created by</dt><dd>{ticket.createdByName}</dd></div><div><dt>Assigned to</dt><dd>{ticket.assignedToName ?? 'Unassigned'}</dd></div>{ticket.resolvedDate && <div><dt>Resolved</dt><dd>{formatLocalDateTime(ticket.resolvedDate)}</dd></div>}{ticket.closedDate && <div><dt>Closed</dt><dd>{formatLocalDateTime(ticket.closedDate)}</dd></div>}<div><dt>Last updated</dt><dd><time dateTime={ticket.updatedDate} title="Displayed in your local time">{formatLocalDateTime(ticket.updatedDate)}</time></dd></div></dl>
        </aside>
      </div>
      <TicketComments
        comments={ticket.comments}
        endpoint={`/api/tickets/${id}/comments`}
        canViewPrivate
        canComment={!ticket.closedDate && !ticket.cancelledDate && ticket.statusName !== 'Duplicate'}
        readOnlyMessage="Comments are read-only because this ticket is completed."
        formatTimestamp={formatLocalDateTime}
        onNotify={notify}
      />
      {ticket.history.length > 0 && <section className="panel dashboard-section"><div className="panel__heading"><div><h2>Ticket History</h2><p>Updates recorded for this support request.</p></div></div><div className="table-scroll"><table className="ticket-table"><thead><tr><th>Action</th><th>Performed By</th><th>Description</th><th>Date</th></tr></thead><tbody>{ticket.history.map((item) => <tr key={item.id}><td><strong>{item.actionType}</strong></td><td>{item.performedByName}</td><td>{item.description ?? '—'}</td><td>{formatLocalDateTime(item.createdDate)}</td></tr>)}</tbody></table></div></section>}
      {dialogOpen && <div className="dialog-backdrop"><div className="dialog" role="dialog" aria-modal="true" aria-labelledby="details-cancel-title" aria-describedby="details-cancel-description"><h2 id="details-cancel-title">Cancel {formatTicketReference(ticket)}?</h2><p id="details-cancel-description">The ticket will be removed from your active list.</p><label><span>Reason (optional)</span><textarea maxLength="500" value={reason} onChange={(e) => setReason(e.target.value)} /></label><div className="dialog__actions"><button autoFocus type="button" className="button button--secondary" onClick={() => setDialogOpen(false)} disabled={saving}>Keep Ticket</button><button type="button" className="button button--danger" onClick={confirmCancel} disabled={saving}>{saving ? 'Cancelling…' : 'Confirm Cancellation'}</button></div></div></div>}
    </>
  )
}

export default TicketDetailsPage
