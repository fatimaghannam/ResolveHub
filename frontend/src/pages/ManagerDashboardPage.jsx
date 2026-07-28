import {
  AlertTriangle,
  CircleDot,
  Clock3,
  ListChecks,
  ShieldAlert,
  Ticket,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { TicketStatusChart } from '../components/admin/AdminDashboardCharts.jsx'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import { TicketPriorityBadge } from '../components/tickets/TicketBadges.jsx'
import { getManagerDashboard } from '../services/managerService.js'
import { formatLocalDate } from '../utils/dateTime.js'

const statistics = [
  ['Total Tickets', 'totalTickets', Ticket, 'blue'],
  ['Open Tickets', 'openTickets', CircleDot, 'cyan'],
  ['In Progress', 'inProgressTickets', Clock3, 'amber'],
  ['Unassigned', 'unassignedTickets', ListChecks, 'red'],
  ['Resolved This Month', 'resolvedThisMonth', ShieldAlert, 'green'],
  ['Critical Tickets', 'criticalTickets', AlertTriangle, 'red'],
]

function ManagerDashboardPage() {
  const { user } = useOutletContext()
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    getManagerDashboard(controller.signal)
      .then((result) => { if (!controller.signal.aborted) setData(result) })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) {
          setError(requestError.message)
        }
      })
    return () => controller.abort()
  }, [reload])

  return (
    <>
      <section className="page-heading page-heading--compact">
        <h2>Welcome back, {user?.firstName ?? 'Manager'}</h2>
        <p>Track support operations, team capacity, and tickets requiring attention.</p>
      </section>
      {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
      {!error && !data && <LoadingState message="Loading Manager dashboard…" />}
      {data && <>
        <section className="stat-grid stat-grid--six" aria-label="Manager ticket statistics">
          {statistics.map(([label, field, Icon, tone]) => (
            <article className="stat-card" key={field}>
              <span className={`stat-card__icon tone-${tone}`}><Icon size={22} /></span>
              <span><small>{label}</small><strong>{data[field]}</strong></span>
            </article>
          ))}
        </section>
        <div className="admin-chart-grid">
          <TicketStatusChart data={data.ticketCountsByStatus} totalTickets={data.totalTickets} />
          <section className="panel chart-panel">
            <div className="chart-heading"><h2>Priority Overview</h2><p>Current tickets grouped by business priority.</p></div>
            <div className="manager-priority-overview">
              {data.ticketCountsByPriority.map((item) => (
                <div key={item.name}><TicketPriorityBadge value={item.name} /><strong>{item.value}</strong></div>
              ))}
            </div>
          </section>
        </div>
        <section className="panel dashboard-section">
          <div className="panel__heading"><div><h2>Unassigned Tickets</h2><p>Requests waiting for an active IT Support Agent.</p></div><Link to="/manager/assignments">Manage assignments</Link></div>
          <div className="table-scroll"><table className="ticket-table admin-dashboard-assignment-table"><thead><tr><th>Ticket</th><th>Title</th><th>Requester</th><th>Priority</th><th>Created</th><th>Action</th></tr></thead><tbody>
            {data.unassigned.map((ticket) => <tr key={ticket.id}><td><strong>{ticket.ticketReferenceNumber}</strong></td><td><span className="assignment-title" title={ticket.title}>{ticket.title}</span></td><td>{ticket.requesterName}</td><td><TicketPriorityBadge value={ticket.priorityName} /></td><td>{formatLocalDate(ticket.createdDate)}</td><td><Link className="table-action" to={`/manager/assignments?ticket=${ticket.ticketReferenceNumber}`}>Assign</Link></td></tr>)}
          </tbody></table></div>
        </section>
        <div className="admin-chart-grid">
          <section className="panel">
            <div className="panel__heading"><div><h2>Agent Workload</h2><p>Current active assignments by agent.</p></div><Link to="/manager/workload">View team</Link></div>
            <div className="dashboard-workload-list">{data.agentWorkloads.map((agent) => <article className="dashboard-workload-item" key={agent.userId}><div><strong>{agent.name}</strong><small>{agent.activeAssigned} active · {agent.inProgress} in progress</small></div><span className={`capacity-badge capacity-badge--${agent.capacity.toLowerCase().replace(' ', '-')}`}>{agent.capacity}</span></article>)}</div>
          </section>
          <section className="panel">
            <div className="panel__heading"><div><h2>Recent Ticket Activity</h2><p>Latest operational changes.</p></div><Link to="/manager/activity">View activity</Link></div>
            <div className="manager-activity-list">{data.recentActivity.map((item) => <article key={item.id}><strong>{item.actionType}</strong><span>{item.ticketReferenceNumber} · {item.actorName}</span><small>{formatLocalDate(item.createdDate)}</small></article>)}</div>
          </section>
        </div>
        <section className="panel">
          <div className="panel__heading"><div><h2>Tickets Requiring Attention</h2><p>Unassigned, high-priority, and critical support requests.</p></div><Link to="/manager/tickets">View all tickets</Link></div>
          <div className="priority-list">{data.ticketsRequiringAttention.map((ticket) => <div className="priority-item" key={ticket.id}><div className="priority-item__summary"><strong>{ticket.ticketReferenceNumber}</strong><span>{ticket.title}</span></div><div className="priority-item__badges"><TicketPriorityBadge value={ticket.priorityName} /></div><Link className="table-action" to={`/manager/tickets/${ticket.ticketReferenceNumber}`}>View</Link></div>)}</div>
        </section>
      </>}
    </>
  )
}

export default ManagerDashboardPage
