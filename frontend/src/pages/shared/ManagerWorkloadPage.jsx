import { useEffect, useMemo, useState } from 'react'
import { EmptyState, ErrorState, LoadingState } from '../../components/common/States.jsx'
import { AgentWorkloadCard } from '../../components/tickets/AgentWorkload.jsx'
import { getManagerWorkload } from '../../services/managerService.js'
import { getAdminWorkload } from '../../services/adminService.js'

const initialFilters = {
  search: '',
  capacityState: '',
  workload: '',
  sortBy: 'name-asc',
}

function ManagerWorkloadPage({ roleArea = 'manager' }) {
  const [agents, setAgents] = useState(null)
  const [error, setError] = useState('')
  const [filters, setFilters] = useState(initialFilters)
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

  const filteredAgents = useMemo(() => {
    if (!agents) return []
    const search = filters.search.trim().toLowerCase()
    return agents.filter((agent) => {
      const matchesSearch = !search ||
        agent.name.toLowerCase().includes(search) ||
        (agent.email ?? '').toLowerCase().includes(search)
      const matchesCapacity = !filters.capacityState ||
        agent.capacityState === filters.capacityState
      const matchesWorkload = !filters.workload ||
        (filters.workload === 'none' && agent.activeTicketCount === 0) ||
        (filters.workload === 'light' && agent.activeTicketCount >= 1 && agent.activeTicketCount <= 2) ||
        (filters.workload === 'moderate' && agent.activeTicketCount >= 3 && agent.activeTicketCount <= 4) ||
        (filters.workload === 'full' && agent.activeTicketCount === agent.maxActiveTickets)
      return matchesSearch && matchesCapacity && matchesWorkload
    }).toSorted((left, right) => {
      switch (filters.sortBy) {
        case 'name-desc':
          return right.name.localeCompare(left.name) || left.userId - right.userId
        case 'workload-asc':
          return left.activeTicketCount - right.activeTicketCount ||
            left.name.localeCompare(right.name) || left.userId - right.userId
        case 'workload-desc':
          return right.activeTicketCount - left.activeTicketCount ||
            left.name.localeCompare(right.name) || left.userId - right.userId
        default:
          return left.name.localeCompare(right.name) || left.userId - right.userId
      }
    })
  }, [agents, filters])

  return <>
    <section className="page-heading"><h2>Team Workload</h2><p>Review current workload and monthly resolution progress for active IT Support Agents.</p></section>
    {error && <ErrorState message={error} />}
    {!error && !agents && <LoadingState message="Loading team workload…" />}
    {agents?.length === 0 && <EmptyState title="No IT agents" message="No IT agents are currently available in the system." />}
    {agents?.length > 0 && <>
      <div className="filter-panel team-workload-filters">
        <label><span>Search</span><input value={filters.search} onChange={(event) => setFilters({ ...filters, search: event.target.value })} placeholder="Search agent by name or email" /></label>
        <label><span>Capacity Status</span><select value={filters.capacityState} onChange={(event) => setFilters({ ...filters, capacityState: event.target.value })}><option value="">All statuses</option><option>Available</option><option>Near Capacity</option><option>Full</option></select></label>
        <label><span>Workload</span><select value={filters.workload} onChange={(event) => setFilters({ ...filters, workload: event.target.value })}><option value="">All workloads</option><option value="none">No active tickets</option><option value="light">Light — 1–2 active tickets</option><option value="moderate">Moderate — 3–4 active tickets</option><option value="full">Full — 5 active tickets</option></select></label>
        <label><span>Sort By</span><select value={filters.sortBy} onChange={(event) => setFilters({ ...filters, sortBy: event.target.value })}><option value="name-asc">Name A–Z</option><option value="name-desc">Name Z–A</option><option value="workload-asc">Lowest workload</option><option value="workload-desc">Highest workload</option></select></label>
        <button className="button button--secondary button--compact" type="button" onClick={() => setFilters(initialFilters)}>Reset Filters</button>
      </div>
      <p className="results-count">Showing {filteredAgents.length} of {agents.length} agents</p>
      {filteredAgents.length === 0
        ? <EmptyState title="No agents match the selected filters." message="Try adjusting your filters or reset them to view the full team." action={<button className="button button--secondary button--compact" type="button" onClick={() => setFilters(initialFilters)}>Reset Filters</button>} />
        : <section className="workload-grid">{filteredAgents.map((agent) => <AgentWorkloadCard agent={agent} ticketPath={`/${roleArea}/workload/${agent.userId}`} key={agent.userId} />)}</section>}
    </>}
  </>
}

export default ManagerWorkloadPage
