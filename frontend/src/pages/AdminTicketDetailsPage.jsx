import { ArrowLeft } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import {
  getAdminTicket,
  removeAdminDuplicateTicket,
} from '../services/adminService.js'
import { addManagerTicketComment, getManagerTicket } from '../services/managerService.js'
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
  const [comment, setComment] = useState('')
  const [addingComment, setAddingComment] = useState(false)
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
          toast: {
            type: 'success',
            title: 'Duplicate Removed',
            message: `${ticket.ticketReferenceNumber} was removed as a duplicate of ${originalReference.trim()}.`,
          },
        },
      })
    } catch (requestError) {
      setDuplicateError(requestError.message)
    } finally {
      setRemovingDuplicate(false)
    }
  }

  async function addComment(event) {
    event.preventDefault()
    if (!comment.trim() || addingComment) return
    try {
      setAddingComment(true)
      const created = await addManagerTicketComment(
        ticket.ticketReferenceNumber,
        comment.trim(),
      )
      setTicket((current) => ({
        ...current,
        comments: [...current.comments, created],
        history: [...current.history, {
          id: `comment-${created.id}`,
          actionType: 'Manager Comment Added',
          performedByName: created.authorName,
          description: 'A Manager comment was added.',
          createdDate: created.createdDate,
        }],
      }))
      setComment('')
      setToast({
        id: Date.now(),
        type: 'success',
        title: 'Comment Added',
        message: 'Your comment was added successfully.',
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
        {roleArea === 'admin' && <button className="button button--danger-outline" type="button" onClick={() => { setDuplicateError(''); setDuplicateDialogOpen(true) }}>Remove Duplicate</button>}
      </section>
      <div className="details-grid"><section className="panel"><h2>Ticket Summary</h2><p className="ticket-description">{ticket.description}</p></section><aside className="panel details-side"><h2>Ticket Information</h2><dl><div><dt>Requester</dt><dd>{ticket.requesterName}</dd></div><div><dt>Category</dt><dd>{ticket.categoryName}</dd></div><div><dt>Priority</dt><dd><TicketPriorityBadge value={ticket.priorityName} /></dd></div><div><dt>Status</dt><dd><TicketStatusBadge value={ticket.statusName} /></dd></div><div><dt>Assigned agent</dt><dd>{ticket.assignedAgentName ?? 'Unassigned'}</dd></div>{ticket.resolvedDate && <div><dt>Resolved</dt><dd>{formatLocalDate(ticket.resolvedDate)}</dd></div>}{ticket.closedDate && <div><dt>Closed</dt><dd>{formatLocalDate(ticket.closedDate)}</dd></div>}</dl></aside></div>
      {roleArea === 'manager' && <section className="panel dashboard-section">
        <div className="panel__heading"><div><h2>Comments</h2><p>Public updates visible to the requester and assigned IT Agent.</p></div></div>
        <div className="agent-message-list">{ticket.comments.map((item) => <article key={item.id}><strong>{item.authorName}</strong><p>{item.content}</p><small>{formatLocalDate(item.createdDate)}</small></article>)}</div>
        <form className="agent-message-form" onSubmit={addComment}>
          <label htmlFor="manager-comment">Add comment</label>
          <textarea id="manager-comment" value={comment} onChange={(event) => setComment(event.target.value)} maxLength="5000" disabled={addingComment} />
          <button className="button button--secondary" type="submit" disabled={!comment.trim() || addingComment}>{addingComment ? 'Adding…' : 'Add Comment'}</button>
        </form>
      </section>}
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
