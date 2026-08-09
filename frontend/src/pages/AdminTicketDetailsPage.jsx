import { ArrowLeft } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import TicketComments from '../components/tickets/TicketComments.jsx'
import TicketAttachments from '../components/tickets/TicketAttachments.jsx'
import TicketActivityLog from '../components/tickets/TicketActivityLog.jsx'
import TicketHistorySection from '../components/tickets/TicketHistorySection.jsx'
import {
  getAdminTicket,
  markAdminTicketDuplicate,
  reviewAdminDuplicate,
} from '../services/adminService.js'
import { getManagerTicket, reportManagerDuplicate } from '../services/managerService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'
import { downloadAttachment } from '../services/ticketService.js'

function getDirectDuplicateError(error) {
  if (error.status === 404) {
    return 'Reported or original ticket could not be found.'
  }
  if (error.status === 0) {
    return 'The server could not be reached.'
  }
  return error.message
}

function TicketComparison({ reported, original }) {
  const fields = [
    ['Ticket Number', 'ticketReferenceNumber'],
    ['Title', 'title'],
    ['Requester', 'requesterName'],
    ['Category', 'categoryName'],
    ['Priority', 'priorityName'],
    ['Status', 'statusName'],
  ]
  function ComparisonCard({ label, item }) {
    return <article className="duplicate-comparison-card"><h3>{label}</h3><dl>{fields.map(([name, key]) => <div key={key}><dt>{name}</dt><dd>{key === 'statusName' ? <TicketStatusBadge value={item[key]} /> : key === 'priorityName' ? <TicketPriorityBadge value={item[key]} /> : item[key]}</dd></div>)}<div><dt>Created Date</dt><dd>{formatLocalDateTime(item.createdDate)}</dd></div></dl></article>
  }
  return <div className="duplicate-comparison"><ComparisonCard label="Reported Ticket" item={reported} /><ComparisonCard label="Original Ticket" item={original} /></div>
}

