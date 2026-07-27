import { useMemo, useState } from 'react'
import { FilePlus2 } from 'lucide-react'
import { Link, useLocation } from 'react-router-dom'
import Pagination from '../components/common/Pagination.jsx'
import { EmptyState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { ticketCategories, ticketMockData } from '../data/index.js'
import { getLocalQuickDateRange, parseLocalDateInput } from '../utils/dateRange.js'
import { formatLocalDate } from '../utils/dateTime.js'

const pageSize = 8
const blankFilters = { search: '', status: '', category: '', priority: '', assignment: '', dateRange: 'all', fromDate: '', toDate: '' }
const dateOptions = [['all', 'All Dates'], ['yesterday', 'Yesterday'], ['last7Days', 'Last 7 Days'], ['last30Days', 'Last 30 Days'], ['custom', 'Custom Range']]
const uniqueValues = (field) => [...new Set(ticketMockData.map((ticket) => ticket[field]))]

function AdminTicketsPage() {
  const location = useLocation()
  const [draft, setDraft] = useState(blankFilters)
  const [filters, setFilters] = useState(blankFilters)
  const [page, setPage] = useState(1)
  const [dateError, setDateError] = useState('')

  const filtered = useMemo(() => ticketMockData.filter((ticket) => {
    const search = filters.search.trim().toLowerCase()
    if (search && ![ticket.ticketReferenceNumber, ticket.title, ticket.requesterName].some((value) => value.toLowerCase().includes(search))) return false
    if (filters.status && ticket.statusName !== filters.status) return false
    if (filters.category && ticket.categoryName !== filters.category) return false
    if (filters.priority && ticket.priorityName !== filters.priority) return false
    if (filters.assignment === 'assigned' && !ticket.assignedAgentName) return false
    if (filters.assignment === 'unassigned' && ticket.assignedAgentName) return false
    if (filters.fromDate || filters.toDate) {
      const created = new Date(ticket.createdDate)
      const start = parseLocalDateInput(filters.fromDate)
      const end = parseLocalDateInput(filters.toDate)
      if (start && created < start) return false
      if (end) {
        end.setDate(end.getDate() + 1)
        if (created >= end) return false
      }
    }
    return true
  }), [filters])
  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize))
  const rows = filtered.slice((page - 1) * pageSize, page * pageSize)

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
        <Link className="button button--primary" to="/admin/tickets/create"><FilePlus2 size={17} />Create Ticket</Link>
      </section>
      {location.state?.notice && <div className="inline-alert inline-alert--success" role="status">{location.state.notice}</div>}
      <form className="filter-panel ticket-filters" onSubmit={applyFilters}>
        <div className="ticket-filters__grid admin-ticket-filters">
          <label className="filter-search"><span>Search</span><input value={draft.search} onChange={(event) => setDraft({ ...draft, search: event.target.value })} placeholder="Ticket number, title, or requester" /></label>
          {[['status', 'Status', uniqueValues('statusName')], ['category', 'Category', ticketCategories], ['priority', 'Priority', uniqueValues('priorityName')]].map(([key, label, options]) => (
            <label key={key}><span>{label}</span><select value={draft[key]} onChange={(event) => setDraft({ ...draft, [key]: event.target.value })}><option value="">All</option>{options.map((option) => <option key={option}>{option}</option>)}</select></label>
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
      <section className="panel">
        <div className="results-count">{filtered.length} ticket{filtered.length === 1 ? '' : 's'}</div>
        {rows.length === 0 ? <EmptyState title="No tickets found" message="Try changing or clearing the current filters." /> : <div className="table-scroll"><table className="ticket-table admin-ticket-table">
          <thead><tr><th>Ticket Number</th><th>Title</th><th>Requester</th><th>Category</th><th>Priority</th><th>Status</th><th>Assigned Agent</th><th>Created</th><th>Action</th></tr></thead>
          <tbody>{rows.map((ticket) => <tr key={ticket.id}><td><strong>{ticket.ticketReferenceNumber}</strong></td><td>{ticket.title}</td><td>{ticket.requesterName}</td><td>{ticket.categoryName}</td><td><TicketPriorityBadge value={ticket.priorityName} /></td><td><TicketStatusBadge value={ticket.statusName} /></td><td>{ticket.assignedAgentName ?? 'Unassigned'}</td><td>{formatLocalDate(ticket.createdDate)}</td><td><Link className="table-action" to={`/admin/tickets/${ticket.ticketReferenceNumber}`}>View</Link></td></tr>)}</tbody>
        </table></div>}
        <Pagination page={page} totalPages={totalPages} onChange={setPage} />
      </section>
    </>
  )
}

export default AdminTicketsPage
