import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import Pagination from '../components/common/Pagination.jsx'
import { EmptyState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { agentFilterOptions, agentTickets } from '../data/agentDashboardMockData.js'
import { formatLocalDate } from '../utils/dateTime.js'
import { getLocalQuickDateRange } from '../utils/dateRange.js'
import { formatTicketReference } from '../utils/ticketReference.js'

const pageSize = 8
const dateRangeOptions = [
  ['all', 'All Dates'],
  ['yesterday', 'Yesterday'],
  ['last7Days', 'Last 7 Days'],
  ['last30Days', 'Last 30 Days'],
  ['custom', 'Custom Range'],
]
const emptyFilters = {
  search: '',
  status: '',
  category: '',
  priority: '',
  dateRange: 'all',
  fromDate: '',
  toDate: '',
}
const selectFilters = [
  ['status', 'Status', agentFilterOptions.statuses],
  ['category', 'Category', agentFilterOptions.categories],
  ['priority', 'Priority', agentFilterOptions.priorities],
]

function AgentTicketsPage() {
  const [draft, setDraft] = useState(emptyFilters)
  const [filters, setFilters] = useState(emptyFilters)
  const [dateError, setDateError] = useState('')
  const [page, setPage] = useState(1)

  const filteredTickets = useMemo(() => {
    const search = filters.search.trim().toLowerCase()
    return agentTickets.filter((ticket) => {
      const createdDate = ticket.createdDate.slice(0, 10)
      return (
        (!search ||
          formatTicketReference(ticket).toLowerCase().includes(search) ||
          ticket.title.toLowerCase().includes(search)) &&
        (!filters.status || ticket.status === filters.status) &&
        (!filters.category || ticket.category === filters.category) &&
        (!filters.priority || ticket.priority === filters.priority) &&
        (!filters.fromDate || createdDate >= filters.fromDate) &&
        (!filters.toDate || createdDate <= filters.toDate)
      )
    })
  }, [filters])

  const totalPages = Math.max(1, Math.ceil(filteredTickets.length / pageSize))
  const visibleTickets = filteredTickets.slice((page - 1) * pageSize, page * pageSize)

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

    const dates = draft.dateRange === 'custom'
      ? { fromDate: draft.fromDate, toDate: draft.toDate }
      : getLocalQuickDateRange(draft.dateRange)
    const next = { ...draft, ...dates }
    setDateError('')
    setDraft(next)
    setFilters(next)
    setPage(1)
  }

  function clearFilters() {
    setDraft(emptyFilters)
    setFilters(emptyFilters)
    setDateError('')
    setPage(1)
  }

  function changeDateRange(value) {
    setDateError('')
    if (value === 'custom') {
      setDraft((current) => ({
        ...current,
        dateRange: value,
        fromDate: '',
        toDate: '',
      }))
      return
    }

    setDraft((current) => ({
      ...current,
      dateRange: value,
      ...getLocalQuickDateRange(value),
    }))
  }

  function changeCustomDate(field, value) {
    const next = { ...draft, dateRange: 'custom', [field]: value }
    setDraft(next)
    if (next.fromDate && next.toDate && next.toDate >= next.fromDate) {
      setDateError('')
    }
  }

  return (
    <>
      <section className="page-heading">
        <h2>Assigned Tickets</h2>
        <p>Review and manage the support requests assigned to you.</p>
      </section>

      <form className="filter-panel ticket-filters" onSubmit={applyFilters}>
        <div className="ticket-filters__grid">
          <label className="filter-search"><span>Search</span><input value={draft.search} onChange={(event) => setDraft({ ...draft, search: event.target.value })} placeholder="Ticket number or title" /></label>
          {selectFilters.map(([key, label, options]) => (
            <label key={key}>
              <span>{label}</span>
              <select value={draft[key]} onChange={(event) => setDraft({ ...draft, [key]: event.target.value })}>
                <option value="">All</option>
                {options.map((option) => <option value={option} key={option}>{option}</option>)}
              </select>
            </label>
          ))}
          <label>
            <span>Date Range</span>
            <select value={draft.dateRange} onChange={(event) => changeDateRange(event.target.value)}>
              {dateRangeOptions.map(([value, label]) => <option value={value} key={value}>{label}</option>)}
            </select>
          </label>
        </div>
        <div className={`ticket-filters__custom-date-row ${draft.dateRange === 'custom' ? 'ticket-filters__custom-date-row--visible' : ''}`}>
          {draft.dateRange === 'custom' && (
            <>
              <label><span>From Date</span><input type="date" value={draft.fromDate} onChange={(event) => changeCustomDate('fromDate', event.target.value)} aria-invalid={Boolean(dateError)} aria-describedby={dateError ? 'agent-date-error' : undefined} /></label>
              <label><span>To Date</span><input type="date" value={draft.toDate} onChange={(event) => changeCustomDate('toDate', event.target.value)} aria-invalid={Boolean(dateError)} aria-describedby={dateError ? 'agent-date-error' : undefined} /></label>
            </>
          )}
          <div className="filter-actions">
            <button className="button button--primary" type="submit">Apply Filters</button>
            <button className="button button--secondary" type="button" onClick={clearFilters}>Clear</button>
          </div>
        </div>
        {dateError && <p className="filter-validation" id="agent-date-error" role="alert">{dateError}</p>}
      </form>

      <section className="panel">
        <div className="results-count">{filteredTickets.length} assigned ticket{filteredTickets.length === 1 ? '' : 's'}</div>
        {visibleTickets.length === 0 ? (
          <EmptyState title="No assigned tickets found" message="Try changing or clearing the current filters." />
        ) : (
          <div className="table-scroll">
            <table className="ticket-table agent-ticket-table">
              <thead><tr><th>Ticket Number</th><th>Title</th><th>Requester</th><th>Category</th><th>Priority</th><th>Status</th><th>Created</th><th>Action</th></tr></thead>
              <tbody>
                {visibleTickets.map((ticket) => (
                  <tr key={ticket.id}>
                    <td><strong>{formatTicketReference(ticket)}</strong></td><td>{ticket.title}</td>
                    <td>{ticket.requester}</td><td>{ticket.category}</td>
                    <td><TicketPriorityBadge value={ticket.priority} /></td>
                    <td><TicketStatusBadge value={ticket.status} /></td>
                    <td>{formatLocalDate(ticket.createdDate)}</td>
                    <td><Link className="table-action" to={`/agent/tickets/${formatTicketReference(ticket)}`}>View</Link></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <Pagination page={page} totalPages={totalPages} onChange={setPage} />
      </section>
    </>
  )
}

export default AgentTicketsPage
