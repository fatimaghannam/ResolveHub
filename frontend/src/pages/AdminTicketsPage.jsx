import { useEffect, useState } from 'react'
import { FilePlus2 } from 'lucide-react'
import { Link, useLocation } from 'react-router-dom'
import Pagination from '../components/common/Pagination.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { getAdminTickets } from '../services/adminService.js'
import { getManagerTickets } from '../services/managerService.js'
import { getCategories, getPriorities, getStatuses } from '../services/ticketService.js'
import { getLocalQuickDateRange } from '../utils/dateRange.js'
import { formatLocalDate } from '../utils/dateTime.js'

const pageSize = 8
const blankFilters = { search: '', status: '', category: '', priority: '', assignment: '', dateRange: 'all', fromDate: '', toDate: '' }
const dateOptions = [['all', 'All Dates'], ['yesterday', 'Yesterday'], ['last7Days', 'Last 7 Days'], ['last30Days', 'Last 30 Days'], ['custom', 'Custom Range']]

function AdminTicketsPage({ roleArea = 'admin' }) {
  const location = useLocation()
  const [draft, setDraft] = useState(blankFilters)
  const [filters, setFilters] = useState(blankFilters)
  const [page, setPage] = useState(1)
  const [dateError, setDateError] = useState('')
  const [data, setData] = useState(null)
  const [lookups, setLookups] = useState({ statuses: [], categories: [], priorities: [] })
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    Promise.all([getStatuses(controller.signal), getCategories(controller.signal), getPriorities(controller.signal)])
      .then(([statuses, categories, priorities]) => setLookups({ statuses, categories, priorities }))
      .catch((requestError) => { if (requestError.name !== 'AbortError') setError(requestError.message) })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    const loadTickets = roleArea === 'manager' ? getManagerTickets : getAdminTickets
    loadTickets({
      search: filters.search, statusId: filters.status, categoryId: filters.category,
      priorityId: filters.priority,
      unassignedOnly: filters.assignment === 'unassigned' ? true : undefined,
      assignedOnly: filters.assignment === 'assigned' ? true : undefined,
      fromDate: filters.fromDate, toDate: filters.toDate, page, pageSize,
    }, controller.signal).then(setData)
      .catch((requestError) => { if (requestError.name !== 'AbortError') setError(requestError.message) })
    return () => controller.abort()
  }, [filters, page, roleArea])
  const rows = data?.items ?? []

  function applyFilters(event) {
    event.preventDefault()
    if (draft.dateRange === 'custom' && (!draft.fromDate || !draft.toDate)) {
      setDateError('Select both a start date and an end date.')
      return
    }
    if (draft.fromDate && draft.toDate && draft.toDate < draft.fromDate) {
      setDateError('The end date cannot be earlier than the start date.')
      return
    }
    const dates = draft.dateRange === 'custom' ? { fromDate: draft.fromDate, toDate: draft.toDate } : getLocalQuickDateRange(draft.dateRange)
    setFilters({ ...draft, ...dates }); setDraft({ ...draft, ...dates }); setPage(1); setDateError('')
  }

  function changeDateRange(value) {
    const dates = value === 'custom' ? { fromDate: '', toDate: '' } : getLocalQuickDateRange(value)
    setDraft((current) => ({ ...current, dateRange: value, ...dates }))
    setDateError('')
  }

  return (
    <>
      <section className="page-heading page-heading--action">
        <div><h2>All Tickets</h2><p>Review, filter, and manage tickets across the organization.</p></div>
        <Link className="button button--primary" to={`/${roleArea}/tickets/create`}><FilePlus2 size={17} />Create Ticket</Link>
      </section>
      {location.state?.notice && <div className="inline-alert inline-alert--success" role="status">{location.state.notice}</div>}
      <form className="filter-panel ticket-filters" onSubmit={applyFilters}>
        <div className="ticket-filters__grid admin-ticket-filters">
          <label className="filter-search"><span>Search</span><input value={draft.search} onChange={(event) => setDraft({ ...draft, search: event.target.value })} placeholder="Ticket number, title, or requester" /></label>
          {[['status', 'Status', lookups.statuses], ['category', 'Category', lookups.categories], ['priority', 'Priority', lookups.priorities]].map(([key, label, options]) => (
            <label key={key}><span>{label}</span><select value={draft[key]} onChange={(event) => setDraft({ ...draft, [key]: event.target.value })}><option value="">All</option>{options.map((option) => <option value={option.id} key={option.id}>{option.name}</option>)}</select></label>
          ))}
          <label><span>Assignment</span><select value={draft.assignment} onChange={(event) => setDraft({ ...draft, assignment: event.target.value })}><option value="">All</option><option value="assigned">Assigned</option><option value="unassigned">Unassigned</option></select></label>
          <label><span>Date Range</span><select value={draft.dateRange} onChange={(event) => changeDateRange(event.target.value)}>{dateOptions.map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
        </div>
        <div className={`ticket-filters__custom-date-row ${draft.dateRange === 'custom' ? 'ticket-filters__custom-date-row--visible' : ''}`}>
          {draft.dateRange === 'custom' && <>
            <label><span>From Date</span><input type="date" value={draft.fromDate} onChange={(event) => setDraft({ ...draft, fromDate: event.target.value })} /></label>
            <label><span>To Date</span><input type="date" value={draft.toDate} onChange={(event) => setDraft({ ...draft, toDate: event.target.value })} /></label>
          </>}
          <div className="filter-actions"><button className="button button--primary" type="submit">Apply Filters</button><button className="button button--secondary" type="button" onClick={() => { setDraft(blankFilters); setFilters(blankFilters); setPage(1); setDateError('') }}>Clear</button></div>
        </div>
        {dateError && <p className="filter-validation" role="alert">{dateError}</p>}
      </form>
      {error && <ErrorState message={error} />}
      {!error && !data && <LoadingState message="Loading tickets…" />}
      {data &&
      <section className="panel">
        <div className="results-count">{data.totalItems} ticket{data.totalItems === 1 ? '' : 's'}</div>
        {rows.length === 0 ? <EmptyState title="No tickets found" message="Try changing or clearing the current filters." /> : <div className="table-scroll admin-ticket-table-wrap"><table className="ticket-table admin-ticket-table">
          <colgroup>
            <col className="admin-ticket-col--number" />
            <col className="admin-ticket-col--title" />
            <col className="admin-ticket-col--requester" />
            <col className="admin-ticket-col--category" />
            <col className="admin-ticket-col--priority" />
            <col className="admin-ticket-col--status" />
            <col className="admin-ticket-col--agent" />
            <col className="admin-ticket-col--created" />
            <col className="admin-ticket-col--action" />
          </colgroup>
          <thead><tr><th>Ticket Number</th><th>Title</th><th>Requester</th><th>Category</th><th>Priority</th><th>Status</th><th>Assigned Agent</th><th>Created</th><th>Action</th></tr></thead>
          <tbody>{rows.map((ticket) => <tr key={ticket.id}><td><strong>{ticket.ticketReferenceNumber}</strong></td><td><span className="admin-ticket-title" title={ticket.title}>{ticket.title}</span></td><td><span className="admin-ticket-cell-ellipsis" title={ticket.requesterName}>{ticket.requesterName}</span></td><td><span className="admin-ticket-cell-ellipsis" title={ticket.categoryName}>{ticket.categoryName}</span></td><td><TicketPriorityBadge value={ticket.priorityName} /></td><td><TicketStatusBadge value={ticket.statusName} /></td><td><span className="admin-ticket-cell-ellipsis" title={ticket.assignedAgentName ?? 'Unassigned'}>{ticket.assignedAgentName ?? 'Unassigned'}</span></td><td><span className="admin-ticket-cell-ellipsis" title={formatLocalDate(ticket.createdDate)}>{formatLocalDate(ticket.createdDate)}</span></td><td><div className="row-actions"><Link className="table-action" to={`/${roleArea}/tickets/${ticket.ticketReferenceNumber}`}>View</Link>{roleArea === 'manager' && !ticket.assignedAgentId && <Link className="table-action" to={`/manager/assignments?ticket=${ticket.ticketReferenceNumber}`}>Assign</Link>}</div></td></tr>)}</tbody>
        </table></div>}
        <Pagination page={page} totalPages={data.totalPages} onChange={setPage} />
      </section>}
    </>
  )
}

export default AdminTicketsPage
