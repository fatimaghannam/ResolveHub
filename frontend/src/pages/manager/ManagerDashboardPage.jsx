import {
  AlertTriangle,
  CircleCheckBig,
  Clock3,
  ListChecks,
  Ticket,
  UserRoundMinus,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { TicketStatusChart } from '../../components/admin/AdminDashboardCharts.jsx'
import { ErrorState, LoadingState } from '../../components/common/States.jsx'
import { TicketPriorityBadge } from '../../components/tickets/TicketBadges.jsx'
import { AgentWorkloadSummary } from '../../components/tickets/AgentWorkload.jsx'
import { getManagerDashboard } from '../../services/managerService.js'
import { formatLocalDateTime } from '../../utils/dateTime.js'
import { DashboardReportButton } from '../../components/reports/DashboardReportDialog.jsx'

const statistics = [
  ['Total Tickets', 'totalTickets', Ticket, 'cyan'],
  ['Assigned Tickets', 'assignedTickets', ListChecks, 'blue'],
  ['In Progress', 'inProgressTickets', Clock3, 'blue'],
  ['Unassigned', 'unassignedTickets', UserRoundMinus, 'red'],
  ['Resolved This Month', 'resolvedThisMonth', CircleCheckBig, 'green'],
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
      <section className="page-heading page-heading--compact page-heading--action">
        <div><h2>Welcome back, {user?.firstName ?? 'Manager'}</h2>
        <p>Track support operations, team capacity, and tickets requiring attention.</p></div>
        <DashboardReportButton />
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
            {data.unassigned.map((ticket) => <tr key={ticket.id}><td><strong>{ticket.ticketReferenceNumber}</strong></td><td><span className="assignment-title" title={ticket.title}>{ticket.title}</span></td><td>{ticket.requesterName}</td><td><TicketPriorityBadge value={ticket.priorityName} /></td><td>{formatLocalDateTime(ticket.createdDate)}</td><td><Link className="table-action" to={`/manager/assignments?ticket=${ticket.ticketReferenceNumber}`}>Assign</Link></td></tr>)}
          </tbody></table></div>
        </section>
        <section className="panel dashboard-section">
  <div className="panel__heading">
    <div>
      <h2>Agent Workload</h2>
      <p>Current active assignments by agent.</p>
    </div>

    <Link to="/manager/workload">View team</Link>
  </div>

  <div className="dashboard-workload-list">
    {data.agentWorkloads.map((agent) => (
      <AgentWorkloadSummary agent={agent} key={agent.userId} />
    ))}
  </div>
</section>
      </>}
    </>
  )
}

export default ManagerDashboardPage
