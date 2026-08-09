import { ArrowLeft } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../../components/common/States.jsx'
import { TicketPriorityBadge, TicketStatusBadge } from '../../components/tickets/TicketBadges.jsx'
import { getAdminTickets, getAdminWorkload } from '../../services/adminService.js'
import { getManagerTickets, getManagerWorkload } from '../../services/managerService.js'
import { getStatuses } from '../../services/ticketService.js'
import { formatLocalDateTime } from '../../utils/dateTime.js'

const workloadStatuses = new Set(['Assigned', 'In Progress', 'Pending'])

function AgentWorkloadTicketsPage({ roleArea }) {
  const { agentId } = useParams()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  const requestedStatus = searchParams.get('status')
  const status = workloadStatuses.has(requestedStatus) ? requestedStatus : null
  const [agent, setAgent] = useState(null)
  const [tickets, setTickets] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    const loadWorkload = roleArea === 'admin' ? getAdminWorkload : getManagerWorkload
    const loadTickets = roleArea === 'admin' ? getAdminTickets : getManagerTickets
    setError('')
    setAgent(null)
    setTickets(null)
    Promise.all([loadWorkload(controller.signal), getStatuses(controller.signal)])
      .then(async ([agents, statuses]) => {
        const selectedAgent = agents.find((item) => String(item.userId) === agentId)
        if (!selectedAgent) {
          setAgent(false)
          setTickets([])
          return
        }
        const selectedStatus = status
          ? statuses.find((item) => item.name === status)
          : null
        const result = await loadTickets({
          agentUserId: selectedAgent.userId,
          statusId: selectedStatus?.id,
          activeWorkloadOnly: status ? undefined : true,
          page: 1,
          pageSize: 100,
        }, controller.signal)
        if (!controller.signal.aborted) {
          setAgent(selectedAgent)
          setTickets(result.items)
        }
      })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setError(requestError.message)
      })
    return () => controller.abort()
  }, [agentId, roleArea, status])

  const viewLabel = status ? `${status} Tickets` : 'Active Tickets'
  const agentName = agent?.name ?? ''
  const emptyStatus = (status ?? 'active').toLowerCase()
  const fromAdminAssignments = roleArea === 'admin' &&
    location.state?.from === 'admin-assignments-workload'
  const backTarget = fromAdminAssignments
    ? '/admin/assignments#it-agent-workload'
    : `/${roleArea}/workload`
  const backLabel = fromAdminAssignments ? 'Back to IT Agent Workload' : 'Back to Team Workload'
  const ticketDetailsState = {
    from: 'agent-workload-tickets',
    backTo: `${location.pathname}${location.search}`,
    origin: fromAdminAssignments ? 'admin-assignments-workload' : undefined,
  }

  return <>
    <Link className="back-link back-link--top" to={backTarget}><ArrowLeft size={18} />{backLabel}</Link>
    <section className="page-heading"><h2>{agentName ? `${agentName} — ${viewLabel}` : viewLabel}</h2><p>Review this agent&apos;s current workload tickets.</p></section>
    {error && <ErrorState message={error} />}
    {!error && agent === null && <LoadingState message="Loading agent workload…" />}
    {!error && agent === false && <EmptyState title="Agent not found" message="This IT Agent is not available in Team Workload." />}
    {agent && tickets?.length === 0 && <EmptyState title={`No ${emptyStatus} tickets for ${agentName}.`} message="No tickets match this workload view." />}
    {agent && tickets?.length > 0 && <section className="panel">
      <div className="results-count">{tickets.length} ticket{tickets.length === 1 ? '' : 's'}</div>
      <div className="table-scroll workload-ticket-table-wrap"><table className="ticket-table workload-ticket-table">
        <thead><tr><th>Ticket Number</th><th>Title</th><th>Requester</th><th>Priority</th><th>Status</th><th>Created</th><th>Action</th></tr></thead>
        <tbody>{tickets.map((ticket) => <tr key={ticket.id}>
          <td><strong>{ticket.ticketReferenceNumber}</strong></td>
          <td><span className="admin-ticket-title" title={ticket.title}>{ticket.title}</span></td>
          <td>{ticket.requesterName}</td>
          <td><TicketPriorityBadge value={ticket.priorityName} /></td>
          <td><TicketStatusBadge value={ticket.statusName} /></td>
          <td>{formatLocalDateTime(ticket.createdDate)}</td>
          <td><Link className="table-action" to={`/${roleArea}/tickets/${ticket.ticketReferenceNumber}`} state={ticketDetailsState}>View</Link></td>
        </tr>)}</tbody>
      </table></div>
    </section>}
  </>
}

export default AgentWorkloadTicketsPage
