import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { ClipboardCheck, Inbox } from 'lucide-react'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import { TicketPriorityBadge } from '../components/tickets/TicketBadges.jsx'
import { AgentWorkloadCard } from '../components/tickets/AgentWorkload.jsx'
import { assignAdminTicket, getAdminAssignmentRequests, getAdminAssignments, reviewAdminAssignmentRequest } from '../services/adminService.js'
import {
  assignManagerTicket,
  getManagerAssignmentRequests,
  getManagerAssignments,
  getManagerCancellationRequests,
  reviewManagerCancellationRequest,
} from '../services/managerService.js'
import { getCategories, getPriorities } from '../services/ticketService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'
import {
  getLocalQuickDateRange,
  getUtcDateRange,
  STANDARD_DATE_RANGE_OPTIONS,
} from '../utils/dateRange.js'

const blankFilters = {
  search: '',
  categoryId: '',
  priorityId: '',
  dateRange: 'all',
  fromDate: '',
  toDate: '',
}
const dateRangeOptions = STANDARD_DATE_RANGE_OPTIONS
const initialWorkloadFilters = {
  search: '',
  capacityState: '',
  workload: '',
  sortBy: 'name-asc',
}

function AssignmentEmptyState({ type = 'requests', title, message }) {
  const Icon = type === 'tickets' ? Inbox : ClipboardCheck
  return <div className="assignment-empty-state"><span className="assignment-empty-state__icon" aria-hidden="true"><Icon size={24} /></span><h3>{title}</h3><p>{message}</p></div>
}
const emptyAgentWorkloads = []
const emptyUnassignedTickets = []

