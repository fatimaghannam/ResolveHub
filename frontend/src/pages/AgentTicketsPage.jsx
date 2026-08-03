import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import Pagination from '../components/common/Pagination.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { getAssignedTickets, getOpenTickets } from '../services/agentTicketService.js'
import { getCategories, getPriorities, getStatuses } from '../services/ticketService.js'
import { formatLocalDate } from '../utils/dateTime.js'
import {
  getLocalQuickDateRange,
  getUtcDateRange,
  STANDARD_DATE_RANGE_OPTIONS,
} from '../utils/dateRange.js'
import { formatTicketReference } from '../utils/ticketReference.js'

const pageSize = 8
const dateRangeOptions = STANDARD_DATE_RANGE_OPTIONS
const supportedDateRanges = new Set(dateRangeOptions.map(([value]) => value))
const emptyDraft = {
  search: '',
  statusId: '',
  categoryId: '',
  priorityId: '',
  dateRange: 'all',
  fromDate: '',
  toDate: '',
}
const emptyFilters = { ...emptyDraft, page: 1, pageSize }

function initialFilters(searchParams) {
  const query = Object.fromEntries(searchParams)
  const dateRange = supportedDateRanges.has(query.dateRange)
    ? query.dateRange
    : query.fromDate || query.toDate ? 'custom' : 'all'
  return {
    ...emptyFilters,
    ...query,
    dateRange,
    page: Math.max(1, Number(query.page) || 1),
    pageSize,
  }
}

function urlFilters(filters) {
  return Object.fromEntries(Object.entries(filters).filter(([, value]) =>
    value !== '' && value !== 'all' && value !== 1 && value !== pageSize))
}

function getApiFilters(filters) {
  const { fromUtc, toUtcExclusive } =
    getUtcDateRange(filters.fromDate, filters.toDate)
  return {
    search: filters.search,
    statusId: filters.statusId,
    categoryId: filters.categoryId,
    priorityId: filters.priorityId,
    fromUtc,
    toUtcExclusive,
    page: filters.page,
    pageSize: filters.pageSize,
    sortBy: 'assignedDate',
    sortDirection: 'desc',
  }
}

const viewContent = {
  assigned: ['Assigned Tickets', 'Review and manage the support requests assigned to you.', 'assigned'],
  open: ['Open Tickets', 'Review unassigned requests and ask a Manager for assignment.', 'open'],
}

