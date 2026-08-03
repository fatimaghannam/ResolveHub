import { ChevronDown } from 'lucide-react'
import { useState } from 'react'

export default function TicketHistorySection({ history, formatDate }) {
  const [open, setOpen] = useState(false)
  const contentId = `ticket-history-${history.length}`

  return <section className={`panel dashboard-section unified-collapsible ticket-history-collapsible ${open ? 'is-open' : ''}`}>
    <button className="unified-collapsible__header" type="button" onClick={() => setOpen((value) => !value)} aria-expanded={open} aria-controls={contentId}>
      <span className="activity-log__title"><span><strong>Ticket History</strong><small>Read-only lifecycle and action record.</small></span></span>
      <ChevronDown className="section-header__chevron" size={18} strokeWidth={2} aria-hidden="true" />
    </button>
    <div className="unified-collapsible__content" id={contentId} aria-hidden={!open}>
      <div className="unified-collapsible__inner">
        <div className="ticket-history__content">
        {history.length === 0
          ? <p className="unified-collapsible__empty">No ticket history has been recorded.</p>
          : <div className="table-scroll"><table className="ticket-table"><thead><tr><th>Action</th><th>Performed By</th><th>Description</th><th>Date</th></tr></thead><tbody>{history.map((item) => <tr key={item.id}><td><strong>{item.actionType}</strong></td><td>{item.performedByName}</td><td>{item.description ?? '—'}</td><td>{formatDate(item.createdDate)}</td></tr>)}</tbody></table></div>}
        </div>
      </div>
    </div>
  </section>
}
