import { ticketCategories } from '../shared/ticketLookups.js'
import { getMockUserName } from './users.js'

export const adminActivity = [
  { id: 4, actionType: 'User Created', user: getMockUserName(1), action: `${getMockUserName(1)} added ${getMockUserName(21)} as an Employee`, entity: 'User account', details: 'Employee account created', timestamp: '2026-07-26T16:20:00Z' },
  { id: 6, actionType: 'User Deactivated', user: getMockUserName(1), action: `${getMockUserName(18)} was deactivated`, entity: 'User account', details: 'Account status changed to inactive', timestamp: '2026-07-26T11:45:00Z' },
]

export const categoryData = ticketCategories.map((name, index) => ({
  id: index + 1,
  name,
  description: `${name} support requests and related incidents.`,
  activeTickets: 0,
  status: 'Active',
}))
