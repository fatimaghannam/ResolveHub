import { useEffect, useState } from 'react'
import { EmptyState, ErrorState, LoadingState } from '../../components/common/States.jsx'
import { AgentWorkloadCard } from '../../components/tickets/AgentWorkload.jsx'
import { getManagerWorkload } from '../../services/managerService.js'
import { getAdminWorkload } from '../../services/adminService.js'

function ManagerWorkloadPage({ roleArea = 'manager' }) {
  const [agents, setAgents] = useState(null)
  const [error, setError] = useState('')
  useEffect(() => {
    const controller = new AbortController()
    const loadWorkload = roleArea === 'admin' ? getAdminWorkload : getManagerWorkload
    loadWorkload(controller.signal)
      .then((result) => { if (!controller.signal.aborted) setAgents(result) })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setError(requestError.message)
      })
    return () => controller.abort()
  }, [roleArea])
  return <>
    <section className="page-heading"><h2>Team Workload</h2><p>Review current workload and monthly resolution progress for active IT Support Agents.</p></section>
    {error && <ErrorState message={error} />}
    {!error && !agents && <LoadingState message="Loading team workload…" />}
    {agents?.length === 0 && <EmptyState title="No active agents" message="Active IT Support Agents will appear here." />}
    {agents?.length > 0 && <section className="workload-grid">{agents.map((agent) => <AgentWorkloadCard agent={agent} ticketPath={`/${roleArea}/workload/${agent.userId}`} key={agent.userId} />)}</section>}
  </>
}

export default ManagerWorkloadPage
