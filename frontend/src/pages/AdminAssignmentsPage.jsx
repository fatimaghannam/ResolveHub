import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState } from '../components/common/States.jsx'
import { TicketPriorityBadge } from '../components/tickets/TicketBadges.jsx'
import { agentWorkloads, unassignedTickets } from '../data/index.js'
import { formatLocalDate } from '../utils/dateTime.js'

function AdminAssignmentsPage() {
  const [searchParams] = useSearchParams()
  const selectedReference = searchParams.get('ticket')
  const initialSelections = useMemo(() => Object.fromEntries(
    unassignedTickets.map((ticket) => [ticket.ticketReferenceNumber, '']),
  ), [])
  const [selections, setSelections] = useState(initialSelections)
  const [notice, setNotice] = useState('')

  function stageAssignment(ticket) {
    const agent = selections[ticket.ticketReferenceNumber]
    if (!agent) return
    setNotice(`${ticket.ticketReferenceNumber} is staged for ${agent}. Backend persistence is not connected yet.`)
  }

  return (
    <>
      <section className="page-heading"><h2>Ticket Assignments</h2><p>Assign unassigned requests and review current IT Agent workloads.</p></section>
      {notice && <div className="inline-notice" role="status">{notice}</div>}
      <section className="panel dashboard-section">
        <div className="panel__heading"><div><h2>Unassigned Tickets</h2><p>Select an available IT Support Agent for each unassigned ticket.</p></div></div>
        {unassignedTickets.length === 0 ? <EmptyState title="No unassigned tickets" message="All current tickets have an assigned agent." /> : <div className="table-scroll"><table className="ticket-table admin-assignment-table admin-assignments-page-table">
          <colgroup>
            <col className="assignments-col--number" />
            <col className="assignments-col--title" />
            <col className="assignments-col--requester" />
            <col className="assignments-col--priority" />
            <col className="assignments-col--created" />
            <col className="assignments-col--agent" />
            <col className="assignments-col--action" />
          </colgroup>
          <thead><tr><th>Ticket Number</th><th>Title</th><th>Requester</th><th>Priority</th><th>Created</th><th>Assign To</th><th>Action</th></tr></thead>
          <tbody>{unassignedTickets.map((ticket) => <tr className={selectedReference === ticket.ticketReferenceNumber ? 'table-row--highlighted' : ''} key={ticket.id}>
            <td className="assignments-ticket-number"><strong>{ticket.ticketReferenceNumber}</strong></td>
            <td><span className="assignments-ticket-title" title={ticket.title}>{ticket.title}</span></td>
            <td className="assignments-requester" title={ticket.requesterName}>{ticket.requesterName}</td>
            <td><TicketPriorityBadge value={ticket.priorityName} /></td>
            <td className="assignments-created">{formatLocalDate(ticket.createdDate)}</td>
            <td><label className="sr-only" htmlFor={`agent-${ticket.id}`}>Assign {ticket.ticketReferenceNumber} to</label><select className="assignment-agent-select" id={`agent-${ticket.id}`} value={selections[ticket.ticketReferenceNumber]} onChange={(event) => setSelections({ ...selections, [ticket.ticketReferenceNumber]: event.target.value })}><option value="">Select agent</option>{agentWorkloads.map((agent) => <option key={agent.name}>{agent.name}</option>)}</select></td>
            <td><button className="button button--primary button--compact assignment-submit" type="button" disabled={!selections[ticket.ticketReferenceNumber]} onClick={() => stageAssignment(ticket)}>Assign</button></td>
          </tr>)}</tbody>
        </table></div>}
      </section>
      <section className="panel">
        <div className="panel__heading"><div><h2>IT Agent Workload</h2></div></div>
        <div className="workload-grid">{agentWorkloads.map((agent) => <article className="workload-card" key={agent.name}><h3>{agent.name}</h3><dl><div><dt>Active Assigned</dt><dd>{agent.activeAssigned}</dd></div><div><dt>In Progress</dt><dd>{agent.inProgress}</dd></div><div><dt>Pending</dt><dd>{agent.pending}</dd></div></dl><span className={`capacity-badge capacity-badge--${agent.capacity.toLowerCase().replace(' ', '-')}`}>{agent.capacity}</span></article>)}</div>
      </section>
    </>
  )
}

export default AdminAssignmentsPage