function AgentTicketsPage({ view = 'assigned' }) {
  const [title, subtitle, noun] = viewContent[view] ?? viewContent.assigned
  const [searchParams, setSearchParams] = useSearchParams()
  const initial = initialFilters(searchParams)
  const [draft, setDraft] = useState({
    ...emptyDraft,
    ...initial,
  })
  const [filters, setFilters] = useState(initial)
  const [dateError, setDateError] = useState('')
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)
  const [lookups, setLookups] = useState({
    statuses: [],
    categories: [],
    priorities: [],
  })

  useEffect(() => {
    const next = initialFilters(searchParams)
    setDraft((current) => {
      const nextDraft = { ...emptyDraft, ...next }
      return JSON.stringify(current) === JSON.stringify(nextDraft)
        ? current
        : nextDraft
    })
    setFilters((current) =>
      JSON.stringify(current) === JSON.stringify(next) ? current : next)
  }, [searchParams])

  useEffect(() => {
    const controller = new AbortController()
    Promise.all([
      getStatuses(controller.signal),
      getCategories(controller.signal),
      getPriorities(controller.signal),
    ])
      .then(([statuses, categories, priorities]) => {
        if (controller.signal.aborted) return
        setLookups({ statuses, categories, priorities })
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) {
          setError(requestError.message)
        }
      })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    setData(null)
    setError('')
    const loadTickets = view === 'open' ? getOpenTickets : getAssignedTickets
    loadTickets(getApiFilters(filters), controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setData(result)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) {
          setError(requestError.message)
        }
      })
    return () => controller.abort()
  }, [filters, reload, view])

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
    const nextDraft = { ...draft, ...dates }
    setDateError('')
    setDraft(nextDraft)
    const next = { ...nextDraft, page: 1, pageSize }
    setFilters(next)
    setSearchParams(urlFilters(next))
  }

  function clearFilters() {
    setDraft(emptyDraft)
    setFilters(emptyFilters)
    setSearchParams({})
    setDateError('')
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

  const hasActiveFilters = Boolean(
    filters.search || filters.statusId || filters.categoryId ||
    filters.priorityId || filters.dateRange !== 'all',
  )

  return (
    <>
      <section className="page-heading">
        <h2>{title}</h2>
        <p>{subtitle}</p>
      </section>

      <form className="filter-panel ticket-filters" onSubmit={applyFilters}>
        <div className="ticket-filters__grid">
          <label className="filter-search"><span>Search</span><input value={draft.search} onChange={(event) => setDraft({ ...draft, search: event.target.value })} placeholder="Ticket number or title" /></label>
          {[
            ['statusId', 'Status', lookups.statuses],
            ['categoryId', 'Category', lookups.categories],
            ['priorityId', 'Priority', lookups.priorities],
          ].map(([key, label, options]) => (
            <label key={key}>
              <span>{label}</span>
              <select value={draft[key]} onChange={(event) => setDraft({ ...draft, [key]: event.target.value })}>
                <option value="">All</option>
                {options.map((option) => <option value={option.id} key={option.id}>{option.name}</option>)}
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
        {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
        {!error && !data && <LoadingState message="Loading assigned tickets…" />}
        {data && (
          <>
            <div className="results-count">{data.totalItems} {noun} ticket{data.totalItems === 1 ? '' : 's'}</div>
            {data.items.length === 0 ? (
              <EmptyState
                title={hasActiveFilters ? 'No tickets match the selected filters' : 'No tickets found.'}
                message={hasActiveFilters ? 'Try changing or clearing the current filters.' : `${title} will appear here.`}
              />
            ) : (
              <div className="table-scroll agent-ticket-table-wrap">
                <table className="ticket-table agent-ticket-table">
                  <colgroup>
                    <col className="agent-ticket-col--number" />
                    <col className="agent-ticket-col--title" />
                    <col className="agent-ticket-col--requester" />
                    <col className="agent-ticket-col--category" />
                    <col className="agent-ticket-col--priority" />
                    <col className="agent-ticket-col--status" />
                    <col className="agent-ticket-col--created" />
                    <col className="agent-ticket-col--action" />
                  </colgroup>
                  <thead><tr><th>Ticket Number</th><th>Title</th><th>Requester</th><th>Category</th><th>Priority</th><th>Status</th><th>Created</th><th>Action</th></tr></thead>
                  <tbody>
                    {data.items.map((ticket) => (
                      <tr key={ticket.id}>
                        <td><strong>{formatTicketReference(ticket)}</strong></td>
                        <td><span className="agent-ticket-title" title={ticket.title}>{ticket.title}</span></td>
                        <td><span className="agent-ticket-ellipsis" title={ticket.requesterName}>{ticket.requesterName}</span></td>
                        <td><span className="agent-ticket-ellipsis" title={ticket.categoryName}>{ticket.categoryName}</span></td>
                        <td><TicketPriorityBadge value={ticket.priorityName} /></td>
                        <td><TicketStatusBadge value={ticket.statusName} /></td>
                        <td><span className="agent-ticket-ellipsis" title={formatLocalDate(ticket.createdDate)}>{formatLocalDate(ticket.createdDate)}</span></td>
                        <td><Link className="table-action" to={`/agent/tickets/${formatTicketReference(ticket)}`}>View</Link></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <Pagination
              page={data.page}
              totalPages={data.totalPages}
              onChange={(page) => {
                const next = { ...filters, page }
                setFilters(next)
                setSearchParams(urlFilters(next))
              }}
            />
          </>
        )}
      </section>
    </>
  )
}

export default AgentTicketsPage