function AdminAssignmentsPage({ roleArea = 'admin' }) {
  const [searchParams] = useSearchParams()
  const location = useLocation()
  const selectedReference = searchParams.get('ticket')
  const unassignedSectionRef = useRef(null)
  const cancellationRequestsSectionRef = useRef(null)
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)
  const [selections, setSelections] = useState({})
  const [toast, setToast] = useState(null)
  const [assigning, setAssigning] = useState('')
  const [draftFilters, setDraftFilters] = useState(blankFilters)
  const [filters, setFilters] = useState(blankFilters)
  const [lookups, setLookups] = useState({ categories: [], priorities: [] })
  const [dateError, setDateError] = useState('')
  const [workloadFilters, setWorkloadFilters] = useState(initialWorkloadFilters)
  const [assignmentRequests, setAssignmentRequests] = useState([])
  const [assignmentRequestError, setAssignmentRequestError] = useState('')
  const [reviewingRequest, setReviewingRequest] = useState('')
  const [rejectingRequest, setRejectingRequest] = useState(null)
  const [rejectionReason, setRejectionReason] = useState('')
  const [cancellationRequests, setCancellationRequests] = useState([])
  const [cancellationRequestError, setCancellationRequestError] = useState('')
  const [reviewingCancellation, setReviewingCancellation] = useState('')
  const [cancellationReview, setCancellationReview] = useState(null)
  const [cancellationReviewNote, setCancellationReviewNote] = useState('')
  const [openCancellationMenu, setOpenCancellationMenu] = useState(null)
  const [cancellationMenuPosition, setCancellationMenuPosition] = useState(null)
  const dismissToast = useCallback(() => setToast(null), [])

  useEffect(() => {
    const controller = new AbortController()
    setAssignmentRequestError('')
    Promise.all([
      getCategories(controller.signal),
      getPriorities(controller.signal),
    ]).then(([categories, priorities]) => {
      if (!controller.signal.aborted) setLookups({ categories, priorities })
    }).catch((requestError) => {
      if (requestError.name !== 'AbortError') setError(requestError.message)
    })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    const loadAssignments = roleArea === 'manager' ? getManagerAssignments : getAdminAssignments
    const { fromUtc, toUtcExclusive } =
      getUtcDateRange(filters.fromDate, filters.toDate)
    loadAssignments({
      search: filters.search,
      categoryId: filters.categoryId,
      priorityId: filters.priorityId,
      fromUtc,
      toUtcExclusive,
    }, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setData(result)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) {
          setError(requestError.message)
        }
      })
    return () => controller.abort()
  }, [reload, roleArea, filters])

  useEffect(() => {
    const controller = new AbortController()
    const loadRequests = roleArea === 'manager' ? getManagerAssignmentRequests : getAdminAssignmentRequests
    loadRequests(controller.signal)
      .then((items) => {
        if (!controller.signal.aborted) setAssignmentRequests(items)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setAssignmentRequestError(requestError.message)
      })
    return () => controller.abort()
  }, [reload, roleArea])

  useEffect(() => {
    if (roleArea !== 'manager') return undefined
    const controller = new AbortController()
    setCancellationRequestError('')
    getManagerCancellationRequests(controller.signal)
      .then((items) => { if (!controller.signal.aborted) setCancellationRequests(items) })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setCancellationRequestError(requestError.message)
      })
    return () => controller.abort()
  }, [reload, roleArea])

  useEffect(() => {
    if (openCancellationMenu === null) return undefined
    function closeCancellationMenu(event) {
      if (!event.target.closest('.cancellation-action-menu, .cancellation-action-menu__dropdown')) setOpenCancellationMenu(null)
    }
    function closeCancellationMenuOnEscape(event) {
      if (event.key === 'Escape') setOpenCancellationMenu(null)
    }
    document.addEventListener('pointerdown', closeCancellationMenu)
    document.addEventListener('keydown', closeCancellationMenuOnEscape)
    return () => {
      document.removeEventListener('pointerdown', closeCancellationMenu)
      document.removeEventListener('keydown', closeCancellationMenuOnEscape)
    }
  }, [openCancellationMenu])

  function toggleCancellationMenu(requestId, event) {
    if (openCancellationMenu === requestId) {
      setOpenCancellationMenu(null)
      return
    }
    const triggerBounds = event.currentTarget.getBoundingClientRect()
    const menuWidth = 196
    const menuHeight = 180
    const viewportMargin = 8
    const openUpward = window.innerHeight - triggerBounds.bottom < menuHeight + viewportMargin
    setCancellationMenuPosition({
      left: Math.max(viewportMargin, Math.min(
        triggerBounds.right - menuWidth,
        window.innerWidth - menuWidth - viewportMargin,
      )),
      top: openUpward
        ? Math.max(viewportMargin, triggerBounds.top - menuHeight - 6)
        : triggerBounds.bottom + 6,
    })
    setOpenCancellationMenu(requestId)
  }

  async function reviewCancellationRequest() {
    if (!cancellationReview || reviewingCancellation) return
    const { request, decision } = cancellationReview
    try {
      setReviewingCancellation(`${request.id}-${decision}`)
      await reviewManagerCancellationRequest(request.id, decision, cancellationReviewNote.trim() || null)
      setCancellationReview(null)
      setCancellationReviewNote('')
      setReload((value) => value + 1)
      setToast({ id: Date.now(), type: 'success', title: 'Cancellation Request Reviewed',
        message: decision === 'reject' ? `${request.ticketReferenceNumber} remains assigned.` : decision === 'cancel' ? `${request.ticketReferenceNumber} was cancelled.` : `${request.ticketReferenceNumber} is ready for reassignment through Administrator approval.` })
    } catch (requestError) {
      setToast({ id: Date.now(), type: 'error', title: 'Review Failed', message: requestError.message })
    } finally {
      setReviewingCancellation('')
    }
  }

  async function reviewRequest(request, decision, reason = null) {
    if (reviewingRequest) return
    try {
      setReviewingRequest(`${request.id}-${decision}`)
      await reviewAdminAssignmentRequest(request.id, decision, reason)
      setReload((value) => value + 1)
      setToast({
        id: Date.now(),
        type: 'success',
        title: decision === 'approve' ? 'Request Approved' : 'Request Rejected',
        message: `${request.ticketReferenceNumber} assignment to ${request.requestedAgentName} was ${decision === 'approve' ? 'approved' : 'rejected'}.`,
      })
    } catch (requestError) {
      setToast({
        id: Date.now(),
        type: 'error',
        title: 'Review Failed',
        message: requestError.message,
      })
    } finally {
      setReviewingRequest('')
      setRejectingRequest(null)
      setRejectionReason('')
    }
  }

  function applyFilters(event) {
    event.preventDefault()
    if (draftFilters.dateRange === 'custom' &&
        (!draftFilters.fromDate || !draftFilters.toDate)) {
      setDateError('Select both a start date and an end date.')
      return
    }
    if (draftFilters.fromDate && draftFilters.toDate <
        draftFilters.fromDate) {
      setDateError('The end date cannot be earlier than the start date.')
      return
    }
    const dates = draftFilters.dateRange === 'custom'
      ? { fromDate: draftFilters.fromDate, toDate: draftFilters.toDate }
      : getLocalQuickDateRange(draftFilters.dateRange)
    const next = { ...draftFilters, ...dates }
    setDateError('')
    setDraftFilters(next)
    setFilters(next)
  }

  function clearFilters() {
    setDraftFilters(blankFilters)
    setFilters(blankFilters)
    setDateError('')
  }

  function changeDateRange(value) {
    const dates = value === 'custom'
      ? { fromDate: '', toDate: '' }
      : getLocalQuickDateRange(value)
    setDraftFilters((current) => ({
      ...current,
      dateRange: value,
      ...dates,
    }))
    setDateError('')
  }

  async function assignTicket(ticket) {
    const agentUserId = Number(selections[ticket.ticketReferenceNumber])
    if (!agentUserId || assigning) return
    const agent = data.agentWorkloads.find((item) => item.userId === agentUserId)
    if (!agent || agent.isAtCapacity) return
    try {
      setAssigning(ticket.ticketReferenceNumber)
      const assign = roleArea === 'manager' ? assignManagerTicket : assignAdminTicket
      const createdRequest = await assign(ticket.ticketReferenceNumber, agentUserId)
      if (roleArea === 'manager' && createdRequest) {
        setAssignmentRequests((current) => [createdRequest, ...current.filter((request) => request.id !== createdRequest.id)])
      }
      setReload((value) => value + 1)
      setToast({
        id: Date.now(),
        type: 'success',
        title: roleArea === 'manager' ? 'Assignment Requested' : 'Ticket Assigned',
        message: roleArea === 'manager'
          ? 'Assignment request submitted for Administrator approval.'
          : `${ticket.ticketReferenceNumber} assigned to ${agent.name}.`,
      })
    } catch (requestError) {
      setToast({
        id: Date.now(),
        type: 'error',
        title: 'Assignment Failed',
        message: requestError.message,
      })
    } finally {
      setAssigning('')
    }
  }

  const unassignedTickets = data?.unassignedTickets ?? emptyUnassignedTickets
  const agentWorkloads = data?.agentWorkloads ?? emptyAgentWorkloads
  const selectedAgent = (ticketReferenceNumber) => {
    const userId = Number(selections[ticketReferenceNumber])
    return agentWorkloads.find((agent) => agent.userId === userId)
  }
  const pendingByTicket = useMemo(() => new Map(assignmentRequests
    .filter((request) => request.status === 'Pending')
    .map((request) => [request.ticketReferenceNumber, request])), [assignmentRequests])
  const visibleAssignmentRequests = useMemo(() => roleArea === 'manager'
    ? assignmentRequests.filter((request) => request.status === 'Pending')
    : assignmentRequests, [assignmentRequests, roleArea])
  const availableUnassignedTickets = useMemo(() => unassignedTickets
    .filter((ticket) => !pendingByTicket.has(ticket.ticketReferenceNumber)),
  [pendingByTicket, unassignedTickets])
  useEffect(() => {
    if (!selectedReference || !availableUnassignedTickets.some((ticket) =>
      ticket.ticketReferenceNumber === selectedReference)) return
    unassignedSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [availableUnassignedTickets, selectedReference])
  useEffect(() => {
    if (roleArea !== 'manager' || location.hash !== '#cancellation-requests' || !data) return
    cancellationRequestsSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [data, location.hash, roleArea])
  const filteredAgentWorkloads = useMemo(() => {
    if (roleArea !== 'admin') return agentWorkloads
    const search = workloadFilters.search.trim().toLowerCase()
    const filtered = agentWorkloads.filter((agent) => {
      const matchesSearch = !search ||
        agent.name.toLowerCase().includes(search) ||
        agent.email.toLowerCase().includes(search)
      const matchesStatus = !workloadFilters.capacityState ||
        agent.capacityState === workloadFilters.capacityState
      const matchesWorkload = !workloadFilters.workload ||
        (workloadFilters.workload === 'has-capacity'
          ? !agent.isAtCapacity
          : agent.isAtCapacity)
      return matchesSearch && matchesStatus && matchesWorkload
    })
    return filtered.toSorted((left, right) => {
      switch (workloadFilters.sortBy) {
        case 'name-desc':
          return right.name.localeCompare(left.name)
        case 'workload-asc':
          return left.activeTicketCount - right.activeTicketCount ||
            left.name.localeCompare(right.name)
        case 'workload-desc':
          return right.activeTicketCount - left.activeTicketCount ||
            left.name.localeCompare(right.name)
        case 'remaining-desc':
          return right.remainingCapacity - left.remainingCapacity ||
            left.name.localeCompare(right.name)
        case 'remaining-asc':
          return left.remainingCapacity - right.remainingCapacity ||
            left.name.localeCompare(right.name)
        default:
          return left.name.localeCompare(right.name)
      }
    })
  }, [agentWorkloads, roleArea, workloadFilters])

  return (
    <>
      <section className="page-heading"><h2>Ticket Assignments</h2><p>Assign unassigned requests and review current IT Agent workloads.</p></section>
      <form className="filter-panel ticket-filters" onSubmit={applyFilters}>
        <div className="ticket-filters__grid manager-assignment-filters">
          <label className="filter-search"><span>Search</span><input value={draftFilters.search} onChange={(event) => setDraftFilters({ ...draftFilters, search: event.target.value })} placeholder="Ticket number or title" /></label>
          <label><span>Category</span><select value={draftFilters.categoryId} onChange={(event) => setDraftFilters({ ...draftFilters, categoryId: event.target.value })}><option value="">All</option>{lookups.categories.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
          <label><span>Priority</span><select value={draftFilters.priorityId} onChange={(event) => setDraftFilters({ ...draftFilters, priorityId: event.target.value })}><option value="">All</option>{lookups.priorities.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label>
          <label><span>Date Range</span><select value={draftFilters.dateRange} onChange={(event) => changeDateRange(event.target.value)}>{dateRangeOptions.map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
        </div>
        <div className={`ticket-filters__custom-date-row ${draftFilters.dateRange === 'custom' ? 'ticket-filters__custom-date-row--visible' : ''}`}>
          {draftFilters.dateRange === 'custom' && <>
            <label><span>From Date</span><input type="date" value={draftFilters.fromDate} onChange={(event) => setDraftFilters({ ...draftFilters, fromDate: event.target.value })} /></label>
            <label><span>To Date</span><input type="date" value={draftFilters.toDate} onChange={(event) => setDraftFilters({ ...draftFilters, toDate: event.target.value })} /></label>
          </>}
          <div className="filter-actions"><button className="button button--primary" type="submit">Apply Filters</button><button className="button button--secondary" type="button" onClick={clearFilters}>Clear</button></div>
        </div>
        {dateError && <p className="filter-validation" role="alert">{dateError}</p>}
      </form>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
      {!error && !data && <LoadingState message="Loading ticket assignments…" />}
      {data && <>
      {roleArea === 'manager' && <section id="cancellation-requests" ref={cancellationRequestsSectionRef} className="panel dashboard-section assignment-management-section">
        <div className="panel__heading assignment-section-heading"><div><h2>Cancellation Requests</h2><p>Review requests from IT Agents assigned to active tickets.</p></div><span className="assignment-section-count">{cancellationRequests.filter((item) => item.status === 'Pending').length} pending</span></div>
        {cancellationRequestError ? <div className="inline-alert inline-alert--error" role="alert">{cancellationRequestError}</div> : cancellationRequests.length === 0 ? <AssignmentEmptyState title="No cancellation requests" message="Agent cancellation requests will appear here." /> : <div className="cancellation-requests-table-wrap"><table className="ticket-table cancellation-requests-table"><colgroup><col className="cancellation-col--ticket" /><col className="cancellation-col--agent" /><col className="cancellation-col--ticket-status" /><col className="cancellation-col--requested" /><col className="cancellation-col--request-status" /><col className="cancellation-col--actions" /></colgroup><thead><tr><th>Ticket</th><th>Assigned Agent</th><th>Status</th><th>Requested</th><th>Request Status</th><th>Review</th></tr></thead><tbody>{cancellationRequests.map((request) => <tr key={request.id}><td><span className="cancellation-ticket-identity"><strong>{request.ticketReferenceNumber}</strong><small>{request.ticketTitle}</small></span></td><td className="cancellation-request-agent">{request.requestedByAgentName}</td><td className="cancellation-request-ticket-status">{request.currentTicketStatus}</td><td>{formatLocalDateTime(request.requestedDate)}</td><td><span className={`badge assignment-request-status assignment-request-status--${request.status.toLowerCase()}`}>{request.status}</span></td><td><div className="cancellation-request-actions">{request.status === 'Pending' && <div className="cancellation-action-menu"><button className="button button--primary button--compact cancellation-action-menu__trigger" type="button" aria-haspopup="menu" aria-expanded={openCancellationMenu === request.id} onClick={(event) => toggleCancellationMenu(request.id, event)}>Review <span aria-hidden="true">▼</span></button>{openCancellationMenu === request.id && cancellationMenuPosition && createPortal(<div className="cancellation-action-menu__dropdown" style={cancellationMenuPosition} role="menu"><Link className="cancellation-action-menu__item" role="menuitem" to={`/manager/tickets/${request.ticketReferenceNumber}`} state={{ from: 'cancellation-requests', cancellationRequest: request }} onClick={() => setOpenCancellationMenu(null)}>View Ticket</Link><div className="cancellation-action-menu__divider" role="separator" /><button className="cancellation-action-menu__item cancellation-action-menu__item--danger" type="button" role="menuitem" onClick={() => { setOpenCancellationMenu(null); setCancellationReview({ request, decision: 'cancel' }) }}>Approve &amp; Cancel</button><button className="cancellation-action-menu__item" type="button" role="menuitem" onClick={() => { setOpenCancellationMenu(null); setCancellationReview({ request, decision: 'reassign' }) }}>Approve &amp; Reassign</button><div className="cancellation-action-menu__divider" role="separator" /><button className="cancellation-action-menu__item cancellation-action-menu__item--danger" type="button" role="menuitem" onClick={() => { setOpenCancellationMenu(null); setCancellationReview({ request, decision: 'reject' }) }}>Reject</button></div>, document.body)}</div>}</div></td></tr>)}</tbody></table></div>}
      </section>}
      <section className="panel dashboard-section assignment-management-section">
        <div className="panel__heading assignment-section-heading"><div><h2>{roleArea === 'manager' ? 'My Assignment Requests' : 'Assignment Approvals'}</h2><p>{roleArea === 'manager' ? 'Track requests submitted for Administrator approval.' : 'Approve or reject assignment requests submitted by Managers.'}</p></div><span className="assignment-section-count">{visibleAssignmentRequests.length} {visibleAssignmentRequests.length === 1 ? 'request' : 'requests'}</span></div>
        {assignmentRequestError
          ? <div className="inline-alert inline-alert--error" role="alert"><strong>Assignment requests could not be loaded.</strong><span>{assignmentRequestError}</span><button className="table-action" type="button" onClick={() => setReload((value) => value + 1)}>Try again</button></div>
          : visibleAssignmentRequests.length === 0
          ? <AssignmentEmptyState title="No assignment requests" message={roleArea === 'manager' ? 'Requests you submit for Administrator approval will appear here.' : 'There are currently no requests awaiting Administrator approval.'} />
          : <div className="table-scroll assignment-requests-table-wrap"><table className={`ticket-table assignment-requests-table ${roleArea === 'admin' ? 'assignment-approvals-table' : ''}`}>
            <colgroup><col className="request-col--ticket" /><col className="request-col--agent" />{roleArea === 'admin' && <><col className="request-col--workload" /><col className="request-col--requester" /></>}<col className="request-col--date" />{roleArea === 'manager' && <col className="request-col--status" />}<col className="request-col--action" /></colgroup>
            <thead><tr><th>Ticket</th><th>Requested Agent</th>{roleArea === 'admin' && <><th>Workload</th><th>Requested By</th></>}<th>Requested Date</th>{roleArea === 'manager' && <th>Status</th>}<th>Actions</th></tr></thead>
            <tbody>{visibleAssignmentRequests.map((request) => {
              const approving = reviewingRequest === `${request.id}-approve`
              const rejecting = reviewingRequest === `${request.id}-reject`
              const rowBusy = approving || rejecting
              return <tr key={request.id}>
              <td><span className="assignment-ticket-identity"><strong>{request.ticketTitle}</strong><small>{request.ticketReferenceNumber}</small></span></td>
              <td><span className="assignment-agent-name">{request.requestedAgentName || 'Unavailable'}</span></td>
              {roleArea === 'admin' && <><td className="assignment-workload">{request.requestedAgentActiveTicketCount}/{request.requestedAgentMaxActiveTickets}</td><td>{request.requestedByName}</td></>}
              <td className="assignment-date">{formatLocalDateTime(request.requestedDate)}</td>
              {roleArea === 'manager' && <td><span className={`badge assignment-request-status assignment-request-status--${request.status.toLowerCase()}`}>{request.status === 'Pending' ? 'Pending Approval' : request.status}</span></td>}
              <td><div className="table-actions assignment-request-actions">{roleArea === 'admin' && request.status === 'Pending' && <><button className="button button--primary button--compact assignment-approval-action" type="button" disabled={Boolean(reviewingRequest)} onClick={() => reviewRequest(request, 'approve')}>{approving ? 'Approving…' : 'Approve'}</button><button className="button button--danger-outline button--compact assignment-approval-action" type="button" disabled={Boolean(reviewingRequest)} onClick={() => setRejectingRequest(request)}>{rejecting ? 'Rejecting…' : 'Reject'}</button></>}<Link className={`${roleArea === 'admin' ? 'table-action assignment-approval-view-link' : 'button button--secondary button--compact assignment-approval-action'}${rowBusy ? ' assignment-action--disabled' : ''}`} to={`/${roleArea}/tickets/${request.ticketReferenceNumber}`} aria-disabled={rowBusy || undefined} tabIndex={rowBusy ? -1 : undefined} onClick={(event) => { if (rowBusy) event.preventDefault() }}>View Ticket</Link></div></td>
            </tr>
            })}</tbody>
          </table></div>}
      </section>
      {cancellationReview && <div className="dialog-backdrop" role="presentation"><div className="dialog" role="dialog" aria-modal="true" aria-labelledby="cancellation-review-title"><h2 id="cancellation-review-title">{cancellationReview.decision === 'reject' ? 'Reject Cancellation Request' : cancellationReview.decision === 'cancel' ? 'Approve & Cancel Ticket' : 'Approve & Reassign Ticket'}</h2><p>{cancellationReview.decision === 'cancel' ? `${cancellationReview.request.ticketReferenceNumber} will become Cancelled and the Agent will be released.` : cancellationReview.decision === 'reassign' ? `The Agent will be released and ${cancellationReview.request.ticketReferenceNumber} will return to the unassigned queue. A replacement assignment will still require Administrator approval.` : `The ticket will remain ${cancellationReview.request.currentTicketStatus} and assigned to ${cancellationReview.request.requestedByAgentName}.`}</p><label><span>Review note (optional)</span><textarea value={cancellationReviewNote} onChange={(event) => setCancellationReviewNote(event.target.value)} maxLength="500" disabled={Boolean(reviewingCancellation)} /></label><div className="dialog__actions"><button autoFocus className="button button--secondary" type="button" onClick={() => setCancellationReview(null)} disabled={Boolean(reviewingCancellation)}>Back</button><button className={cancellationReview.decision === 'cancel' ? 'button button--danger' : 'button button--primary'} type="button" onClick={reviewCancellationRequest} disabled={Boolean(reviewingCancellation)}>{reviewingCancellation ? 'Reviewing…' : 'Confirm Decision'}</button></div></div></div>}
      <section className="panel dashboard-section assignment-management-section" ref={unassignedSectionRef}>
        <div className="panel__heading assignment-table-heading"><h2>Unassigned Tickets</h2></div>
        {availableUnassignedTickets.length === 0
          ? <AssignmentEmptyState type="tickets" title="No unassigned tickets" message="All current tickets have an assigned agent." />
          : <div className="table-scroll assignment-table-wrap"><table className="ticket-table admin-assignment-table admin-assignments-page-table">
          <colgroup>
            <col className="assignments-col--number" />
            <col className="assignments-col--title" />
            <col className="assignments-col--requester" />
            <col className="assignments-col--priority" />
            <col className="assignments-col--created" />
            <col className="assignments-col--agent" />
            <col className="assignments-col--action" />
          </colgroup>
          <thead><tr><th>Ticket Number</th><th>Title</th><th>Requester</th><th>Priority</th><th>Created</th><th>Assign To</th><th>Action</th></tr></thead>
          <tbody>{availableUnassignedTickets.map((ticket) => <tr key={ticket.id}>
            <td className="assignments-ticket-number"><strong>{ticket.ticketReferenceNumber}</strong></td>
            <td><span className="assignments-ticket-title" title={ticket.title}>{ticket.title}</span></td>
            <td className="assignments-requester" title={ticket.requesterName}>{ticket.requesterName}</td>
            <td><TicketPriorityBadge value={ticket.priorityName} /></td>
            <td className="assignments-created">{formatLocalDateTime(ticket.createdDate)}</td>
            <td><label className="sr-only" htmlFor={`agent-${ticket.id}`}>Assign {ticket.ticketReferenceNumber} to</label><select className="assignment-agent-select" id={`agent-${ticket.id}`} value={selections[ticket.ticketReferenceNumber] ?? ''} onChange={(event) => setSelections({ ...selections, [ticket.ticketReferenceNumber]: event.target.value })}><option value="">Select agent</option>{agentWorkloads.map((agent) => <option value={agent.userId} key={agent.userId} disabled={agent.isAtCapacity}>{agent.name}</option>)}</select></td>
            <td><button className="button button--primary button--compact assignment-submit" type="button" disabled={!selectedAgent(ticket.ticketReferenceNumber) || selectedAgent(ticket.ticketReferenceNumber)?.isAtCapacity || Boolean(assigning)} onClick={() => assignTicket(ticket)}>{assigning === ticket.ticketReferenceNumber ? roleArea === 'manager' ? 'Submitting…' : 'Assigning…' : 'Assign'}</button></td>
          </tr>)}</tbody>
        </table></div>}
      </section>
      </>}
      {rejectingRequest && <><div className="dialog-backdrop" onClick={() => !reviewingRequest && setRejectingRequest(null)} aria-hidden="true" /><section className="dialog" role="dialog" aria-modal="true" aria-labelledby="reject-assignment-title"><h2 id="reject-assignment-title">Reject assignment request?</h2><p>Provide a short reason for rejecting assignment of {rejectingRequest.ticketReferenceNumber} to {rejectingRequest.requestedAgentName}.</p><label className="field"><span>Rejection reason</span><textarea maxLength="500" rows="3" value={rejectionReason} onChange={(event) => setRejectionReason(event.target.value)} autoFocus /></label><div className="dialog__actions"><button className="button button--secondary" type="button" disabled={Boolean(reviewingRequest)} onClick={() => setRejectingRequest(null)}>Cancel</button><button className="button button--danger" type="button" disabled={!rejectionReason.trim() || Boolean(reviewingRequest)} onClick={() => reviewRequest(rejectingRequest, 'reject', rejectionReason)}>{reviewingRequest ? 'Rejecting…' : 'Reject Request'}</button></div></section></>}
      <section className="panel">
        <div className="panel__heading"><div><h2>IT Agent Workload</h2><p>Review current capacity before assigning support requests.</p></div></div>
        {roleArea === 'admin' && <div className="workload-filter-toolbar">
          <label><span>Search</span><input value={workloadFilters.search} onChange={(event) => setWorkloadFilters({ ...workloadFilters, search: event.target.value })} placeholder="Search agent by name" /></label>
          <label><span>Capacity status</span><select value={workloadFilters.capacityState} onChange={(event) => setWorkloadFilters({ ...workloadFilters, capacityState: event.target.value })}><option value="">All statuses</option><option>Available</option><option>Near Capacity</option><option>Full</option><option>Over Capacity</option></select></label>
          <label><span>Workload</span><select value={workloadFilters.workload} onChange={(event) => setWorkloadFilters({ ...workloadFilters, workload: event.target.value })}><option value="">All workloads</option><option value="has-capacity">Has capacity</option><option value="no-capacity">No capacity</option></select></label>
          <label><span>Sort by</span><select value={workloadFilters.sortBy} onChange={(event) => setWorkloadFilters({ ...workloadFilters, sortBy: event.target.value })}><option value="name-asc">Name A–Z</option><option value="name-desc">Name Z–A</option><option value="workload-asc">Lowest workload</option><option value="workload-desc">Highest workload</option><option value="remaining-desc">Most remaining capacity</option><option value="remaining-asc">Least remaining capacity</option></select></label>
          <button className="button button--secondary button--compact" type="button" onClick={() => setWorkloadFilters(initialWorkloadFilters)}>Reset Filters</button>
        </div>}
        {agentWorkloads.length > 0 && roleArea === 'admin' && <p className="workload-result-count">Showing {filteredAgentWorkloads.length} of {agentWorkloads.length} agents</p>}
        {agentWorkloads.length === 0
          ? <EmptyState title="No active agents" message="Active IT Support Agents will appear here." />
          : filteredAgentWorkloads.length === 0
            ? <EmptyState title="No matching agents" message="No IT Agents match the selected filters." action={<button className="button button--secondary button--compact" type="button" onClick={() => setWorkloadFilters(initialWorkloadFilters)}>Reset Filters</button>} />
            : <div className="workload-grid workload-grid--filterable">{filteredAgentWorkloads.map((agent) => <AgentWorkloadCard agent={agent} key={agent.userId} />)}</div>}
      </section>
    </>
  )
}

export default AdminAssignmentsPage