function AdminTicketDetailsPage({ roleArea = 'admin' }) {
  const { ticketReference } = useParams()
  const navigate = useNavigate()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [duplicateDialogOpen, setDuplicateDialogOpen] = useState(false)
  const [originalReference, setOriginalReference] = useState('')
  const [duplicateReason, setDuplicateReason] = useState('')
  const [processingDuplicate, setProcessingDuplicate] = useState(false)
  const [duplicateError, setDuplicateError] = useState('')
  const [originalTicketPreview, setOriginalTicketPreview] = useState(null)
  const [loadingOriginal, setLoadingOriginal] = useState(false)
  const [reviewNote, setReviewNote] = useState('')
  const [duplicateConfirmed, setDuplicateConfirmed] = useState(false)
  const duplicateTriggerRef = useRef(null)
  const location = useLocation()
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

  const closeDuplicateDialog = useCallback(() => {
    setDuplicateDialogOpen(false)
    requestAnimationFrame(() => duplicateTriggerRef.current?.focus())
  }, [])

  function openDuplicateDialog() {
    setDuplicateError('')
    setOriginalReference('')
    setDuplicateReason('')
    setReviewNote('')
    setOriginalTicketPreview(null)
    setDuplicateConfirmed(false)
    setDuplicateDialogOpen(true)
  }

  useEffect(() => {
    if (!duplicateDialogOpen) return undefined
    const onKeyDown = (event) => {
      if (event.key === 'Escape' && !processingDuplicate) closeDuplicateDialog()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [closeDuplicateDialog, duplicateDialogOpen, processingDuplicate])

  if (loading) return <LoadingState message="Loading ticket details…" />
  if (error) return <ErrorState message={error} />
  if (!ticket) return <EmptyState title="Ticket not found" message="This ticket is not available." action={<Link className="button button--secondary" to={`/${roleArea}/tickets`}>Back to All Tickets</Link>} />

  async function downloadTicketAttachment(file) {
    try {
      const blob = await downloadAttachment(ticket.id, file.id)
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

  async function reportDuplicate() {
    if (!originalTicketPreview || processingDuplicate) return
    try {
      setProcessingDuplicate(true)
      setDuplicateError('')
      const review = await reportManagerDuplicate(ticket.ticketReferenceNumber, {
        suggestedOriginalTicketReference: originalTicketPreview.ticketReferenceNumber,
        reason: duplicateReason.trim() || null,
      })
      setTicket((current) => ({ ...current, pendingDuplicateReview: review }))
      closeDuplicateDialog()
      setToast({ id: Date.now(), type: 'success', title: 'Duplicate Review Submitted', message: 'Duplicate review request submitted successfully.' })
    } catch (requestError) {
      setDuplicateError(requestError.message)
    } finally {
      setProcessingDuplicate(false)
    }
  }

  async function reviewDuplicate(decision) {
    if (processingDuplicate || !ticket.pendingDuplicateReview) return
    try {
      setProcessingDuplicate(true)
      setDuplicateError('')
      await reviewAdminDuplicate(
        ticket.pendingDuplicateReview.id, decision, reviewNote.trim() || null)
      const approved = decision === 'approve'
      const original = ticket.pendingDuplicateReview.suggestedOriginalTicketReference
      const originalTitle = ticket.pendingDuplicateReview.suggestedOriginalTicketTitle
      setTicket((current) => ({ ...current, statusName: approved ? 'Duplicate' : current.statusName, originalTicketReference: approved ? original : current.originalTicketReference, originalTicketTitle: approved ? originalTitle : current.originalTicketTitle, pendingDuplicateReview: null }))
      closeDuplicateDialog()
      setToast({ id: Date.now(), type: 'success', title: approved ? 'Duplicate Approved' : 'Duplicate Report Rejected', message: approved ? `${ticket.ticketReferenceNumber} was marked as a duplicate of ${original}.` : 'Duplicate report rejected.' })
    } catch (requestError) {
      setDuplicateError(requestError.message)
    } finally {
      setProcessingDuplicate(false)
    }
  }

  async function loadOriginalTicket() {
    const reference = originalReference.trim()
    if (!reference || loadingOriginal) return
    if (reference.toLowerCase() === ticket.ticketReferenceNumber.toLowerCase()) {
      setDuplicateError('A ticket cannot be reported as a duplicate of itself.')
      setOriginalTicketPreview(null)
      return
    }
    try {
      setLoadingOriginal(true)
      setDuplicateError('')
      const loadTicket = roleArea === 'manager' ? getManagerTicket : getAdminTicket
      setOriginalTicketPreview(await loadTicket(reference))
      setDuplicateConfirmed(false)
    } catch (requestError) {
      setOriginalTicketPreview(null)
      setDuplicateError(requestError.message)
    } finally {
      setLoadingOriginal(false)
    }
  }

  async function markDuplicate() {
    if (processingDuplicate || !originalTicketPreview || !duplicateConfirmed) return
    try {
      setProcessingDuplicate(true)
      setDuplicateError('')
      await markAdminTicketDuplicate(ticket.ticketReferenceNumber, {
        originalTicketReference: originalTicketPreview.ticketReferenceNumber,
        reason: duplicateReason.trim() || null,
        confirmed: true,
      })
      setTicket(await getAdminTicket(ticket.ticketReferenceNumber))
      closeDuplicateDialog()
      setToast({ id: Date.now(), type: 'success', title: 'Duplicate Marked', message: 'Ticket marked as duplicate.' })
    } catch (requestError) {
      setDuplicateError(getDirectDuplicateError(requestError))
    } finally {
      setProcessingDuplicate(false)
    }
  }

  const reviewingDuplicate = roleArea === 'admin' && Boolean(ticket.pendingDuplicateReview) && !ticket.pendingDuplicateReview.reportedByAdministrator
  const markingDuplicate = roleArea === 'admin' && !reviewingDuplicate
  const reviewReportedTicket = reviewingDuplicate ? {
    ticketReferenceNumber: ticket.pendingDuplicateReview.reportedTicketReference,
    title: ticket.pendingDuplicateReview.reportedTicketTitle,
    requesterName: ticket.pendingDuplicateReview.reportedRequesterName,
    categoryName: ticket.pendingDuplicateReview.reportedCategoryName,
    priorityName: ticket.pendingDuplicateReview.reportedTicketPriority,
    statusName: ticket.pendingDuplicateReview.reportedTicketStatus,
    createdDate: ticket.pendingDuplicateReview.reportedTicketCreatedDate,
  } : null
  const reviewOriginalTicket = reviewingDuplicate ? {
    ticketReferenceNumber: ticket.pendingDuplicateReview.suggestedOriginalTicketReference,
    title: ticket.pendingDuplicateReview.suggestedOriginalTicketTitle,
    requesterName: ticket.pendingDuplicateReview.suggestedOriginalRequesterName,
    categoryName: ticket.pendingDuplicateReview.suggestedOriginalCategoryName,
    priorityName: ticket.pendingDuplicateReview.suggestedOriginalTicketPriority,
    statusName: ticket.pendingDuplicateReview.suggestedOriginalTicketStatus,
    createdDate: ticket.pendingDuplicateReview.suggestedOriginalTicketCreatedDate,
  } : null
  const cancellationRequest = roleArea === 'manager' &&
    location.state?.cancellationRequest?.ticketReferenceNumber === ticket.ticketReferenceNumber
    ? location.state.cancellationRequest
    : null
  const fromCancellationRequests = roleArea === 'manager' &&
    location.state?.from === 'cancellation-requests'
  const fromAssignmentApprovals = roleArea === 'admin' &&
    location.state?.from === 'assignment-approvals'
  const fromAgentWorkloadTickets = location.state?.from === 'agent-workload-tickets' &&
    typeof location.state?.backTo === 'string' &&
    location.state.backTo.startsWith(`/${roleArea}/workload/`)
  const workloadBackState = fromAgentWorkloadTickets &&
    location.state?.origin === 'admin-assignments-workload'
    ? { from: 'admin-assignments-workload' }
    : undefined
  const fromNotifications = location.state?.from === 'notifications'
  const backTarget = fromNotifications
    ? `/${roleArea}/notifications`
    : fromCancellationRequests
      ? '/manager/assignments#cancellation-requests'
      : fromAssignmentApprovals
        ? '/admin/assignments#assignment-approvals'
        : fromAgentWorkloadTickets
          ? location.state.backTo
          : `/${roleArea}/tickets`
  const backLabel = fromNotifications
    ? 'Back to Notifications'
    : fromCancellationRequests
      ? 'Back to Cancellation Requests'
      : fromAssignmentApprovals
        ? 'Back to Assignment Approvals'
        : fromAgentWorkloadTickets
          ? 'Back to Agent Workload Tickets'
          : 'Back to All Tickets'

  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <Link className="back-link back-link--top" to={backTarget} state={workloadBackState}><ArrowLeft size={18} />{backLabel}</Link>
      <section className="page-heading page-heading--action">
        <div><span className="eyebrow">{ticket.ticketReferenceNumber}</span><h2>{ticket.title}</h2><p>Created {formatLocalDateTime(ticket.createdDate)}</p></div>
        {roleArea === 'manager' && !ticket.pendingDuplicateReview && ticket.statusName !== 'Duplicate' && <button ref={duplicateTriggerRef} className="button button--secondary" type="button" onClick={openDuplicateDialog}>Report Possible Duplicate</button>}
        {roleArea === 'admin' && markingDuplicate && ticket.statusName !== 'Duplicate' && <button ref={duplicateTriggerRef} className="button button--secondary" type="button" onClick={openDuplicateDialog}>Mark as Duplicate</button>}
        {roleArea === 'admin' && reviewingDuplicate && ticket.statusName !== 'Duplicate' && <button ref={duplicateTriggerRef} className="button button--secondary" type="button" onClick={openDuplicateDialog}>Review Duplicate Report</button>}
      </section>
      {ticket.pendingDuplicateReview && !(roleArea === 'admin' && ticket.pendingDuplicateReview.reportedByAdministrator) && <div className="inline-alert"><span className="badge badge--pending-approval">Duplicate Review Pending</span></div>}
      {ticket.statusName === 'Duplicate' && ticket.originalTicketReference && <section className="duplicate-info-panel" aria-labelledby="duplicate-info-title"><h2 id="duplicate-info-title">Duplicate Ticket</h2><p>This ticket was marked as a duplicate of:</p><Link className="duplicate-info-panel__link" to={`/${roleArea}/tickets/${ticket.originalTicketReference}`}><strong>{ticket.originalTicketReference}</strong><span>{ticket.originalTicketTitle || 'View original ticket'}</span></Link>{(ticket.duplicateApprovedDate || ticket.duplicateApprovedByName) && <p className="duplicate-info-panel__meta">Approved{ticket.duplicateApprovedDate ? ` ${formatLocalDateTime(ticket.duplicateApprovedDate)}` : ''}{ticket.duplicateApprovedByName ? ` by ${ticket.duplicateApprovedByName}` : ''}</p>}</section>}
      <div className="details-grid">
        <section className="panel">
          <h2>Ticket Summary</h2>
          <p className="ticket-description">{ticket.description}</p>
          <TicketAttachments attachments={ticket.attachments} onDownload={downloadTicketAttachment} />
        </section>
        <aside className="panel details-side"><h2>Ticket Information</h2><dl><div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div><div><dt>Category</dt><dd>{ticket.categoryName}</dd></div><div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div><div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div>{ticket.originalTicketReference && <div><dt>Original Ticket</dt><dd><Link to={`/${roleArea}/tickets/${ticket.originalTicketReference}`}>{ticket.originalTicketReference}</Link></dd></div>}<div><dt>Assigned agent</dt><dd>{ticket.assignedAgentName ?? 'Unassigned'}</dd></div><div><dt>Created</dt><dd><time dateTime={ticket.createdDate} title="Displayed in your local time">{formatLocalDateTime(ticket.createdDate)}</time></dd></div>{ticket.resolvedDate && <div><dt>Resolved</dt><dd><time dateTime={ticket.resolvedDate} title="Displayed in your local time">{formatLocalDateTime(ticket.resolvedDate)}</time></dd></div>}{ticket.closedDate && <div><dt>Closed</dt><dd><time dateTime={ticket.closedDate} title="Displayed in your local time">{formatLocalDateTime(ticket.closedDate)}</time></dd></div>}</dl></aside>
      </div>
      {cancellationRequest && <section className="panel manager-cancellation-request" aria-labelledby="manager-cancellation-request-title"><h2 id="manager-cancellation-request-title">Cancellation Request</h2><dl><div><dt>Requested by</dt><dd>{cancellationRequest.requestedByAgentName}</dd></div><div><dt>Requested on</dt><dd>{formatLocalDateTime(cancellationRequest.requestedDate)}</dd></div><div><dt>Status</dt><dd>{cancellationRequest.status === 'Pending' ? 'Pending Manager Review' : cancellationRequest.status}</dd></div></dl><div className="manager-cancellation-request__reason"><h3>Reason</h3><p>{cancellationRequest.reason}</p></div></section>}
      <div className="ticket-detail-section-stack">
      <TicketComments
        comments={ticket.comments}
        endpoint={`/api/${roleArea}/tickets/${encodeURIComponent(ticket.ticketReferenceNumber)}/comments`}
        canViewPrivate={false}
        canComment={!['Closed', 'Cancelled', 'Duplicate'].includes(ticket.statusName)}
        readOnlyMessage="Comments are read-only because this ticket is completed."
        onNotify={notify}
      />
      <TicketActivityLog ticketReference={ticket.ticketReferenceNumber} />
      <TicketHistorySection history={ticket.history} />
      </div>
      {duplicateDialogOpen && <>
        <button className="dialog-backdrop" type="button" aria-label="Close duplicate dialog" onClick={() => { if (!processingDuplicate) closeDuplicateDialog() }} />
        <section className="dialog dialog--duplicate" role="dialog" aria-modal="true" aria-labelledby="duplicate-title" aria-describedby="duplicate-description">
          <h2 id="duplicate-title">{reviewingDuplicate ? 'Review Duplicate Ticket' : markingDuplicate ? 'Mark as Duplicate' : 'Report Possible Duplicate'}</h2>
          {reviewingDuplicate ? <>
            <p id="duplicate-description">Compare the reported ticket with the proposed original ticket. If they represent the same issue, confirm to mark this ticket as a duplicate. This preserves all ticket history and links the duplicate to the original ticket.</p>
            <TicketComparison reported={reviewReportedTicket} original={reviewOriginalTicket} />
            <div className="duplicate-report-meta"><span>Reported by <strong>{ticket.pendingDuplicateReview.reportedByName}</strong></span><span>{formatLocalDateTime(ticket.pendingDuplicateReview.createdDate)}</span></div>
            {ticket.pendingDuplicateReview.reason && <section className="duplicate-reason"><h3>Reporter Reason</h3><p>{ticket.pendingDuplicateReview.reason}</p></section>}
            <label><span>Internal review note (Optional)</span><textarea value={reviewNote} onChange={(event) => setReviewNote(event.target.value)} maxLength="1000" placeholder="Add context for the internal review history..." disabled={processingDuplicate} /></label>
            <section className="duplicate-next-steps"><h3>What happens after approval?</h3><ul><li>This ticket will become Duplicate.</li><li>All history will be preserved.</li><li>Future work should continue on the original ticket.</li><li>The duplicate ticket becomes read-only.</li><li>The duplicate is linked to the original ticket.</li></ul></section>
          </> : markingDuplicate ? <>
            <p id="duplicate-description">Compare this ticket with the original ticket. Confirming will immediately mark this ticket as Duplicate, link it to the original ticket, and make it read-only.</p>
            <label><span>Original Ticket</span><span className="duplicate-reference-field"><input autoFocus value={originalReference} onChange={(event) => { setOriginalReference(event.target.value); setOriginalTicketPreview(null); setDuplicateConfirmed(false) }} placeholder="RH-2026-0034" disabled={processingDuplicate || loadingOriginal} /><button className="button button--secondary" type="button" onClick={loadOriginalTicket} disabled={!originalReference.trim() || loadingOriginal || processingDuplicate}>{loadingOriginal ? 'Loading…' : 'Compare'}</button></span></label>
            {originalTicketPreview && <TicketComparison reported={ticket} original={originalTicketPreview} />}
            <label><span>Reason (Optional)</span><textarea value={duplicateReason} onChange={(event) => setDuplicateReason(event.target.value)} maxLength="1000" placeholder="Provide additional context if needed..." disabled={processingDuplicate} /></label>
            <label className="duplicate-confirmation"><input type="checkbox" checked={duplicateConfirmed} onChange={(event) => setDuplicateConfirmed(event.target.checked)} disabled={!originalTicketPreview || processingDuplicate} /><span>I confirm this ticket should be marked as a duplicate immediately.</span></label>
            <section className="duplicate-next-steps"><h3>What happens now?</h3><ul><li>The ticket status changes to Duplicate immediately.</li><li>The ticket is linked to the selected original ticket.</li><li>All ticket history is preserved.</li><li>The duplicate ticket becomes read-only.</li></ul></section>
          </> : <>
            <p id="duplicate-description">Report this ticket as a possible duplicate. An Administrator will review this request before any ticket status changes are made.</p>
            <label><span>Original Ticket</span><span className="duplicate-reference-field"><input autoFocus value={originalReference} onChange={(event) => { setOriginalReference(event.target.value); setOriginalTicketPreview(null) }} placeholder="RH-2026-0034" disabled={processingDuplicate || loadingOriginal} /><button className="button button--secondary" type="button" onClick={loadOriginalTicket} disabled={!originalReference.trim() || loadingOriginal || processingDuplicate}>{loadingOriginal ? 'Loading…' : 'Compare'}</button></span></label>
            {originalTicketPreview && <TicketComparison reported={ticket} original={originalTicketPreview} />}
            <label><span>Reason (Optional)</span><textarea value={duplicateReason} onChange={(event) => setDuplicateReason(event.target.value)} maxLength="1000" placeholder="Provide additional context if needed..." disabled={processingDuplicate} /></label>
            <section className="duplicate-next-steps"><h3>What happens next?</h3><ul><li>No ticket status changes immediately.</li><li>Administrators will review this request.</li><li>The ticket continues through its normal workflow until a decision is made.</li><li>A history record will be created.</li></ul></section>
          </>}
          {duplicateError && <div className="inline-alert inline-alert--error" role="alert">{duplicateError}</div>}
          <div className="dialog__actions"><button autoFocus={reviewingDuplicate} className="button button--secondary" type="button" onClick={closeDuplicateDialog} disabled={processingDuplicate}>Cancel</button>{reviewingDuplicate ? <><button className="button button--danger-outline" type="button" onClick={() => reviewDuplicate('reject')} disabled={processingDuplicate}>Reject Report</button><button className="button button--primary" type="button" onClick={() => reviewDuplicate('approve')} disabled={processingDuplicate}>{processingDuplicate ? 'Reviewing…' : 'Approve Duplicate'}</button></> : markingDuplicate ? <button className="button button--primary" type="button" onClick={markDuplicate} disabled={!originalTicketPreview || !duplicateConfirmed || processingDuplicate}>{processingDuplicate ? 'Marking…' : 'Mark Duplicate'}</button> : <button className="button button--primary" type="button" onClick={reportDuplicate} disabled={!originalTicketPreview || processingDuplicate}>{processingDuplicate ? 'Submitting…' : 'Submit Duplicate Report'}</button>}</div>
        </section>
      </>}
    </>
  )
}

export default AdminTicketDetailsPage
