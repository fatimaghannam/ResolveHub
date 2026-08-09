import { useCallback, useEffect, useState } from 'react'
import { FilePlus2 } from 'lucide-react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import Pagination from '../../components/common/Pagination.jsx'
import { EmptyState, ErrorState, LoadingState } from '../../components/common/States.jsx'
import Toast from '../../components/common/Toast.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../../components/tickets/TicketBadges.jsx'
import { getAdminTickets } from '../../services/adminService.js'
import { getManagerTickets } from '../../services/managerService.js'
import { getCategories, getPriorities, getStatuses } from '../../services/ticketService.js'
import {
  getLocalQuickDateRange,
  getUtcDateRange,
  STANDARD_DATE_RANGE_OPTIONS,
} from '../../utils/dateRange.js'
import { formatLocalDateTime } from '../../utils/dateTime.js'

const pageSize = 8
const blankFilters = {
  search: '',
  status: '',
  category: '',
  priority: '',
  dateRange: 'all',
  fromDate: '',
  toDate: '',
}
const dateOptions = STANDARD_DATE_RANGE_OPTIONS
const supportedDateRanges = new Set(dateOptions.map(([value]) => value))
function initialFilters(searchParams) {
  const query = Object.fromEntries(searchParams)
  const dateRange = supportedDateRanges.has(query.dateRange)
    ? query.dateRange
    : query.fromDate || query.toDate ? 'custom' : 'all'
  return {
    ...blankFilters,
    search: query.search ?? '',
    status: query.status ?? '',
    category: query.category ?? '',
    priority: query.priority ?? '',
    dateRange,
    fromDate: query.fromDate ?? '',
    toDate: query.toDate ?? '',
  }
}

function urlValues(filters, page) {
  const values = { ...filters, page: page === 1 ? '' : page }
  return Object.fromEntries(
    Object.entries(values).filter(([, value]) =>
      value !== '' && value !== 'all'),
  )
}

