import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import { TicketPriorityBadge } from '../components/tickets/TicketBadges.jsx'
import { AgentWorkloadCard } from '../components/tickets/AgentWorkload.jsx'
import { assignAdminTicket, getAdminAssignments } from '../services/adminService.js'
import {
  assignManagerTicket,
  getManagerAssignmentRequests,
  getManagerAssignments,
  reviewManagerAssignmentRequest,
} from '../services/managerService.js'
import { getCategories, getPriorities } from '../services/ticketService.js'
import { formatLocalDate } from '../utils/dateTime.js'
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
const emptyAgentWorkloads = []

function AdminAssignmentsPage({ roleArea = 'admin' }) {
  const [searchParams] = useSearchParams()
  const selectedReference = searchParams.get('ticket')
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
  const [reviewingRequest, setReviewingRequest] = useState('')
  const dismissToast = useCallback(() => setToast(null), [])

  useEffect(() => {
    const controller = new AbortController()
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
    if (roleArea !== 'manager') return undefined
    const controller = new AbortController()
    getManagerAssignmentRequests(controller.signal)
      .then((items) => {
        if (!controller.signal.aborted) setAssignmentRequests(items)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setError(requestError.message)
      })
    return () => controller.abort()
  }, [reload, roleArea])

  async function reviewRequest(request, decision) {
    if (reviewingRequest) return
    try {
      setReviewingRequest(`${request.id}-${decision}`)
      await reviewManagerAssignmentRequest(request.id, decision)
      setReload((value) => value + 1)
      setToast({
        id: Date.now(),
        type: 'success',
        title: decision === 'approve' ? 'Request Approved' : 'Request Rejected',
        message: `${request.ticketReferenceNumber} request from ${request.requestedByName} was ${decision === 'approve' ? 'approved' : 'rejected'}.`,
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
      await assign(ticket.ticketReferenceNumber, agentUserId)
      setReload((value) => value + 1)
      setToast({
        id: Date.now(),
        type: 'success',
        title: 'Ticket Assigned',
        message: `${ticket.ticketReferenceNumber} assigned to ${agent.name}.`,
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

  const unassignedTickets = data?.unassignedTickets ?? []
  const agentWorkloads = data?.agentWorkloads ?? emptyAgentWorkloads
  const selectedAgent = (ticketReferenceNumber) => {
    const userId = Number(selections[ticketReferenceNumber])
    return agentWorkloads.find((agent) => agent.userId === userId)
  }
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
      {roleArea === 'manager' && <section className="panel dashboard-section">
        <div className="panel__heading"><div><h2>Assignment Requests</h2><p>Approve or reject requests submitted by IT Support Agents.</p></div></div>
        {assignmentRequests.length === 0
          ? <EmptyState title="No assignment requests pending" message="There are currently no assignment requests awaiting manager review." />
          : <div className="table-scroll"><table className="ticket-table"><thead><tr><th>Ticket</th><th>Title</th><th>Requested By</th><th>Requested</th><th>Action</th></tr></thead><tbody>{assignmentRequests.map((request) => <tr key={request.id}><td><strong>{request.ticketReferenceNumber}</strong></td><td>{request.ticketTitle}</td><td>{request.requestedByName}</td><td>{formatLocalDate(request.requestedDate)}</td><td><div className="table-actions"><button className="table-action" type="button" disabled={Boolean(reviewingRequest)} onClick={() => reviewRequest(request, 'approve')}>Approve</button><button className="table-action table-action--danger" type="button" disabled={Boolean(reviewingRequest)} onClick={() => reviewRequest(request, 'reject')}>Reject</button></div></td></tr>)}</tbody></table></div>}
      </section>}
      <section className="panel dashboard-section">
        <div className="panel__heading"><div><h2>Unassigned Tickets</h2></div></div>
        {unassignedTickets.length === 0 ? <EmptyState title="No unassigned tickets" message="All current tickets have an assigned agent." /> : <div className="table-scroll"><table className="ticket-table admin-assignment-table admin-assignments-page-table">
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
          <tbody>{unassignedTickets.map((ticket) => <tr className={selectedReference === ticket.ticketReferenceNumber ? 'table-row--highlighted' : ''} key={ticket.id}>
            <td className="assignments-ticket-number"><strong>{ticket.ticketReferenceNumber}</strong></td>
            <td><span className="assignments-ticket-title" title={ticket.title}>{ticket.title}</span></td>
            <td className="assignments-requester" title={ticket.requesterName}>{ticket.requesterName}</td>
            <td><TicketPriorityBadge value={ticket.priorityName} /></td>
            <td className="assignments-created">{formatLocalDate(ticket.createdDate)}</td>
            <td><label className="sr-only" htmlFor={`agent-${ticket.id}`}>Assign {ticket.ticketReferenceNumber} to</label><select className="assignment-agent-select" id={`agent-${ticket.id}`} value={selections[ticket.ticketReferenceNumber] ?? ''} onChange={(event) => setSelections({ ...selections, [ticket.ticketReferenceNumber]: event.target.value })}><option value="">Select agent</option>{agentWorkloads.map((agent) => <option value={agent.userId} key={agent.userId} disabled={agent.isAtCapacity}>{agent.name}</option>)}</select></td>
            <td><button className="button button--primary button--compact assignment-submit" type="button" disabled={!selectedAgent(ticket.ticketReferenceNumber) || selectedAgent(ticket.ticketReferenceNumber)?.isAtCapacity || Boolean(assigning)} onClick={() => assignTicket(ticket)}>{assigning === ticket.ticketReferenceNumber ? 'Assigning…' : 'Assign'}</button></td>
          </tr>)}</tbody>
        </table></div>}
      </section>
      </>}
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
