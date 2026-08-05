import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { getSystemAuditLog } from '../services/adminService.js'
import { formatLocalDate, formatLocalDateTime, formatLocalTime } from '../utils/dateTime.js'

const initialFilters = {
  search: '', dateRange: 'all', fromDate: '', toDate: '', page: 1, pageSize: 20,
}

function AdminActivityPage() {
  const [filters, setFilters] = useState(initialFilters)
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [dateError, setDateError] = useState('')
  const [selected, setSelected] = useState(null)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    if (!selected) return undefined
    function closeOnEscape(event) {
      if (event.key === 'Escape') setSelected(null)
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [selected])

  useEffect(() => {
    if (filters.dateRange === 'custom' && filters.fromDate && filters.toDate && filters.fromDate > filters.toDate) {
      setDateError('From Date cannot be later than To Date.')
      return undefined
    }
    setDateError('')
    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      const request = Object.fromEntries(Object.entries(filters).filter(([, value]) => value !== ''))
      if (request.dateRange !== 'custom') {
        delete request.fromDate
        delete request.toDate
      }
      getSystemAuditLog(request, controller.signal)
        .then((result) => { setData(result); setError('') })
        .catch((requestError) => {
          if (requestError.name !== 'AbortError') setError('We could not load the System Audit Log. Please try again.')
        })
    }, 250)
    return () => { window.clearTimeout(timer); controller.abort() }
  }, [filters, reload])

  function updateFilter(name, value) {
    setFilters((current) => ({
      ...current,
      [name]: value,
      page: 1,
      ...(name === 'dateRange' && value !== 'custom' ? { fromDate: '', toDate: '' } : {}),
    }))
  }

  return (
    <>
      <section className="page-heading"><h2>System Audit Log</h2><p>Review important administrative, security, and system-level actions across ResolveHub.</p></section>
      <section className="filter-panel system-audit-filters">
        <label className="filter-search"><span>Search</span><input value={filters.search} onChange={(event) => updateFilter('search', event.target.value)} placeholder="Action, user, ticket, category, or details" /></label>
        <label><span>Date Range</span><select value={filters.dateRange} onChange={(event) => updateFilter('dateRange', event.target.value)}><option value="all">All Dates</option><option value="today">Today</option><option value="yesterday">Yesterday</option><option value="7">Last 7 Days</option><option value="30">Last 30 Days</option><option value="custom">Custom Range</option></select></label>
        <button className="button button--secondary" type="button" onClick={() => setFilters(initialFilters)}>Clear</button>
        {filters.dateRange === 'custom' && <div className="system-audit-custom-dates"><label><span>From Date</span><input type="date" value={filters.fromDate} max={filters.toDate || undefined} onChange={(event) => updateFilter('fromDate', event.target.value)} /></label><label><span>To Date</span><input type="date" value={filters.toDate} min={filters.fromDate || undefined} onChange={(event) => updateFilter('toDate', event.target.value)} /></label>{dateError && <p className="form-error" role="alert">{dateError}</p>}</div>}
      </section>

      {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
      {!error && !data && <LoadingState message="Loading audit records…" />}
      {!error && data && <section className="panel system-audit-panel">
        <div className="results-count">{data.totalItems} audit record{data.totalItems === 1 ? '' : 's'}</div>
        {data.items.length === 0
          ? <EmptyState title="No audit records" message="No audit records match your search and date range." />
          : <div className="system-audit-table-wrap"><table className="ticket-table system-audit-table">
            <colgroup><col className="audit-col--user" /><col className="audit-col--action" /><col className="audit-col--entity" /><col className="audit-col--details" /><col className="audit-col--date" /></colgroup>
            <thead><tr><th>User</th><th>Action</th><th>Entity</th><th>Details</th><th>Date</th></tr></thead>
            <tbody>{data.items.map((item) => <tr key={item.id} tabIndex="0" role="button" onClick={() => setSelected(item)} onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); setSelected(item) } }}>
              <td><strong>{item.performedByName}</strong><small>{item.performerRole}</small></td>
              <td><strong>{item.action}</strong><small>{item.actionCategory}</small></td>
              <td><strong>{item.entityType}</strong><small>{item.entityDisplayName}</small></td>
              <td className="audit-details">{item.description}</td>
              <td><time className="audit-date" dateTime={item.createdAt} title={formatLocalDateTime(item.createdAt)}><span>{formatLocalDate(item.createdAt)}</span><span>{formatLocalTime(item.createdAt)}</span></time></td>
            </tr>)}</tbody>
          </table></div>}
        {data.totalPages > 1 && <nav className="pagination" aria-label="Audit log pages"><button type="button" disabled={data.page <= 1} onClick={() => setFilters((current) => ({ ...current, page: current.page - 1 }))}>Previous</button><span>Page {data.page} of {data.totalPages}</span><button type="button" disabled={data.page >= data.totalPages} onClick={() => setFilters((current) => ({ ...current, page: current.page + 1 }))}>Next</button></nav>}
      </section>}

      {selected && <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) setSelected(null) }}><div className="dialog system-audit-dialog" role="dialog" aria-modal="true" aria-labelledby="audit-details-title"><h2 id="audit-details-title">Audit Record Details</h2><div className="audit-record-summary"><div><small>Action</small><strong>{selected.action}</strong></div><div><small>Entity</small><strong>{selected.entityType}</strong><span>{selected.entityDisplayName}</span></div><div><small>Date and time</small><strong>{formatLocalDateTime(selected.createdAt)}</strong></div></div><dl className="audit-record-details"><div><dt>Performed by</dt><dd>{selected.performedByName}</dd></div><div><dt>Performer role</dt><dd>{selected.performerRole}</dd></div><div><dt>Action category</dt><dd>{selected.actionCategory}</dd></div><div><dt>Entity</dt><dd>{selected.entityDisplayName}</dd></div><div className="audit-record-details__wide"><dt>Description</dt><dd>{selected.description}</dd></div>{selected.oldValue && <div><dt>Previous value</dt><dd>{selected.oldValue}</dd></div>}{selected.newValue && <div><dt>New value</dt><dd>{selected.newValue}</dd></div>}{selected.relatedUrl && <div className="audit-record-details__wide"><dt>Related record</dt><dd><Link to={selected.relatedUrl} onClick={(event) => event.stopPropagation()}>{selected.entityType === 'User Account' ? 'View user profile' : selected.entityType === 'Ticket' ? 'View ticket' : selected.entityType === 'Ticket Category' ? 'View category' : 'View record'}</Link></dd></div>}</dl><div className="dialog__actions"><button autoFocus className="button button--secondary" type="button" onClick={() => setSelected(null)}>Close</button></div></div></div>}
    </>
  )
}

export default AdminActivityPage
