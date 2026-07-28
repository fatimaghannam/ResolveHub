import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge } from '../components/tickets/TicketBadges.jsx'
import { assignAdminTicket, getAdminAssignments } from '../services/adminService.js'
import { formatLocalDate } from '../utils/dateTime.js'

function AdminAssignmentsPage() {
  const [searchParams] = useSearchParams()
  const selectedReference = searchParams.get('ticket')
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)
  const [selections, setSelections] = useState({})
  const [notice, setNotice] = useState('')
  const [assigning, setAssigning] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    getAdminAssignments(controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setData(result)
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) {
          setError(requestError.message)
        }
      })
    return () => controller.abort()
  }, [reload])

  async function assignTicket(ticket) {
    const agentUserId = Number(selections[ticket.ticketReferenceNumber])
    if (!agentUserId || assigning) return
    const agent = data.agentWorkloads.find((item) => item.userId === agentUserId)
    try {
      setAssigning(ticket.ticketReferenceNumber)
      setNotice('')
      await assignAdminTicket(ticket.ticketReferenceNumber, agentUserId)
      setNotice(`${ticket.ticketReferenceNumber} was assigned to ${agent.name}.`)
      setReload((value) => value + 1)
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setAssigning('')
    }
  }

  const unassignedTickets = data?.unassignedTickets ?? []
  const agentWorkloads = data?.agentWorkloads ?? []

  return (
    <>
      <section className="page-heading"><h2>Ticket Assignments</h2><p>Assign unassigned requests and review current IT Agent workloads.</p></section>
      {notice && <div className="inline-notice" role="status">{notice}</div>}
      {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
      {!error && !data && <LoadingState message="Loading ticket assignments…" />}
      {data && <>
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
            <td><label className="sr-only" htmlFor={`agent-${ticket.id}`}>Assign {ticket.ticketReferenceNumber} to</label><select className="assignment-agent-select" id={`agent-${ticket.id}`} value={selections[ticket.ticketReferenceNumber] ?? ''} onChange={(event) => setSelections({ ...selections, [ticket.ticketReferenceNumber]: event.target.value })}><option value="">Select agent</option>{agentWorkloads.map((agent) => <option value={agent.userId} key={agent.userId}>{agent.name}</option>)}</select></td>
            <td><button className="button button--primary button--compact assignment-submit" type="button" disabled={!selections[ticket.ticketReferenceNumber] || Boolean(assigning)} onClick={() => assignTicket(ticket)}>{assigning === ticket.ticketReferenceNumber ? 'Assigning…' : 'Assign'}</button></td>
          </tr>)}</tbody>
        </table></div>}
      </section>
      </>}
      <section className="panel">
        <div className="panel__heading"><div><h2>IT Agent Workload</h2></div></div>
        <div className="workload-grid">{agentWorkloads.map((agent) => <article className="workload-card" key={agent.name}><h3>{agent.name}</h3><dl><div><dt>Active Assigned</dt><dd>{agent.activeAssigned}</dd></div><div><dt>In Progress</dt><dd>{agent.inProgress}</dd></div><div><dt>Pending</dt><dd>{agent.pending}</dd></div></dl><span className={`capacity-badge capacity-badge--${agent.capacity.toLowerCase().replace(' ', '-')}`}>{agent.capacity}</span></article>)}</div>
      </section>
    </>
  )
}

export default AdminAssignmentsPage