function AdminTicketsPage({ roleArea = 'admin' }) {
  const location = useLocation()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const initial = initialFilters(searchParams)
  const [draft, setDraft] = useState(initial)
  const [filters, setFilters] = useState(initial)
  const [page, setPage] = useState(Math.max(1, Number(searchParams.get('page')) || 1))
  const [dateError, setDateError] = useState('')
  const [data, setData] = useState(null)
  const [lookups, setLookups] = useState({ statuses: [], categories: [], priorities: [] })
  const [error, setError] = useState('')
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
    const next = initialFilters(searchParams)
    const nextPage = Math.max(1, Number(searchParams.get('page')) || 1)
    setDraft((current) =>
      JSON.stringify(current) === JSON.stringify(next) ? current : next)
    setFilters((current) =>
      JSON.stringify(current) === JSON.stringify(next) ? current : next)
    setPage(nextPage)
  }, [searchParams])

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
    const { fromUtc, toUtcExclusive } =
      getUtcDateRange(filters.fromDate, filters.toDate)
    loadTickets({
      search: filters.search, statusId: filters.status, categoryId: filters.category,
      priorityId: filters.priority,
      fromUtc, toUtcExclusive,
      page, pageSize,
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
    const next = { ...draft, ...dates }
    setFilters(next); setDraft(next); setPage(1); setSearchParams(urlValues(next, 1)); setDateError('')
  }

  function changeDateRange(value) {
    const dates = value === 'custom' ? { fromDate: '', toDate: '' } : getLocalQuickDateRange(value)
    setDraft((current) => ({ ...current, dateRange: value, ...dates }))
    setDateError('')
  }

  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <section className="page-heading page-heading--action">
        <div><h2>All Tickets</h2><p>Review, filter, and manage tickets across the organization.</p></div>
        {roleArea === 'admin' && <Link className="button button--primary" to="/admin/tickets/create"><FilePlus2 size={17} />Create Ticket</Link>}
      </section>
      <form className="filter-panel ticket-filters" onSubmit={applyFilters}>
        <div className="ticket-filters__grid admin-ticket-filters">
          <label className="filter-search"><span>Search</span><input value={draft.search} onChange={(event) => setDraft({ ...draft, search: event.target.value })} placeholder={roleArea === 'manager' ? 'Ticket number or title' : 'Ticket number, title, or requester'} /></label>
          {[['status', 'Status', lookups.statuses], ['category', 'Category', lookups.categories], ['priority', 'Priority', lookups.priorities]].map(([key, label, options]) => (
            <label key={key}><span>{label}</span><select value={draft[key]} onChange={(event) => setDraft({ ...draft, [key]: event.target.value })}><option value="">All</option>{options.map((option) => <option value={option.id} key={option.id}>{option.name}</option>)}</select></label>
          ))}
          <label><span>Date Range</span><select value={draft.dateRange} onChange={(event) => changeDateRange(event.target.value)}>{dateOptions.map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
        </div>
        <div className={`ticket-filters__custom-date-row ${draft.dateRange === 'custom' ? 'ticket-filters__custom-date-row--visible' : ''}`}>
          {draft.dateRange === 'custom' && <>
            <label><span>From Date</span><input type="date" value={draft.fromDate} onChange={(event) => setDraft({ ...draft, fromDate: event.target.value })} /></label>
            <label><span>To Date</span><input type="date" value={draft.toDate} onChange={(event) => setDraft({ ...draft, toDate: event.target.value })} /></label>
          </>}
          <div className="filter-actions"><button className="button button--primary" type="submit">Apply Filters</button><button className="button button--secondary" type="button" onClick={() => { setDraft(blankFilters); setFilters(blankFilters); setPage(1); setSearchParams({}); setDateError('') }}>Clear</button></div>
        </div>
        {dateError && <p className="filter-validation" role="alert">{dateError}</p>}
      </form>
      {error && <ErrorState message={error} />}
      {!error && !data && <LoadingState message="Loading tickets…" />}
      {data &&
      <section className="panel">
        <div className="results-count">{data.totalItems} ticket{data.totalItems === 1 ? '' : 's'}</div>
        {rows.length === 0 ? <EmptyState title="No tickets found." message="Try changing or clearing the current filters." /> : <div className="table-scroll admin-ticket-table-wrap"><table className="ticket-table admin-ticket-table">
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
          <tbody>{rows.map((ticket) => <tr key={ticket.id}><td><strong>{ticket.ticketReferenceNumber}</strong></td><td><span className="admin-ticket-title" title={ticket.title}>{ticket.title}</span></td><td><span className="admin-ticket-cell-ellipsis" title={ticket.requesterName}>{ticket.requesterName}</span></td><td><span className="admin-ticket-cell-ellipsis" title={ticket.categoryName}>{ticket.categoryName}</span></td><td><TicketPriorityBadge value={ticket.priorityName} /></td><td><TicketStatusBadge value={ticket.statusName} /></td><td><span className="admin-ticket-cell-ellipsis" title={ticket.assignedAgentName ?? 'Unassigned'}>{ticket.assignedAgentName ?? 'Unassigned'}</span></td><td><span className="admin-ticket-cell-ellipsis" title={formatLocalDateTime(ticket.createdDate)}>{formatLocalDateTime(ticket.createdDate)}</span></td><td><div className="row-actions"><Link className="table-action" to={`/${roleArea}/tickets/${ticket.ticketReferenceNumber}`}>View</Link>{!ticket.assignedAgentId && ticket.statusName !== 'Duplicate' && <Link className="table-action" to={`/${roleArea}/assignments?ticket=${encodeURIComponent(ticket.ticketReferenceNumber)}`}>Assign</Link>}</div></td></tr>)}</tbody>
        </table></div>}
        <Pagination page={page} totalPages={data.totalPages} onChange={(nextPage) => { setPage(nextPage); setSearchParams(urlValues(filters, nextPage)) }} />
      </section>}
    </>
  )
}

export default AdminTicketsPage
