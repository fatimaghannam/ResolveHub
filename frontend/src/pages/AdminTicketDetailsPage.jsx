import { ArrowLeft } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import TicketComments from '../components/tickets/TicketComments.jsx'
import {
  addAdminTicketComment,
  getAdminTicket,
  markAdminTicketDuplicate,
  reviewAdminDuplicate,
} from '../services/adminService.js'
import { addManagerTicketComment, getManagerTicket, reportManagerDuplicate } from '../services/managerService.js'
import { formatLocalDate } from '../utils/dateTime.js'

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
  const [duplicateConfirmed, setDuplicateConfirmed] = useState(false)
  const [loadingOriginal, setLoadingOriginal] = useState(false)
  const [comment, setComment] = useState('')
  const [addingComment, setAddingComment] = useState(false)
  const duplicateTriggerRef = useRef(null)
  const location = useLocation()
  const [toast, setToast] = useState(() => {
    const notification = location.state?.toast
    return notification ? { id: Date.now(), ...notification } : null
  })
  const dismissToast = useCallback(() => setToast(null), [])

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

  async function reportDuplicate() {
    if (!originalReference.trim() || processingDuplicate) return
    try {
      setProcessingDuplicate(true)
      setDuplicateError('')
      const review = await reportManagerDuplicate(ticket.ticketReferenceNumber, {
        suggestedOriginalTicketReference: originalReference.trim(),
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
      await reviewAdminDuplicate(ticket.pendingDuplicateReview.id, decision)
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
      setOriginalTicketPreview(await getAdminTicket(reference))
      setDuplicateConfirmed(false)
    } catch (requestError) {
      setOriginalTicketPreview(null)
      setDuplicateError(requestError.message)
    } finally {
      setLoadingOriginal(false)
    }
  }

  async function markDuplicate() {
    if (processingDuplicate || !originalTicketPreview ||
        !duplicateReason.trim() || !duplicateConfirmed) return
    try {
      setProcessingDuplicate(true)
      setDuplicateError('')
      await markAdminTicketDuplicate(ticket.ticketReferenceNumber, {
        originalTicketReference: originalTicketPreview.ticketReferenceNumber,
        reason: duplicateReason.trim(),
        confirmed: true,
      })
      setTicket(await getAdminTicket(ticket.ticketReferenceNumber))
      closeDuplicateDialog()
      setToast({ id: Date.now(), type: 'success', title: 'Duplicate Marked', message: `${ticket.ticketReferenceNumber} was marked as a duplicate of ${originalTicketPreview.ticketReferenceNumber}.` })
    } catch (requestError) {
      setDuplicateError(requestError.message)
    } finally {
      setProcessingDuplicate(false)
    }
  }

  async function addComment(event) {
    event.preventDefault()
    if (!comment.trim() || addingComment) return
    try {
      setAddingComment(true)
      const addCommentRequest = roleArea === 'manager'
        ? addManagerTicketComment
        : addAdminTicketComment
      const created = await addCommentRequest(
        ticket.ticketReferenceNumber, comment.trim())
      setTicket((current) => ({
        ...current,
        comments: [...current.comments, created],
      }))
      setComment('')
      setToast({
        id: Date.now(),
        type: 'success',
        title: 'Comment Added',
        message: 'Your Public comment was added.',
      })
    } catch (requestError) {
      setToast({
        id: Date.now(),
        type: 'error',
        title: 'Unable to Add Comment',
        message: requestError.message,
      })
    } finally {
      setAddingComment(false)
    }
  }

  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <Link className="back-link back-link--top" to={`/${roleArea}/tickets`}><ArrowLeft size={18} />Back to All Tickets</Link>
      <section className="page-heading page-heading--action">
        <div><span className="eyebrow">{ticket.ticketReferenceNumber}</span><h2>{ticket.title}</h2><p>Created {formatLocalDate(ticket.createdDate)}</p></div>
        {roleArea === 'manager' && !ticket.pendingDuplicateReview && ticket.statusName !== 'Duplicate' && <button ref={duplicateTriggerRef} className="button button--secondary" type="button" onClick={openDuplicateDialog}>Report Possible Duplicate</button>}
        {roleArea === 'admin' && ticket.statusName !== 'Duplicate' && <button ref={duplicateTriggerRef} className="button button--secondary" type="button" onClick={openDuplicateDialog}>{ticket.pendingDuplicateReview ? 'Review Duplicate' : 'Mark as Duplicate'}</button>}
      </section>
      {ticket.pendingDuplicateReview && <div className="inline-alert"><span className="badge badge--pending-approval">Duplicate Review Pending</span></div>}
      {ticket.statusName === 'Duplicate' && ticket.originalTicketReference && <section className="duplicate-info-panel" aria-labelledby="duplicate-info-title"><h2 id="duplicate-info-title">Duplicate Ticket</h2><p>This ticket was marked as a duplicate of:</p><Link className="duplicate-info-panel__link" to={`/${roleArea}/tickets/${ticket.originalTicketReference}`}><strong>{ticket.originalTicketReference}</strong><span>{ticket.originalTicketTitle || 'View original ticket'}</span></Link>{(ticket.duplicateApprovedDate || ticket.duplicateApprovedByName) && <p className="duplicate-info-panel__meta">Approved{ticket.duplicateApprovedDate ? ` ${formatLocalDate(ticket.duplicateApprovedDate)}` : ''}{ticket.duplicateApprovedByName ? ` by ${ticket.duplicateApprovedByName}` : ''}</p>}</section>}
      <div className="details-grid"><section className="panel"><h2>Ticket Summary</h2><p className="ticket-description">{ticket.description}</p></section><aside className="panel details-side"><h2>Ticket Information</h2><dl><div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div><div><dt>Category</dt><dd>{ticket.categoryName}</dd></div><div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div><div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div>{ticket.originalTicketReference && <div><dt>Original Ticket</dt><dd><Link to={`/${roleArea}/tickets/${ticket.originalTicketReference}`}>{ticket.originalTicketReference}</Link></dd></div>}<div><dt>Assigned agent</dt><dd>{ticket.assignedAgentName ?? 'Unassigned'}</dd></div>{ticket.resolvedDate && <div><dt>Resolved</dt><dd>{formatLocalDate(ticket.resolvedDate)}</dd></div>}{ticket.closedDate && <div><dt>Closed</dt><dd>{formatLocalDate(ticket.closedDate)}</dd></div>}</dl></aside></div>
      <TicketComments
        comments={ticket.comments}
        helperText="You can add Public comments visible to everyone with access to this ticket."
        message={comment}
        onMessageChange={setComment}
        onSubmit={addComment}
        isSubmitting={addingComment}
        canComment={!['Closed', 'Cancelled', 'Duplicate'].includes(ticket.statusName)}
        publicOnly
        readOnlyMessage="Comments are read-only because this ticket is completed."
        formatTimestamp={formatLocalDate}
      />
      <section className="panel dashboard-section">
        <div className="panel__heading"><div><h2>Ticket History</h2><p>Read-only record of important ticket actions.</p></div></div>
        {ticket.history.length === 0
          ? <EmptyState title="No history yet" message="Ticket activity will appear here." />
          : <div className="table-scroll"><table className="ticket-table"><thead><tr><th>Action</th><th>Performed By</th><th>Description</th><th>Date</th></tr></thead><tbody>{ticket.history.map((item) => <tr key={item.id}><td><strong>{item.actionType}</strong></td><td>{item.performedByName}</td><td>{item.description ?? '—'}</td><td>{formatLocalDate(item.createdDate)}</td></tr>)}</tbody></table></div>}
      </section>
      {duplicateDialogOpen && <>
        <button className="dialog-backdrop" type="button" aria-label="Close duplicate dialog" onClick={() => { if (!processingDuplicate) closeDuplicateDialog() }} />
        <section className="dialog dialog--duplicate" role="dialog" aria-modal="true" aria-labelledby="duplicate-title" aria-describedby="duplicate-description">
          <h2 id="duplicate-title">{roleArea === 'manager' ? 'Report Possible Duplicate' : ticket.pendingDuplicateReview ? 'Review Duplicate' : 'Mark as Duplicate'}</h2>
          {roleArea === 'manager' ? <>
            <p id="duplicate-description">Submit this ticket for Administrator review. Its status will not change.</p>
            <label><span>Possible original ticket</span><input autoFocus value={originalReference} onChange={(event) => setOriginalReference(event.target.value)} placeholder="RH-2026-0003" disabled={processingDuplicate} /></label>
            <label><span>Reason (optional)</span><textarea value={duplicateReason} onChange={(event) => setDuplicateReason(event.target.value)} maxLength="1000" disabled={processingDuplicate} /></label>
          </> : ticket.pendingDuplicateReview ? <>
            <p id="duplicate-description">Review the Manager's report before changing the ticket.</p>
            <dl className="duplicate-review-details"><div><dt>Reported Ticket</dt><dd><strong>{ticket.pendingDuplicateReview.reportedTicketReference}</strong><span>{ticket.pendingDuplicateReview.reportedTicketTitle}</span></dd></div><div><dt>Current Status</dt><dd>{ticket.pendingDuplicateReview.reportedTicketStatus}</dd></div><div><dt>Requester</dt><dd>{ticket.pendingDuplicateReview.reportedRequesterName}</dd></div><div><dt>Category</dt><dd>{ticket.pendingDuplicateReview.reportedCategoryName}</dd></div><div><dt>Suspected Original</dt><dd><strong>{ticket.pendingDuplicateReview.suggestedOriginalTicketReference}</strong><span>{ticket.pendingDuplicateReview.suggestedOriginalTicketTitle}</span></dd></div><div><dt>Original Status</dt><dd>{ticket.pendingDuplicateReview.suggestedOriginalTicketStatus}</dd></div><div><dt>Original Requester</dt><dd>{ticket.pendingDuplicateReview.suggestedOriginalRequesterName}</dd></div><div><dt>Original Category</dt><dd>{ticket.pendingDuplicateReview.suggestedOriginalCategoryName}</dd></div><div><dt>Reported By</dt><dd>{ticket.pendingDuplicateReview.reportedByName}</dd></div><div><dt>Report Date</dt><dd>{formatLocalDate(ticket.pendingDuplicateReview.createdDate)}</dd></div><div><dt>Reason</dt><dd>{ticket.pendingDuplicateReview.reason || 'No reason provided.'}</dd></div></dl>
          </> : <>
            <p id="duplicate-description">Choose and compare the original ticket, provide a reason, then confirm this permanent action.</p>
            <label><span>Original ticket</span><span className="duplicate-reference-field"><input autoFocus value={originalReference} onChange={(event) => { setOriginalReference(event.target.value); setOriginalTicketPreview(null); setDuplicateConfirmed(false) }} placeholder="RH-2026-0003" disabled={processingDuplicate || loadingOriginal} /><button className="button button--secondary" type="button" onClick={loadOriginalTicket} disabled={!originalReference.trim() || loadingOriginal || processingDuplicate}>{loadingOriginal ? 'Loading…' : 'Compare'}</button></span></label>
            {originalTicketPreview && <dl className="duplicate-review-details"><div><dt>Reported Ticket</dt><dd><strong>{ticket.ticketReferenceNumber}</strong><span>{ticket.title}</span></dd></div><div><dt>Current Status</dt><dd>{ticket.statusName}</dd></div><div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div><div><dt>Category</dt><dd>{ticket.categoryName}</dd></div><div><dt>Original Ticket</dt><dd><strong>{originalTicketPreview.ticketReferenceNumber}</strong><span>{originalTicketPreview.title}</span></dd></div><div><dt>Original Status</dt><dd>{originalTicketPreview.statusName}</dd></div><div><dt>Original Requester</dt><dd>{originalTicketPreview.requesterName}</dd></div><div><dt>Original Category</dt><dd>{originalTicketPreview.categoryName}</dd></div></dl>}
            <label><span>Reason</span><textarea value={duplicateReason} onChange={(event) => setDuplicateReason(event.target.value)} maxLength="1000" required disabled={processingDuplicate} /></label>
            <label className="duplicate-confirmation"><input type="checkbox" checked={duplicateConfirmed} onChange={(event) => setDuplicateConfirmed(event.target.checked)} disabled={!originalTicketPreview || processingDuplicate} /><span>I confirm this ticket should be permanently marked as a duplicate.</span></label>
          </>}
          {duplicateError && <div className="inline-alert inline-alert--error" role="alert">{duplicateError}</div>}
          <div className="dialog__actions"><button autoFocus={roleArea === 'admin' && Boolean(ticket.pendingDuplicateReview)} className="button button--secondary" type="button" onClick={closeDuplicateDialog} disabled={processingDuplicate}>Cancel</button>{roleArea === 'manager' ? <button className="button button--primary" type="button" onClick={reportDuplicate} disabled={!originalReference.trim() || processingDuplicate}>{processingDuplicate ? 'Submitting…' : 'Submit Report'}</button> : ticket.pendingDuplicateReview ? <><button className="button button--danger-outline" type="button" onClick={() => reviewDuplicate('reject')} disabled={processingDuplicate}>Reject Report</button><button className="button button--primary" type="button" onClick={() => reviewDuplicate('approve')} disabled={processingDuplicate}>{processingDuplicate ? 'Reviewing…' : 'Approve Duplicate'}</button></> : <button className="button button--primary" type="button" onClick={markDuplicate} disabled={!originalTicketPreview || !duplicateReason.trim() || !duplicateConfirmed || processingDuplicate}>{processingDuplicate ? 'Marking…' : 'Mark as Duplicate'}</button>}</div>
        </section>
      </>}
    </>
  )
}

export default AdminTicketDetailsPage
