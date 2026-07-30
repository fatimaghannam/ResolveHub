import { ticketCategories } from '../shared/ticketLookups.js'

// Production contains no fictional business records. These collections will be
// replaced by API-backed repositories as Administrator endpoints are connected.
export const usersMockData = []
export const ticketMockData = []
export const adminActivity = []
export const adminNotifications = []
export const unassignedTickets = []

export const adminStatistics = {
  totalUsers: 0,
  totalTickets: 0,
  openTickets: 0,
  inProgress: 0,
  unassignedTickets: 0,
  resolvedThisMonth: 0,
}

export const ticketStatusChartData = [
  { name: 'Open', value: 0, color: '#1769c2' },
  { name: 'Assigned', value: 0, color: '#6f42a6' },
  { name: 'In Progress', value: 0, color: '#d17a00' },
  { name: 'Pending', value: 0, color: '#087b8c' },
  { name: 'Resolved', value: 0, color: '#18794e' },
]

export const monthlyTicketTrend = []
export const ticketsByCategory = ticketCategories.map((category) => ({
  category,
  tickets: 0,
}))
export const userOverview = {
  Employees: 0,
  'IT Support Agents': 0,
  Managers: 0,
  Administrators: 0,
  'Active Users': 0,
  'Inactive Users': 0,
}
export const categoryData = ticketCategories.map((name, index) => ({
  id: index + 1,
  name,
  description: `${name} support requests and related incidents.`,
  activeTickets: 0,
  status: 'Active',
}))
