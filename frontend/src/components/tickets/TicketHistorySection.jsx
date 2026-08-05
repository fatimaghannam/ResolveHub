import { ArrowRight, ChevronDown } from 'lucide-react'
import { useMemo, useState } from 'react'
import { formatLocalDateTime } from '../../utils/dateTime.js'

export default function TicketHistorySection({ history }) {
  const [open, setOpen] = useState(false)
  const contentId = `ticket-history-${history.length}`
  const orderedHistory = useMemo(() => history.toSorted((left, right) =>
    new Date(right.createdDate) - new Date(left.createdDate)), [history])

  return <section className={`panel dashboard-section unified-collapsible ticket-history-collapsible ${open ? 'is-open' : ''}`}>
    <button className="unified-collapsible__header" type="button" onClick={() => setOpen((value) => !value)} aria-expanded={open} aria-controls={contentId}>
      <span className="activity-log__title"><span><strong>Ticket History</strong><small>Read-only lifecycle and action record.</small></span></span>
      <ChevronDown className="section-header__chevron" size={18} strokeWidth={2} aria-hidden="true" />
    </button>
    <div className="unified-collapsible__content" id={contentId} aria-hidden={!open}>
      <div className="unified-collapsible__inner">
        <div className="ticket-history__content">
        {orderedHistory.length === 0
          ? <p className="unified-collapsible__empty">No ticket history is available yet.</p>
          : <div className="table-scroll"><table className="ticket-table"><thead><tr><th>Action</th><th>Performed By</th><th>Description</th><th>Date and Time</th></tr></thead><tbody>{orderedHistory.map((item) => <tr key={item.id}><td><strong>{item.actionType}</strong></td><td>{item.performedByName}</td><td><span className="ticket-history__description">{item.description ?? '—'}</span>{(item.oldValue || item.newValue) && <span className="ticket-history__change"><span>{item.oldValue || '—'}</span><ArrowRight size={13} aria-hidden="true" /><strong>{item.newValue || '—'}</strong></span>}</td><td><time dateTime={item.createdDate}>{formatLocalDateTime(item.createdDate)}</time></td></tr>)}</tbody></table></div>}
        </div>
      </div>
    </div>
  </section>
}
