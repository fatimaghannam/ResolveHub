import {
  CircleCheckBig,
  Clock3,
  ListChecks,
  Ticket,
  UserRoundMinus,
  Users,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import {
  TicketCategoryChart,
  TicketStatusChart,
  TicketTrendChart,
} from '../../components/admin/AdminDashboardCharts.jsx'
import { AgentWorkloadSummary } from '../../components/tickets/AgentWorkload.jsx'
import { ErrorState, LoadingState } from '../../components/common/States.jsx'
import { getAdminDashboard } from '../../services/adminService.js'
import { DashboardReportButton } from '../../components/reports/DashboardReportDialog.jsx'

const statistics = [
  ['Total Users', 'totalUsers', Users, 'blue'],
  ['Total Tickets', 'totalTickets', Ticket, 'cyan'],
  ['Assigned Tickets', 'assignedTickets', ListChecks, 'blue'],
  ['In Progress', 'inProgress', Clock3, 'blue'],
  ['Unassigned Tickets', 'unassignedTickets', UserRoundMinus, 'red'],
  ['Resolved This Month', 'resolvedThisMonth', CircleCheckBig, 'green'],
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
      <section className="page-heading page-heading--compact page-heading--action">
        <div><h2>Welcome back, {user?.firstName ?? 'Administrator'}</h2>
        <p>Monitor system performance and manage help desk operations from one place.</p></div>
        <DashboardReportButton />
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

      </>}
    </>
  )
}

export default AdminDashboardPage
