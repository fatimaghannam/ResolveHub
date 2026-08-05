import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import Pagination from '../components/common/Pagination.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { cancelTicket, getCategories, getPriorities, getStatuses, getTickets } from '../services/ticketService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'
import {
  getLocalQuickDateRange,
  getUtcDateRange,
  STANDARD_DATE_RANGE_OPTIONS,
} from '../utils/dateRange.js'
import { formatTicketReference } from '../utils/ticketReference.js'

const dateRangeOptions = STANDARD_DATE_RANGE_OPTIONS
const supportedDateRanges = new Set(dateRangeOptions.map(([value]) => value))
const emptyFilters = { search: '', statusId: '', categoryId: '', priorityId: '', dateRange: 'all', fromDate: '', toDate: '', page: 1, pageSize: 10 }

function getInitialFilters(searchParams) {
  const query = Object.fromEntries(searchParams)
  const dateRange = supportedDateRanges.has(query.dateRange)
    ? query.dateRange
    : query.fromDate || query.toDate ? 'custom' : 'all'
  const dates = query.fromDate || query.toDate
    ? { fromDate: query.fromDate ?? '', toDate: query.toDate ?? '' }
    : getLocalQuickDateRange(dateRange) ?? { fromDate: '', toDate: '' }
  return {
    ...emptyFilters,
    ...query,
    ...dates,
    dateRange,
    page: Math.max(1, Number(query.page) || 1),
    pageSize: Math.max(1, Number(query.pageSize) || 10),
  }
}

function getUrlFilters(values) {
  return Object.fromEntries(Object.entries(values).filter(([, value]) =>
    value !== '' && value !== 1 && value !== 10))
}

function getApiFilters(values) {
  const { fromUtc, toUtcExclusive } = getUtcDateRange(
    values.fromDate,
    values.toDate,
  )
  return {
    search: values.search,
    statusId: values.statusId,
    categoryId: values.categoryId,
    priorityId: values.priorityId,
    fromUtc,
    toUtcExclusive,
    page: values.page,
    pageSize: values.pageSize,
    sortBy: 'createdDate',
    sortDirection: 'desc',
  }
}

