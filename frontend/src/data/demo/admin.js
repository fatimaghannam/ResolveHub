import { ticketCategories } from '../shared/ticketLookups.js'
import { ticketMockData } from './tickets.js'
import {
  getMockUserName,
  mockItAgents,
  usersMockData,
} from './users.js'

// Temporary frontend data derived from shared company records until Admin APIs are implemented.
export const adminStatistics = {
  totalUsers: usersMockData.length,
  totalTickets: ticketMockData.length,
  openTickets: ticketMockData.filter((ticket) => ticket.statusName === 'Open').length,
  inProgress: ticketMockData.filter((ticket) => ticket.statusName === 'In Progress').length,
  unassignedTickets: ticketMockData.filter((ticket) => !ticket.assignedAgentId).length,
  resolvedThisMonth: ticketMockData.filter((ticket) => ticket.statusName === 'Resolved').length,
}

export const ticketStatusChartData = [
  { name: 'Open', value: adminStatistics.openTickets, color: '#1769c2' },
  { name: 'Assigned', value: ticketMockData.filter((ticket) => ticket.statusName === 'Assigned').length, color: '#6f42a6' },
  { name: 'In Progress', value: adminStatistics.inProgress, color: '#d17a00' },
  { name: 'Pending', value: ticketMockData.filter((ticket) => ticket.statusName === 'Pending').length, color: '#087b8c' },
  { name: 'Resolved', value: adminStatistics.resolvedThisMonth, color: '#18794e' },
]

export const monthlyTicketTrend = [
  { month: 'Feb', created: 42, resolved: 35 },
  { month: 'Mar', created: 48, resolved: 40 },
  { month: 'Apr', created: 55, resolved: 47 },
  { month: 'May', created: 61, resolved: 52 },
  { month: 'Jun', created: 73, resolved: 59 },
  { month: 'Jul', created: 68, resolved: adminStatistics.resolvedThisMonth },
]

export const ticketsByCategory = ticketCategories
  .map((category) => ({
    category,
    tickets: ticketMockData.filter((ticket) => ticket.categoryName === category).length,
  }))
  .sort((first, second) => second.tickets - first.tickets)

export const adminActivity = [
  { id: 1, actionType: 'Ticket Created', user: getMockUserName(11), action: `${getMockUserName(11)} created RH-2026-1072`, entity: 'RH-2026-1072', details: 'New network support request', timestamp: '2026-07-27T09:40:00Z' },
  { id: 2, actionType: 'Ticket Assigned', user: getMockUserName(1), action: `${getMockUserName(1)} assigned RH-2026-1070 to ${getMockUserName(6)}`, entity: 'RH-2026-1070', details: 'Assignment updated', timestamp: '2026-07-27T09:05:00Z' },
  { id: 3, actionType: 'Status Changed', user: getMockUserName(6), action: `${getMockUserName(6)} changed RH-2026-1068 from Assigned to In Progress`, entity: 'RH-2026-1068', details: 'Ticket status updated', timestamp: '2026-07-27T08:35:00Z' },
  { id: 4, actionType: 'User Created', user: getMockUserName(1), action: `${getMockUserName(1)} added ${getMockUserName(21)} as an Employee`, entity: 'User account', details: 'Employee account created', timestamp: '2026-07-26T16:20:00Z' },
  { id: 5, actionType: 'Ticket Resolved', user: getMockUserName(8), action: `${getMockUserName(8)} resolved RH-2026-1059`, entity: 'RH-2026-1059', details: 'Resolution recorded', timestamp: '2026-07-26T14:10:00Z' },
  { id: 6, actionType: 'User Deactivated', user: getMockUserName(1), action: `${getMockUserName(18)} was deactivated`, entity: 'User account', details: 'Account status changed to inactive', timestamp: '2026-07-26T11:45:00Z' },
]

export const userOverview = {
  Employees: usersMockData.filter((user) => user.role === 'Employee').length,
  'IT Support Agents': mockItAgents.length,
  Managers: usersMockData.filter((user) => user.role === 'Manager').length,
  Administrators: usersMockData.filter((user) => user.role === 'Administrator').length,
  'Active Users': usersMockData.filter((user) => user.status === 'Active').length,
  'Inactive Users': usersMockData.filter((user) => user.status === 'Inactive').length,
}

export const agentWorkloads = mockItAgents.map((agent) => {
  const assigned = ticketMockData.filter((ticket) => ticket.assignedAgentId === agent.id)
  const activeAssigned = assigned.filter((ticket) => ticket.statusName !== 'Resolved').length

  return {
    userId: agent.id,
    name: getMockUserName(agent.id),
    activeAssigned,
    inProgress: assigned.filter((ticket) => ticket.statusName === 'In Progress').length,
    pending: assigned.filter((ticket) => ticket.statusName === 'Pending').length,
    capacity: activeAssigned >= 3
      ? 'High Workload'
      : activeAssigned >= 2
        ? 'Balanced'
        : 'Available',
  }
})

export const categoryData = ticketCategories.map((name, index) => ({
  id: index + 1,
  name,
  description: `${name} support requests and related incidents.`,
  activeTickets: ticketsByCategory.find((item) => item.category === name)?.tickets ?? 0,
  status: 'Active',
}))

export const adminNotifications = [
  { id: 1, title: 'Critical ticket awaiting assignment', message: 'RH-2026-1072 requires an IT Support Agent.', timestamp: '2026-07-27T08:20:00Z' },
  { id: 2, title: 'Account status changed', message: `${getMockUserName(18)} was deactivated.`, timestamp: '2026-07-26T11:45:00Z' },
  { id: 3, title: 'Ticket resolved', message: `${getMockUserName(6)} resolved RH-2026-1054.`, timestamp: '2026-07-26T14:10:00Z' },
  { id: 4, title: 'Employee account created', message: `${getMockUserName(1)} added ${getMockUserName(21)}.`, timestamp: '2026-07-25T13:30:00Z' },
]

export const unassignedTickets = ticketMockData.filter(
  (ticket) => !ticket.assignedAgentId,
)
