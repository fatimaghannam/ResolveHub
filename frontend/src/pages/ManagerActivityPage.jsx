import { useEffect, useMemo, useState } from 'react'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { getManagerActivity } from '../services/managerService.js'
import { formatLocalDate } from '../utils/dateTime.js'

function ManagerActivityPage() {
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [action, setAction] = useState('')
  useEffect(() => {
    const controller = new AbortController()
    getManagerActivity(controller.signal)
      .then((result) => { if (!controller.signal.aborted) setData(result) })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setError(requestError.message)
      })
    return () => controller.abort()
  }, [])
  const items = useMemo(() => data?.items ?? [], [data])
  const filtered = useMemo(() => items.filter((item) => {
    const query = search.trim().toLowerCase()
    return (!action || item.actionType === action) &&
      (!query || `${item.ticketReferenceNumber} ${item.ticketTitle} ${item.actorName}`.toLowerCase().includes(query))
  }), [items, search, action])
  return <>
    <section className="page-heading"><h2>Ticket Activity</h2><p>Review operational ticket changes without exposing administrator security events.</p></section>
    <section className="filter-panel manager-activity-filters"><label><span>Search</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Ticket number, title, or user" /></label><label><span>Activity Type</span><select value={action} onChange={(event) => setAction(event.target.value)}><option value="">All</option>{[...new Set(items.map((item) => item.actionType))].map((value) => <option key={value}>{value}</option>)}</select></label></section>
    {error && <ErrorState message={error} />}
    {!error && !data && <LoadingState message="Loading ticket activity…" />}
    {data && <section className="panel">{filtered.length === 0 ? <EmptyState title="No activity found" message="Try changing the current filters." /> : <div className="table-scroll"><table className="ticket-table admin-activity-table"><thead><tr><th>Date</th><th>Actor</th><th>Action</th><th>Ticket</th><th>Description</th></tr></thead><tbody>{filtered.map((item) => <tr key={item.id}><td>{formatLocalDate(item.createdDate)}</td><td>{item.actorName}</td><td>{item.actionType}</td><td>{item.ticketReferenceNumber}</td><td>{item.description}</td></tr>)}</tbody></table></div>}</section>}
  </>
}

export default ManagerActivityPage
