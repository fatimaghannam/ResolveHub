import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import { getManagerWorkload } from '../services/managerService.js'

function ManagerWorkloadPage() {
  const [agents, setAgents] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => {
    const controller = new AbortController()
    getManagerWorkload(controller.signal)
      .then((result) => { if (!controller.signal.aborted) setAgents(result) })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setError(requestError.message)
      })
    return () => controller.abort()
  }, [])
  return <>
    <section className="page-heading"><h2>Team Workload</h2><p>Review current workload and monthly resolution progress for active IT Support Agents.</p></section>
    {error && <ErrorState message={error} />}
    {!error && !agents && <LoadingState message="Loading team workload…" />}
    {agents && <section className="workload-grid">{agents.map((agent) => <article className="workload-card" key={agent.userId}><h3>{agent.name}</h3><small>{agent.email}</small><dl><div><dt>Active assigned</dt><dd>{agent.activeAssigned}</dd></div><div><dt>In progress</dt><dd>{agent.inProgress}</dd></div><div><dt>Open</dt><dd>{agent.open}</dd></div><div><dt>Resolved this month</dt><dd>{agent.resolvedThisMonth}</dd></div><div><dt>Critical assigned</dt><dd>{agent.criticalAssigned}</dd></div></dl><div className="workload-card__footer"><span className={`capacity-badge capacity-badge--${agent.capacity.toLowerCase().replace(' ', '-')}`}>{agent.capacity}</span><Link className="table-action" to={`/manager/tickets?agent=${agent.userId}`}>View tickets</Link></div></article>)}</section>}
  </>
}

export default ManagerWorkloadPage
