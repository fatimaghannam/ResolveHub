import { Link } from 'react-router-dom'

function capacityModifier(value) {
  return value.toLowerCase().replaceAll(' ', '-')
}

export function CapacityBadge({ state }) {
  return <span className={`capacity-badge capacity-badge--${capacityModifier(state)}`}>{state}</span>
}

export function AgentWorkloadSummary({ agent }) {
  return (
    <article className="dashboard-workload-item">
      <div>
        <strong>{agent.name}</strong>
        <small>{agent.activeTicketCount}/{agent.maxActiveTickets} active · {agent.remainingCapacity} remaining</small>
      </div>
      <CapacityBadge state={agent.capacityState} />
    </article>
  )
}

export function AgentWorkloadCard({ agent, ticketPath }) {
  const progress = Math.min(100, (agent.activeTicketCount / agent.maxActiveTickets) * 100)
  const slotLabel = agent.remainingCapacity === 1 ? 'slot' : 'slots'

  return (
    <article className="workload-card workload-card--capacity">
      <div className="workload-card__content">
        <header className="workload-card__header">
          <div><h3>{agent.name}</h3>{agent.email && <small title={agent.email}>{agent.email}</small>}</div>
          <CapacityBadge state={agent.capacityState} />
        </header>
        <div className="workload-card__capacity">
          <strong>{agent.activeTicketCount} <span>/ {agent.maxActiveTickets}</span></strong>
          <small>Active tickets</small>
        </div>
        <div className="workload-card__progress" role="progressbar" aria-label={`${agent.name} active workload`} aria-valuenow={agent.activeTicketCount} aria-valuemin="0" aria-valuemax={agent.maxActiveTickets}>
          <span className={`workload-progress__fill workload-progress__fill--${capacityModifier(agent.capacityState)}`} style={{ width: `${progress}%` }} />
        </div>
        <p className="workload-card__remaining">
          {agent.isAtCapacity ? 'No assignment slots remaining' : `${agent.remainingCapacity} ${slotLabel} remaining`}
        </p>
        <div className="workload-card__message-slot">
          {agent.capacityState === 'Over Capacity' && <p className="workload-card__warning">Existing workload exceeds the current limit.</p>}
        </div>
      </div>
      <div className="workload-card__metrics">
        {[['Assigned', agent.assigned], ['In Progress', agent.inProgress], ['Pending', agent.pending]].map(([status, count]) =>
          ticketPath ? (
            <Link className="workload-card__metric" to={`${ticketPath}?status=${encodeURIComponent(status)}`} key={status} aria-label={`View ${status} tickets for ${agent.name}`}>
              <span className="workload-card__metric-label">{status}</span><strong className="workload-card__metric-value">{count}</strong>
            </Link>
          ) : (
            <div className="workload-card__metric" key={status}><span className="workload-card__metric-label">{status}</span><strong className="workload-card__metric-value">{count}</strong></div>
          ),
        )}
      </div>
      {ticketPath && <footer className="workload-card__footer"><Link className="table-action" to={ticketPath}>View tickets</Link></footer>}
    </article>
  )
}
