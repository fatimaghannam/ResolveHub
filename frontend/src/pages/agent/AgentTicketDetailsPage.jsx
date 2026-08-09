import { ArrowLeft } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { createPortal } from 'react-dom'
import { Link, useLocation, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../../components/common/States.jsx'
import Toast from '../../components/common/Toast.jsx'
import {
  TicketPriorityBadge,
  TicketStatusBadge,
} from '../../components/tickets/TicketBadges.jsx'
import TicketComments from '../../components/tickets/TicketComments.jsx'
import TicketAttachments from '../../components/tickets/TicketAttachments.jsx'
import TicketActivityLog from '../../components/tickets/TicketActivityLog.jsx'
import TicketHistorySection from '../../components/tickets/TicketHistorySection.jsx'
import {
  closeAgentTicket,
  downloadAgentTicketAttachment,
  getAgentTicketDetails,
  markAgentTicketPending,
  resumeAgentTicketWork,
  requestAgentTicketCancellation,
  requestAgentTicketAssignment,
  resolveAgentTicket,
  updateAgentTicketStatus,
} from '../../services/agentTicketService.js'
import { formatLocalDateTime } from '../../utils/dateTime.js'
import { formatTicketReference } from '../../utils/ticketReference.js'

function AgentTicketDetailsPage() {
  const location = useLocation()
  const { id } = useParams()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState(null)
  const [reload, setReload] = useState(0)
  const [dialog, setDialog] = useState(null)
  const [resolutionSummary, setResolutionSummary] = useState('')
  const [closingNote, setClosingNote] = useState('')
  const [pendingReason, setPendingReason] = useState('')
  const [customPendingReason, setCustomPendingReason] = useState('')
  const [pendingNote, setPendingNote] = useState('')
  const [cancellationReason, setCancellationReason] = useState('')
  const [saving, setSaving] = useState('')
  const [toast, setToast] = useState(null)
  const [moreActionsOpen, setMoreActionsOpen] = useState(false)
  const [moreActionsPosition, setMoreActionsPosition] = useState(null)
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

  useEffect(() => {
    if (!moreActionsOpen) return undefined
    function closeMoreActions(event) {
      if (!event.target.closest('.agent-more-actions, .agent-more-actions__dropdown')) setMoreActionsOpen(false)
    }
    function closeMoreActionsOnEscape(event) {
      if (event.key === 'Escape') setMoreActionsOpen(false)
    }
    document.addEventListener('pointerdown', closeMoreActions)
    document.addEventListener('keydown', closeMoreActionsOnEscape)
    return () => {
      document.removeEventListener('pointerdown', closeMoreActions)
      document.removeEventListener('keydown', closeMoreActionsOnEscape)
    }
  }, [moreActionsOpen])

  function toggleMoreActions(event) {
    if (moreActionsOpen) {
      setMoreActionsOpen(false)
      return
    }
    const triggerBounds = event.currentTarget.getBoundingClientRect()
    const menuWidth = 196
    const menuHeight = 112
    const viewportMargin = 8
    const openUpward = window.innerHeight - triggerBounds.bottom < menuHeight + viewportMargin
    setMoreActionsPosition({
      left: Math.max(viewportMargin, Math.min(
        triggerBounds.right - menuWidth,
        window.innerWidth - menuWidth - viewportMargin,
      )),
      top: openUpward
        ? Math.max(viewportMargin, triggerBounds.top - menuHeight - 6)
        : triggerBounds.bottom + 6,
    })
    setMoreActionsOpen(true)
  }

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

  async function markPending() {
    if (!pendingReason || saving ||
      (pendingReason === 'other' && !customPendingReason.trim())) return
    try {
      setSaving('pending')
      const result = await markAgentTicketPending(ticket.ticketReferenceNumber, {
        reasonCode: pendingReason,
        customReason: pendingReason === 'other' ? customPendingReason.trim() : null,
        additionalNote: pendingNote.trim() || null,
      })
      setTicket(result.ticket)
      setDialog(null)
      setPendingReason('')
      setCustomPendingReason('')
      setPendingNote('')
      notify('success', 'Work Paused', `${ticket.ticketReferenceNumber} is now pending.`)
    } catch (requestError) {
      notify('error', 'Unable to Pause Work', requestError.message)
    } finally {
      setSaving('')
    }
  }

  async function resumeWork() {
    if (saving) return
    try {
      setSaving('resume')
      const result = await resumeAgentTicketWork(ticket.ticketReferenceNumber)
      setTicket(result.ticket)
      notify('success', 'Work Resumed', `A new work session has started for ${ticket.ticketReferenceNumber}.`)
    } catch (requestError) {
      notify('error', 'Unable to Resume Work', requestError.message)
    } finally {
      setSaving('')
    }
  }

  async function requestCancellation() {
    if (!cancellationReason.trim() || saving) return
    try {
      setSaving('cancellation')
      await requestAgentTicketCancellation(ticket.ticketReferenceNumber, cancellationReason.trim())
      setDialog(null)
      setCancellationReason('')
      setReload((value) => value + 1)
      notify('success', 'Cancellation Requested', 'Your cancellation request was submitted for Manager review. The ticket status has not changed.')
    } catch (requestError) {
      notify('error', 'Unable to Request Cancellation', requestError.message)
    } finally {
      setSaving('')
    }
  }

  async function requestAssignment() {
    if (saving) return
    try {
      setSaving('assignment')
      await requestAgentTicketAssignment(ticket.ticketReferenceNumber)
      setDialog(null)
      setTicket((current) => ({
        ...current,
        canRequestAssignment: false,
        assignmentRequestStatus: 'Pending',
      }))
      notify('success', 'Assignment Requested', 'Your request was submitted for Manager review. The ticket remains unassigned.')
    } catch (requestError) {
      notify('error', 'Unable to Request Assignment', requestError.message)
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
  const canMarkPending = ticket.statusName === 'In Progress'
  const canRequestCancellation = ['Assigned', 'In Progress', 'Pending'].includes(ticket.statusName) &&
    !ticket.pendingCancellationRequest
  const hasMoreActions = canMarkPending || canRequestCancellation

  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <section className="ticket-details-header-grid">
        <Link className="back-link ticket-details-header__back" to={location.state?.from === 'notifications' ? '/agent/notifications' : location.state?.from === 'open-tickets' ? '/agent/tickets/open' : '/agent/tickets'}><ArrowLeft size={18} aria-hidden="true" />{location.state?.from === 'notifications' ? 'Back to Notifications' : location.state?.from === 'open-tickets' ? 'Back to Open Tickets' : 'Back to Assigned Tickets'}</Link>
        <div className="page-heading ticket-details-header__identity"><span className="eyebrow">{formatTicketReference(ticket)}</span><h2>{ticket.title}</h2><p>Created {formatLocalDateTime(ticket.createdDate)}</p></div>
        <div className="heading-actions ticket-details-header__actions">
          {ticket.canRequestAssignment && <button className="button button--primary" type="button" onClick={() => setDialog('assignment')} disabled={Boolean(saving)}>Request Assignment</button>}
          {ticket.assignmentRequestStatus === 'Pending' && <button className="button button--secondary" type="button" disabled>Assignment Requested</button>}
          {canStart && <button className="button button--primary" type="button" onClick={startProgress} disabled={Boolean(saving)}>{saving === 'progress' ? 'Starting…' : 'Start Work'}</button>}
          {ticket.statusName === 'Pending' && <button className="button button--primary" type="button" onClick={resumeWork} disabled={Boolean(saving)}>{saving === 'resume' ? 'Resuming…' : 'Resume Work'}</button>}
          {ticket.canResolve && <button className="button button--primary" type="button" onClick={() => setDialog('resolve')} disabled={Boolean(saving)}>{saving === 'resolve' ? 'Resolving…' : 'Mark as Resolved'}</button>}
          {ticket.canClose && <button className="button button--primary" type="button" onClick={() => setDialog('close')} disabled={Boolean(saving)}>Close Ticket</button>}
          {hasMoreActions && <div className="agent-more-actions"><button className="button button--secondary agent-more-actions__trigger" type="button" aria-haspopup="menu" aria-expanded={moreActionsOpen} onClick={toggleMoreActions} disabled={Boolean(saving)}>More Actions <span aria-hidden="true">▼</span></button>{moreActionsOpen && moreActionsPosition && createPortal(<div className="cancellation-action-menu__dropdown agent-more-actions__dropdown" style={moreActionsPosition} role="menu">{canMarkPending && <button className="cancellation-action-menu__item" type="button" role="menuitem" onClick={() => { setMoreActionsOpen(false); setDialog('pending') }}>Mark as Pending</button>}{canMarkPending && canRequestCancellation && <div className="cancellation-action-menu__divider" role="separator" />}{canRequestCancellation && <button className="cancellation-action-menu__item cancellation-action-menu__item--danger" type="button" role="menuitem" onClick={() => { setMoreActionsOpen(false); setDialog('cancellation') }}>Request Cancellation</button>}</div>, document.body)}</div>}
        </div>
      </section>
      {ticket.pendingCancellationRequest && <div className="inline-alert cancellation-request-banner" role="status"><strong>Cancellation Requested — Pending Manager Review</strong><span>Submitted {formatLocalDateTime(ticket.pendingCancellationRequest.requestedDate)}. The ticket remains {ticket.statusName} while the request is reviewed.</span></div>}
      {ticket.statusName === 'Pending' && ticket.currentPending && <section className="pending-info-panel" aria-labelledby="pending-info-title"><div><span className="pending-info-panel__indicator" aria-hidden="true" /><div><h2 id="pending-info-title">Work Pending</h2><p>{ticket.currentPending.reasonText}</p></div></div><dl><div><dt>Pending since</dt><dd>{formatLocalDateTime(ticket.currentPending.pendingSince)}</dd></div><div><dt>Set by</dt><dd>{ticket.currentPending.setByName}</dd></div>{ticket.currentPending.additionalNote && <div><dt>Additional note</dt><dd>{ticket.currentPending.additionalNote}</dd></div>}</dl></section>}
      {ticket.statusName === 'Duplicate' && ticket.originalTicketReference && <section className="duplicate-info-panel" aria-labelledby="duplicate-info-title"><h2 id="duplicate-info-title">Duplicate Ticket</h2><p>This ticket was marked as a duplicate of:</p><Link className="duplicate-info-panel__link" to={`/agent/tickets/${ticket.originalTicketReference}`}><strong>{ticket.originalTicketReference}</strong><span>{ticket.originalTicketTitle || 'View original ticket'}</span></Link></section>}
      <div className="details-grid">
        <section className="panel">
          <h2>Original Ticket Information</h2>
          <p className="ticket-description">{ticket.description}</p>
          {ticket.resolutionSummary && <div className="resolution-summary"><strong>Resolution summary</strong><p>{ticket.resolutionSummary}</p></div>}
          <TicketAttachments attachments={ticket.attachments} onDownload={downloadAttachment} showEmpty={false} />
        </section>
        <aside className="panel details-side">
          <h2>Ticket Information</h2>
          <dl>
            <div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div>
            <div><dt>Category</dt><dd>{ticket.categoryName}</dd></div>
            <div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div>
            <div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div>
            <div><dt>Assigned agent</dt><dd>{ticket.assignedAgentName ?? 'Unassigned'}</dd></div>
            <div><dt>Created</dt><dd><time dateTime={ticket.createdDate} title="Displayed in your local time">{formatLocalDateTime(ticket.createdDate)}</time></dd></div>
            {ticket.resolvedDate && <div><dt>Resolved</dt><dd><time dateTime={ticket.resolvedDate} title="Displayed in your local time">{formatLocalDateTime(ticket.resolvedDate)}</time></dd></div>}
            {ticket.closedDate && <div><dt>Closed</dt><dd><time dateTime={ticket.closedDate} title="Displayed in your local time">{formatLocalDateTime(ticket.closedDate)}</time></dd></div>}
          </dl>
        </aside>
      </div>
      <div className="ticket-detail-section-stack">
      <TicketComments
        comments={ticket.comments}
        endpoint={`/api/agent/tickets/${encodeURIComponent(ticket.ticketReferenceNumber)}/comments`}
        canViewPrivate={Boolean(ticket.assignedAgentName)}
        canComment={ticket.canComment}
        readOnlyMessage={ticket.assignedAgentName
          ? 'Comments are read-only because this ticket is completed.'
          : null}
        onNotify={notify}
      />
      <TicketActivityLog ticketReference={ticket.ticketReferenceNumber} />
      <TicketHistorySection history={ticket.history} />
      </div>
      {dialog && <>
        <button className="dialog-backdrop" type="button" aria-label="Close dialog" onClick={() => { if (!saving) setDialog(null) }} />
        <section className="dialog" role="dialog" aria-modal="true" aria-labelledby="agent-action-title" aria-describedby="agent-action-description">
          <h2 id="agent-action-title">{dialog === 'assignment' ? 'Request Assignment' : dialog === 'cancellation' ? 'Request ticket cancellation' : dialog === 'close' ? 'Close Ticket' : dialog === 'pending' ? 'Mark as Pending' : 'Mark Ticket as Resolved'}</h2>
          <p id="agent-action-description">{dialog === 'assignment' ? 'Request assignment to this ticket? A Manager will review your request before the ticket is assigned to you.' : dialog === 'cancellation' ? 'Please provide a reason for requesting cancellation of this ticket.' : dialog === 'close' ? `Are you sure you want to close ${ticket.ticketReferenceNumber}? This confirms that the work has been completed.` : dialog === 'pending' ? 'Pause active work and record what the ticket is waiting for.' : 'Describe the technical resolution before marking this ticket as resolved.'}</p>
          {dialog === 'assignment' ? null : dialog === 'cancellation' ? <label><span>Reason</span><textarea autoFocus required value={cancellationReason} onChange={(event) => setCancellationReason(event.target.value)} maxLength="1000" placeholder="Provide a clear reason for requesting cancellation..." disabled={Boolean(saving)} /></label> : dialog === 'pending' ? <div className="pending-dialog-fields"><label><span>Pending reason</span><select autoFocus value={pendingReason} onChange={(event) => setPendingReason(event.target.value)} disabled={Boolean(saving)}><option value="">Select a reason</option><option value="employee-response">Waiting for employee response</option><option value="manager-approval">Waiting for manager approval</option><option value="vendor">Waiting for vendor</option><option value="hardware">Waiting for hardware</option><option value="software-license">Waiting for software license</option><option value="other">Other</option></select></label>{pendingReason === 'other' && <label><span>Custom reason</span><input value={customPendingReason} onChange={(event) => setCustomPendingReason(event.target.value)} maxLength="300" disabled={Boolean(saving)} /></label>}<label><span>Additional note (optional)</span><textarea value={pendingNote} onChange={(event) => setPendingNote(event.target.value)} maxLength="1000" disabled={Boolean(saving)} /></label></div> : dialog === 'resolve'
            ? <label><span>Resolution summary</span><textarea autoFocus value={resolutionSummary} onChange={(event) => setResolutionSummary(event.target.value)} minLength="10" maxLength="5000" disabled={Boolean(saving)} /></label>
            : <label><span>Closing note (optional)</span><textarea autoFocus value={closingNote} onChange={(event) => setClosingNote(event.target.value)} maxLength="500" disabled={Boolean(saving)} /></label>}
          <div className="dialog__actions"><button className="button button--secondary" type="button" onClick={() => setDialog(null)} disabled={Boolean(saving)}>Cancel</button><button className="button button--primary" type="button" onClick={dialog === 'assignment' ? requestAssignment : dialog === 'cancellation' ? requestCancellation : dialog === 'close' ? closeTicket : dialog === 'pending' ? markPending : resolveTicket} disabled={Boolean(saving) || (dialog === 'cancellation' && !cancellationReason.trim()) || (dialog === 'resolve' && resolutionSummary.trim().length < 10) || (dialog === 'pending' && (!pendingReason || (pendingReason === 'other' && !customPendingReason.trim())))}>{saving === 'assignment' ? 'Submitting…' : saving === 'cancellation' ? 'Submitting…' : saving === 'pending' ? 'Moving to Pending…' : saving ? 'Saving…' : dialog === 'assignment' ? 'Submit Request' : dialog === 'cancellation' ? 'Submit Request' : dialog === 'close' ? 'Close Ticket' : dialog === 'pending' ? 'Mark as Pending' : 'Mark as Resolved'}</button></div>
        </section>
      </>}
    </>
  )
}

export default AgentTicketDetailsPage
