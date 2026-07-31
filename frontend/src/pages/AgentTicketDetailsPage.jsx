import { ArrowLeft, FileText } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import {
  PendingApprovalBadge,
  TicketPriorityBadge,
  TicketStatusBadge,
} from '../components/tickets/TicketBadges.jsx'
import TicketComments from '../components/tickets/TicketComments.jsx'
import {
  addAgentTicketComment,
  closeAgentTicket,
  downloadAgentTicketAttachment,
  getAgentTicketDetails,
  requestAgentTicketAssignment,
  resolveAgentTicket,
  updateAgentTicketStatus,
} from '../services/agentTicketService.js'
import { formatLocalDate } from '../utils/dateTime.js'
import { formatTicketReference } from '../utils/ticketReference.js'

function AgentTicketDetailsPage() {
  const { id } = useParams()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState(null)
  const [reload, setReload] = useState(0)
  const [dialog, setDialog] = useState(null)
  const [resolutionSummary, setResolutionSummary] = useState('')
  const [closingNote, setClosingNote] = useState('')
  const [comment, setComment] = useState('')
  const [visibility, setVisibility] = useState('Public')
  const [saving, setSaving] = useState('')
  const [toast, setToast] = useState(null)
  const dismissToast = useCallback(() => setToast(null), [])

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

  useEffect(() => {
    if (!dialog) return undefined
    function dismissOnEscape(event) {
      if (event.key === 'Escape' && !saving) setDialog(null)
    }
    document.addEventListener('keydown', dismissOnEscape)
    return () => document.removeEventListener('keydown', dismissOnEscape)
  }, [dialog, saving])

  function notify(type, title, message) {
    setToast({ id: Date.now(), type, title, message })
  }

  async function startProgress() {
    const target = ticket.allowedStatusTransitions.find(
      (status) => status.statusName === 'In Progress',
    )
    if (!target || saving) return
    try {
      setSaving('progress')
      const updated = await updateAgentTicketStatus(ticket.ticketReferenceNumber, {
        statusId: target.statusId,
      })
      setTicket(updated)
      notify('success', 'Work Started', `${ticket.ticketReferenceNumber} is now in progress.`)
    } catch (requestError) {
      notify('error', 'Unable to Update Ticket', requestError.message)
    } finally {
      setSaving('')
    }
  }

  async function requestAssignment() {
    if (saving) return
    try {
      setSaving('request')
      await requestAgentTicketAssignment(ticket.ticketReferenceNumber)
      setTicket((current) => ({
        ...current,
        canRequestAssignment: false,
        assignmentRequestStatus: 'Pending',
      }))
      notify('success', 'Assignment Requested', 'A Manager will review your request.')
    } catch (requestError) {
      notify('error', 'Request Failed', requestError.message)
    } finally {
      setSaving('')
    }
  }

  async function resolveTicket() {
    if (resolutionSummary.trim().length < 10 || saving) return
    try {
      setSaving('resolve')
      const updated = await resolveAgentTicket(ticket.ticketReferenceNumber, {
        resolutionSummary: resolutionSummary.trim(),
      })
      setTicket(updated)
      setDialog(null)
      setResolutionSummary('')
      notify('success', 'Ticket Resolved', `${ticket.ticketReferenceNumber} has been marked as resolved.`)
    } catch (requestError) {
      notify('error', 'Unable to Resolve Ticket', requestError.message)
    } finally {
      setSaving('')
    }
  }

  async function closeTicket() {
    if (saving) return
    try {
      setSaving('close')
      const updated = await closeAgentTicket(ticket.ticketReferenceNumber, {
        closingNote: closingNote.trim() || null,
      })
      setTicket(updated)
      setDialog(null)
      setClosingNote('')
      notify('success', 'Ticket Closed', `${ticket.ticketReferenceNumber} has been closed successfully.`)
    } catch (requestError) {
      notify('error', 'Unable to Close Ticket', requestError.message)
    } finally {
      setSaving('')
    }
  }

  async function addMessage(event) {
    event.preventDefault()
    if (!comment.trim() || saving) return
    try {
      setSaving('comment')
      const created = await addAgentTicketComment(ticket.ticketReferenceNumber, {
        message: comment.trim(),
        visibility,
      })
      setTicket((current) => ({
        ...current,
        comments: [...current.comments, created],
      }))
      setComment('')
      notify('success', 'Comment Added', `Your ${visibility} comment was added.`)
    } catch (requestError) {
      notify('error', 'Unable to Save Update', requestError.message)
    } finally {
      setSaving('')
    }
  }

  async function downloadAttachment(file) {
    try {
      const blob = await downloadAgentTicketAttachment(
        ticket.ticketReferenceNumber,
        file.id,
      )
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

  if (error?.status === 404) {
    return <EmptyState title="Ticket not found" message="This assigned ticket is not available." action={<Link className="button button--secondary" to="/agent/tickets">Back to Assigned Tickets</Link>} />
  }
  if (error) {
    return <ErrorState message={error.message} onRetry={() => setReload((value) => value + 1)} />
  }
  if (!ticket) return <LoadingState message="Loading ticket details…" />

  const canStart = ticket.statusName === 'Assigned' &&
    ticket.allowedStatusTransitions.some((status) => status.statusName === 'In Progress')

  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <Link className="back-link back-link--top" to="/agent/tickets"><ArrowLeft size={18} aria-hidden="true" />Back to Assigned Tickets</Link>
      <section className="page-heading page-heading--action">
        <div><span className="eyebrow">{formatTicketReference(ticket)}</span><h2>{ticket.title}</h2><p>Created {formatLocalDate(ticket.createdDate)}</p></div>
        <div className="heading-actions">
          {canStart && <button className="button button--primary" type="button" onClick={startProgress} disabled={Boolean(saving)}>{saving === 'progress' ? 'Starting…' : 'Start Progress'}</button>}
          {ticket.canResolve && <button className="button button--primary" type="button" onClick={() => setDialog('resolve')} disabled={Boolean(saving)}>Mark as Resolved</button>}
          {ticket.canClose && <button className="button button--primary" type="button" onClick={() => setDialog('close')} disabled={Boolean(saving)}>Close Ticket</button>}
          {ticket.canRequestAssignment && <button className="button button--primary" type="button" onClick={requestAssignment} disabled={Boolean(saving)}>{saving === 'request' ? 'Requesting…' : 'Request Assignment'}</button>}
          {ticket.assignmentRequestStatus === 'Pending' && <PendingApprovalBadge />}
        </div>
      </section>
      <div className="details-grid">
        <section className="panel">
          <h2>Original Ticket Information</h2>
          <p className="ticket-description">{ticket.description}</p>
          {ticket.resolutionSummary && <div className="resolution-summary"><strong>Resolution summary</strong><p>{ticket.resolutionSummary}</p></div>}
          {ticket.attachments.length > 0 && <div className="agent-detail-list"><h3>Attachments</h3>{ticket.attachments.map((file) => <div key={file.id}><FileText size={17} aria-hidden="true" /><span>{file.fileName}</span><button className="table-action" type="button" onClick={() => downloadAttachment(file)}>Download</button></div>)}</div>}
        </section>
        <aside className="panel details-side">
          <h2>Ticket Information</h2>
          <dl>
            <div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div>
            <div><dt>Category</dt><dd>{ticket.categoryName}</dd></div>
            <div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div>
            <div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div>
            <div><dt>Created</dt><dd>{formatLocalDate(ticket.createdDate)}</dd></div>
            <div><dt>Resolved</dt><dd>{ticket.resolvedDate ? formatLocalDate(ticket.resolvedDate) : '—'}</dd></div>
            {ticket.closedDate && <div><dt>Closed</dt><dd>{formatLocalDate(ticket.closedDate)}</dd></div>}
            <div><dt>Assigned agent</dt><dd>{ticket.assignedAgentName ?? 'Unassigned'}</dd></div>
          </dl>
        </aside>
      </div>
      <TicketComments
        comments={ticket.comments}
        helperText={ticket.assignedAgentName
          ? 'Public comments are visible to everyone with access to this ticket. Private comments are visible only to the requester and assigned IT Support Agent.'
          : 'Public comments for this ticket are shown below.'}
        message={comment}
        onMessageChange={setComment}
        visibility={visibility}
        onVisibilityChange={setVisibility}
        onSubmit={addMessage}
        isSubmitting={saving === 'comment'}
        canComment={ticket.canComment}
        readOnlyMessage={ticket.assignedAgentName
          ? 'Comments are read-only because this ticket is completed.'
          : null}
        formatTimestamp={formatLocalDate}
      />
      <section className="panel dashboard-section">
        <div className="panel__heading"><div><h2>Ticket History</h2><p>Read-only lifecycle and activity record.</p></div></div>
        <div className="table-scroll"><table className="ticket-table"><thead><tr><th>Action</th><th>Performed By</th><th>Description</th><th>Date</th></tr></thead><tbody>{ticket.history.map((item) => <tr key={item.id}><td><strong>{item.actionType}</strong></td><td>{item.performedByName}</td><td>{item.description ?? '—'}</td><td>{formatLocalDate(item.createdDate)}</td></tr>)}</tbody></table></div>
      </section>
      {dialog && <>
        <button className="dialog-backdrop" type="button" aria-label="Close dialog" onClick={() => { if (!saving) setDialog(null) }} />
        <section className="dialog" role="dialog" aria-modal="true" aria-labelledby="agent-action-title" aria-describedby="agent-action-description">
          <h2 id="agent-action-title">{dialog === 'close' ? 'Close Ticket' : 'Mark Ticket as Resolved'}</h2>
          <p id="agent-action-description">{dialog === 'close' ? `Are you sure you want to close ${ticket.ticketReferenceNumber}? This confirms that the work has been completed.` : 'Describe the technical resolution before marking this ticket as resolved.'}</p>
          {dialog === 'resolve'
            ? <label><span>Resolution summary</span><textarea autoFocus value={resolutionSummary} onChange={(event) => setResolutionSummary(event.target.value)} minLength="10" maxLength="5000" disabled={Boolean(saving)} /></label>
            : <label><span>Closing note (optional)</span><textarea autoFocus value={closingNote} onChange={(event) => setClosingNote(event.target.value)} maxLength="500" disabled={Boolean(saving)} /></label>}
          <div className="dialog__actions"><button className="button button--secondary" type="button" onClick={() => setDialog(null)} disabled={Boolean(saving)}>Cancel</button><button className="button button--primary" type="button" onClick={dialog === 'close' ? closeTicket : resolveTicket} disabled={Boolean(saving) || (dialog === 'resolve' && resolutionSummary.trim().length < 10)}>{saving ? 'Saving…' : dialog === 'close' ? 'Close Ticket' : 'Mark as Resolved'}</button></div>
        </section>
      </>}
    </>
  )
}

export default AgentTicketDetailsPage
