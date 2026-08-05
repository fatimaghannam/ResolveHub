import {
  CircleDot,
  ClipboardCheck,
  Clock3,
  FilePlus2,
  ListChecks,
  Tags,
  Ticket,
  UserPlus,
  UserRoundCheck,
  Users,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import {
  TicketCategoryChart,
  TicketStatusChart,
  TicketTrendChart,
} from '../components/admin/AdminDashboardCharts.jsx'
import { TicketPriorityBadge } from '../components/tickets/TicketBadges.jsx'
import { AgentWorkloadSummary } from '../components/tickets/AgentWorkload.jsx'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import { getAdminDashboard } from '../services/adminService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'

const statistics = [
  ['Total Users', 'totalUsers', Users, 'blue'],
  ['Total Tickets', 'totalTickets', Ticket, 'cyan'],
  ['Open Tickets', 'openTickets', CircleDot, 'amber'],
  ['In Progress', 'inProgress', Clock3, 'blue'],
  ['Unassigned Tickets', 'unassignedTickets', ListChecks, 'red'],
  ['Resolved This Month', 'resolvedThisMonth', UserRoundCheck, 'green'],
]

const quickActions = [
  { title: 'Create Ticket', description: 'Submit a new support request for the organization.', to: '/admin/tickets/create', icon: FilePlus2 },
  { title: 'Add User', description: 'Create a new employee or IT Support Agent.', to: '/admin/users', icon: UserPlus },
  { title: 'Assign Tickets', description: 'Review and assign unassigned support requests.', to: '/admin/assignments', icon: ClipboardCheck },
  { title: 'Manage Categories', description: 'Update ticket categories and classifications.', to: '/admin/categories', icon: Tags },
]

function AdminDashboardPage() {
  const { user } = useOutletContext()
  const [data, setData] = useState(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    getAdminDashboard(controller.signal)
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

  return (
    <>
      <section className="page-heading page-heading--compact">
        <h2>Welcome back, {user?.firstName ?? 'Administrator'}</h2>
        <p>Monitor system performance and manage help desk operations from one place.</p>
      </section>

      {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
      {!error && !data && <LoadingState message="Loading Administrator dashboard…" />}
      {data && <>
      <section className="stat-grid stat-grid--six" aria-label="Administrative statistics">
        {statistics.map(([label, field, Icon, tone]) => (
          <article className="stat-card" key={field}>
            <span className={`stat-card__icon tone-${tone}`}><Icon size={22} aria-hidden="true" /></span>
            <span><small>{label}</small><strong>{data[field]}</strong></span>
          </article>
        ))}
      </section>

      <div className="admin-chart-grid">
        <TicketStatusChart data={data.ticketCountsByStatus} totalTickets={data.totalTickets} />
        <TicketTrendChart data={data.monthlyTrend} />
      </div>

      <div className="admin-chart-grid admin-chart-grid--secondary">
        <TicketCategoryChart data={data.ticketsByCategory} />
        <section className="panel chart-panel">
          <div className="chart-heading"><h2>IT Agent Workload</h2><p>Current active workload and capacity status by support agent.</p></div>
          <div className="dashboard-workload-list">
            {data.agentWorkloads.map((agent) => (
              <AgentWorkloadSummary agent={agent} key={agent.userId} />
            ))}
          </div>
        </section>
      </div>

      <section className="panel dashboard-section">
        <div className="panel__heading">
          <div>
            <h2>Tickets Requiring Assignment</h2>
            <p>High-priority and recently created tickets waiting for an IT Support Agent.</p>
          </div>
          <Link to="/admin/assignments">View assignments</Link>
        </div>
        <div className="table-scroll">
          <table className="ticket-table admin-assignment-table admin-dashboard-assignment-table">
            <colgroup>
              <col className="assignment-col--number" />
              <col className="assignment-col--title" />
              <col className="assignment-col--requester" />
              <col className="assignment-col--category" />
              <col className="assignment-col--priority" />
              <col className="assignment-col--created" />
              <col className="assignment-col--action" />
            </colgroup>
            <thead><tr><th>Ticket Number</th><th>Title</th><th>Requester</th><th>Category</th><th>Priority</th><th>Created</th><th>Action</th></tr></thead>
            <tbody>{data.ticketsRequiringAssignment.map((ticket) => (
              <tr key={ticket.id}>
                <td><strong>{ticket.ticketReferenceNumber}</strong></td>
                <td><span className="assignment-title" title={ticket.title}>{ticket.title}</span></td><td>{ticket.requesterName}</td><td>{ticket.categoryName}</td>
                <td><TicketPriorityBadge value={ticket.priorityName} /></td>
                <td>{formatLocalDateTime(ticket.createdDate)}</td>
                <td><Link className="button button--secondary button--compact" to={`/admin/assignments?ticket=${ticket.ticketReferenceNumber}`}>Assign</Link></td>
              </tr>
            ))}</tbody>
          </table>
        </div>
      </section>

      <section className="quick-actions-section" aria-labelledby="quick-actions-heading">
        <div className="section-heading">
          <h2 id="quick-actions-heading">Quick Actions</h2>
          <p>Common administrative tasks and operational shortcuts.</p>
        </div>
        <div className="quick-actions-grid">
          {quickActions.map(({ title, description, to, icon: Icon }) => (
            <Link className="quick-action-card" to={to} key={title}>
              <span className="quick-action-card__icon"><Icon size={20} aria-hidden="true" /></span>
              <span><strong>{title}</strong><small>{description}</small></span>
            </Link>
          ))}
        </div>
      </section>
      </>}
    </>
  )
}

export default AdminDashboardPage