function EmployeeTicketsPage({ roleArea = 'employee' }) {
  const location = useLocation()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const initial = getInitialFilters(searchParams)
  const [draft, setDraft] = useState(initial)
  const [filters, setFilters] = useState(initial)
  const [dateError, setDateError] = useState('')
  const [data, setData] = useState(null)
  const [lookups, setLookups] = useState({ statuses: [], categories: [], priorities: [] })
  const [error, setError] = useState('')
  const [cancelTarget, setCancelTarget] = useState(null)
  const [reason, setReason] = useState('')
  const [cancelling, setCancelling] = useState(false)
  const [reload, setReload] = useState(0)
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
    Promise.all([getStatuses(controller.signal), getCategories(controller.signal), getPriorities(controller.signal)])
      .then(([statuses, categories, priorities]) => {
        if (!controller.signal.aborted) setLookups({ statuses, categories, priorities })
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) setError(requestError.message)
      })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    setData(null); setError('')
    getTickets(getApiFilters(filters), controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setData(result)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) setError(requestError.message)
      })
    return () => controller.abort()
  }, [filters, reload])

  useEffect(() => {
    if (!cancelTarget) return undefined
    const closeOnEscape = (event) => {
      if (event.key === 'Escape' && !cancelling) setCancelTarget(null)
    }
    document.addEventListener('keydown', closeOnEscape)
    return () => document.removeEventListener('keydown', closeOnEscape)
  }, [cancelTarget, cancelling])

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
    const next = { ...draft, ...dates, page: 1 }
    setDateError('')
    setDraft(next)
    setFilters(next)
    setSearchParams(getUrlFilters(next))
  }

  function clearFilters() {
    const cleared = { ...emptyFilters }
    setDraft(cleared); setFilters(cleared); setDateError(''); setSearchParams({})
  }

  function changePage(page) {
    const next = { ...filters, page }
    setFilters(next)
    setSearchParams(getUrlFilters(next))
  }

  function changeDateRange(value) {
    setDateError('')
    if (value === 'custom') {
      setDraft((current) => ({ ...current, dateRange: value, fromDate: '', toDate: '', page: 1 }))
      return
    }
    const dates = getLocalQuickDateRange(value)
    setDraft((current) => ({ ...current, dateRange: value, ...dates, page: 1 }))
  }

  function changeCustomDate(field, value) {
    const next = { ...draft, dateRange: 'custom', [field]: value, page: 1 }
    setDraft(next)
    if (next.fromDate && next.toDate && next.toDate >= next.fromDate) setDateError('')
  }

  async function confirmCancel() {
    try {
      setCancelling(true)
      await cancelTicket(cancelTarget.id, reason)
      setCancelTarget(null); setReason(''); setReload((value) => value + 1)
      setToast({
        id: Date.now(),
        type: 'success',
        title: 'Ticket Cancelled',
        message: `${formatTicketReference(cancelTarget)} was cancelled.`,
      })
    } catch (requestError) {
      setToast({
        id: Date.now(),
        type: 'error',
        title: 'Unable to Cancel Ticket',
        message: requestError.status === 409
          ? 'This ticket can no longer be cancelled because it has already been assigned or work has started.'
          : requestError.message,
      })
      setCancelTarget(null)
    } finally { setCancelling(false) }
  }

  function detailsPath(ticket) {
    return roleArea === 'employee'
      ? `/employee/tickets/${ticket.id}`
      : `/${roleArea}/tickets/${formatTicketReference(ticket)}`
  }

  function editPath(ticket) {
    return roleArea === 'employee'
      ? `/employee/tickets/${ticket.id}/edit`
      : `/${roleArea}/my-tickets/${ticket.id}/edit`
  }

  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <section className="page-heading page-heading--action"><div><h2>My Tickets</h2><p>Search, filter, and manage your support requests.</p></div><div className="heading-actions"><Link className="button button--secondary" to={`/${roleArea}/tickets/drafts`}>Drafts</Link><Link className="button button--primary" to={`/${roleArea}/tickets/create`}>Create Ticket</Link></div></section>
      <form className="filter-panel ticket-filters" onSubmit={applyFilters}>
        <div className="ticket-filters__grid">
          <label className="filter-search"><span>Search</span><input value={draft.search} onChange={(e) => setDraft({ ...draft, search: e.target.value })} placeholder="Ticket number or title" /></label>
          {['statusId', 'categoryId', 'priorityId'].map((key) => (
            <label key={key}><span>{key === 'statusId' ? 'Status' : key === 'categoryId' ? 'Category' : 'Priority'}</span>
              <select value={draft[key]} onChange={(e) => setDraft({ ...draft, [key]: e.target.value })}>
                <option value="">All</option>{lookups[key === 'statusId' ? 'statuses' : key === 'categoryId' ? 'categories' : 'priorities'].map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}
              </select>
            </label>
          ))}
          <label><span>Date Range</span><select value={draft.dateRange} onChange={(e) => changeDateRange(e.target.value)}>
            {dateRangeOptions.map(([value, label]) => <option value={value} key={value}>{label}</option>)}
          </select></label>
        </div>
        <div className={`ticket-filters__custom-date-row ${draft.dateRange === 'custom' ? 'ticket-filters__custom-date-row--visible' : ''}`}>
          {draft.dateRange === 'custom' && (
            <>
              <label><span>From Date</span><input type="date" value={draft.fromDate} onChange={(e) => changeCustomDate('fromDate', e.target.value)} aria-invalid={Boolean(dateError)} aria-describedby={dateError ? 'date-range-error' : undefined} /></label>
              <label><span>To Date</span><input type="date" value={draft.toDate} onChange={(e) => changeCustomDate('toDate', e.target.value)} aria-invalid={Boolean(dateError)} aria-describedby={dateError ? 'date-range-error' : undefined} /></label>
            </>
          )}
          <div className="filter-actions"><button className="button button--primary" type="submit">Apply Filters</button><button className="button button--secondary" type="button" onClick={clearFilters}>Clear</button></div>
        </div>
        {dateError && <p className="filter-validation" id="date-range-error" role="alert">{dateError}</p>}
      </form>
      {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
      {!error && !data && <LoadingState message="Loading tickets…" />}
      {data && <section className="panel">
        <div className="results-count">{data.totalItems} ticket{data.totalItems === 1 ? '' : 's'}</div>
        {data.items.length === 0 ? <EmptyState title="No tickets found." message="Try changing your filters or create a new support ticket." /> : (
          <div className="table-scroll"><table className="ticket-table">
            <thead><tr><th>Ticket Number</th><th>Title</th><th>Category</th><th>Priority</th><th>Status</th><th>Assigned To</th><th>Created</th><th>Actions</th></tr></thead>
            <tbody>{data.items.map((ticket) => <tr key={ticket.id}>
              <td><strong>{formatTicketReference(ticket)}</strong></td><td>{ticket.title}</td><td>{ticket.categoryName}</td>
              <td><TicketPriorityBadge value={ticket.priorityName} /></td><td><TicketStatusBadge value={ticket.statusName} /></td>
              <td>{ticket.assignedToName ?? 'Unassigned'}</td><td>{formatLocalDateTime(ticket.createdDate)}</td>
              <td><div className="row-actions"><Link to={detailsPath(ticket)}>View</Link>{ticket.canEdit && <Link to={editPath(ticket)}>Edit</Link>}{ticket.canDelete && <button onClick={() => setCancelTarget(ticket)}>Delete</button>}</div></td>
            </tr>)}</tbody>
          </table></div>
        )}
        <Pagination page={data.page} totalPages={data.totalPages} onChange={changePage} />
      </section>}
      {cancelTarget && <div className="dialog-backdrop" role="presentation"><div className="dialog" role="dialog" aria-modal="true" aria-labelledby="cancel-title" aria-describedby="cancel-description">
        <h2 id="cancel-title">Cancel {formatTicketReference(cancelTarget)}?</h2><p id="cancel-description">This removes the ticket from your active list. This action cannot be undone.</p>
        <label><span>Reason (optional)</span><select value={reason} onChange={(e) => setReason(e.target.value)}><option value="">Select a reason</option><option>Created by mistake</option><option>Duplicate ticket</option><option>Issue no longer exists</option><option>Other</option></select></label>
        <div className="dialog__actions"><button autoFocus type="button" className="button button--secondary" onClick={() => setCancelTarget(null)} disabled={cancelling}>Keep Ticket</button><button type="button" className="button button--danger" onClick={confirmCancel} disabled={cancelling}>{cancelling ? 'Cancelling…' : 'Cancel Ticket'}</button></div>
      </div></div>}
    </>
  )
}

export default EmployeeTicketsPage
