import { useMemo, useState } from 'react'
import { EmptyState } from '../components/common/States.jsx'
import { adminActivity } from '../data/index.js'
import { formatLocalDateTime } from '../utils/dateTime.js'

function AdminActivityPage() {
  const [search, setSearch] = useState('')
  const [actionType, setActionType] = useState('')
  const [user, setUser] = useState('')
  const [dateRange, setDateRange] = useState('all')
  const filtered = useMemo(() => adminActivity.filter((item) => {
    const query = search.trim().toLowerCase()
    if (query && !`${item.action} ${item.entity} ${item.details}`.toLowerCase().includes(query)) return false
    if (actionType && item.actionType !== actionType) return false
    if (user && item.user !== user) return false
    if (dateRange !== 'all') {
      const cutoff = new Date()
      cutoff.setDate(cutoff.getDate() - Number(dateRange))
      if (new Date(item.timestamp) < cutoff) return false
    }
    return true
  }), [search, actionType, user, dateRange])

  return (
    <>
      <section className="page-heading"><h2>Activity Logs</h2><p>Review important administrative and ticket-related actions.</p></section>
      <section className="filter-panel admin-activity-filters">
        <label className="filter-search"><span>Search</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Action, ticket, or details" /></label>
        <label><span>Action Type</span><select value={actionType} onChange={(event) => setActionType(event.target.value)}><option value="">All</option>{[...new Set(adminActivity.map((item) => item.actionType))].map((value) => <option key={value}>{value}</option>)}</select></label>
        <label><span>User</span><select value={user} onChange={(event) => setUser(event.target.value)}><option value="">All</option>{[...new Set(adminActivity.map((item) => item.user))].map((value) => <option key={value}>{value}</option>)}</select></label>
        <label><span>Date Range</span><select value={dateRange} onChange={(event) => setDateRange(event.target.value)}><option value="all">All Dates</option><option value="7">Last 7 Days</option><option value="30">Last 30 Days</option></select></label>
      </section>
      <section className="panel">{filtered.length === 0 ? <EmptyState title="No activity found" message="Try changing the current filters." /> : <div className="table-scroll"><table className="ticket-table admin-activity-table"><thead><tr><th>Date</th><th>User</th><th>Action</th><th>Entity</th><th>Details</th></tr></thead><tbody>{filtered.map((item) => <tr key={item.id}><td>{formatLocalDateTime(item.timestamp)}</td><td>{item.user}</td><td>{item.actionType}</td><td>{item.entity}</td><td>{item.details}</td></tr>)}</tbody></table></div>}</section>
    </>
  )
}

export default AdminActivityPage
