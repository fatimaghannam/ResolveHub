import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import Pagination from '../components/common/Pagination.jsx'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../components/tickets/TicketBadges.jsx'
import { cancelTicket, getCategories, getPriorities, getStatuses, getTickets } from '../services/ticketService.js'

const emptyFilters = { search: '', statusId: '', categoryId: '', priorityId: '', fromDate: '', toDate: '', page: 1, pageSize: 10 }

function EmployeeTicketsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const initial = { ...emptyFilters, ...Object.fromEntries(searchParams) }
  const [draft, setDraft] = useState(initial)
  const [filters, setFilters] = useState(initial)
  const [data, setData] = useState(null)
  const [lookups, setLookups] = useState({ statuses: [], categories: [], priorities: [] })
  const [error, setError] = useState('')
  const [cancelTarget, setCancelTarget] = useState(null)
  const [reason, setReason] = useState('')
  const [cancelling, setCancelling] = useState(false)
  const [reload, setReload] = useState(0)

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
    getTickets({ ...filters, sortBy: 'createdDate', sortDirection: 'desc' }, controller.signal)
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
    const next = { ...draft, page: 1 }
    setFilters(next)
    setSearchParams(Object.fromEntries(Object.entries(next).filter(([, value]) => value !== '' && value !== 1 && value !== 10)))
  }

  function clearFilters() {
    setDraft(emptyFilters); setFilters(emptyFilters); setSearchParams({})
  }

  function changePage(page) {
    const next = { ...filters, page }
    setFilters(next)
    setSearchParams(Object.fromEntries(Object.entries(next).filter(([, value]) => value !== '' && value !== 1 && value !== 10)))
  }

  async function confirmCancel() {
    try {
      setCancelling(true)
      await cancelTicket(cancelTarget.id, reason)
      setCancelTarget(null); setReason(''); setReload((value) => value + 1)
    } catch (requestError) {
      setError(requestError.status === 409 ? 'This ticket can no longer be deleted because it has already been assigned or work has started.' : requestError.message)
      setCancelTarget(null)
    } finally { setCancelling(false) }
  }

  return (
    <>
      <section className="page-heading page-heading--action"><div><h2>My Tickets</h2><p>Search, filter, and manage your support requests.</p></div><div className="heading-actions"><Link className="button button--secondary" to="/employee/tickets/drafts">Drafts</Link><Link className="button button--primary" to="/employee/tickets/create">Create Ticket</Link></div></section>
      <form className="filter-panel" onSubmit={applyFilters}>
        <label className="filter-search"><span>Search</span><input value={draft.search} onChange={(e) => setDraft({ ...draft, search: e.target.value })} placeholder="Ticket number or title" /></label>
        {['statusId', 'categoryId', 'priorityId'].map((key) => (
          <label key={key}><span>{key === 'statusId' ? 'Status' : key === 'categoryId' ? 'Category' : 'Priority'}</span>
            <select value={draft[key]} onChange={(e) => setDraft({ ...draft, [key]: e.target.value })}>
              <option value="">All</option>{lookups[key === 'statusId' ? 'statuses' : key === 'categoryId' ? 'categories' : 'priorities'].map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}
            </select>
          </label>
        ))}
        <label><span>From</span><input type="date" value={draft.fromDate} onChange={(e) => setDraft({ ...draft, fromDate: e.target.value })} /></label>
        <label><span>To</span><input type="date" value={draft.toDate} onChange={(e) => setDraft({ ...draft, toDate: e.target.value })} /></label>
        <div className="filter-actions"><button className="button button--primary" type="submit">Apply Filters</button><button className="button button--secondary" type="button" onClick={clearFilters}>Clear</button></div>
      </form>
      {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
      {!error && !data && <LoadingState message="Loading tickets…" />}
      {data && <section className="panel">
        <div className="results-count">{data.totalItems} ticket{data.totalItems === 1 ? '' : 's'}</div>
        {data.items.length === 0 ? <EmptyState title="No tickets found" message="Try changing your filters or create a new support ticket." /> : (
          <div className="table-scroll"><table className="ticket-table">
            <thead><tr><th>Ticket Number</th><th>Title</th><th>Category</th><th>Priority</th><th>Status</th><th>Assigned To</th><th>Created</th><th>Actions</th></tr></thead>
            <tbody>{data.items.map((ticket) => <tr key={ticket.id}>
              <td><strong>{ticket.ticketReferenceNumber}</strong></td><td>{ticket.title}</td><td>{ticket.categoryName}</td>
              <td><TicketPriorityBadge value={ticket.priorityName} /></td><td><TicketStatusBadge value={ticket.statusName} /></td>
              <td>{ticket.assignedToName ?? 'Unassigned'}</td><td>{new Date(ticket.createdDate).toLocaleDateString()}</td>
              <td><div className="row-actions"><Link to={`/employee/tickets/${ticket.id}`}>View</Link>{ticket.canEdit && <Link to={`/employee/tickets/${ticket.id}/edit`}>Edit</Link>}{ticket.canDelete && <button onClick={() => setCancelTarget(ticket)}>Delete</button>}</div></td>
            </tr>)}</tbody>
          </table></div>
        )}
        <Pagination page={data.page} totalPages={data.totalPages} onChange={changePage} />
      </section>}
      {cancelTarget && <div className="dialog-backdrop" role="presentation"><div className="dialog" role="dialog" aria-modal="true" aria-labelledby="cancel-title" aria-describedby="cancel-description">
        <h2 id="cancel-title">Cancel {cancelTarget.ticketReferenceNumber}?</h2><p id="cancel-description">This removes the ticket from your active list. This action cannot be undone.</p>
        <label><span>Reason (optional)</span><select value={reason} onChange={(e) => setReason(e.target.value)}><option value="">Select a reason</option><option>Created by mistake</option><option>Duplicate ticket</option><option>Issue no longer exists</option><option>Other</option></select></label>
        <div className="dialog__actions"><button autoFocus type="button" className="button button--secondary" onClick={() => setCancelTarget(null)} disabled={cancelling}>Keep Ticket</button><button type="button" className="button button--danger" onClick={confirmCancel} disabled={cancelling}>{cancelling ? 'Cancelling…' : 'Cancel Ticket'}</button></div>
      </div></div>}
    </>
  )
}

export default EmployeeTicketsPage
