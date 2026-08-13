import { ticketCategories } from '../shared/ticketLookups.js'

export const usersMockData = []
export const adminActivity = []
export const categoryData = ticketCategories.map((name, index) => ({
  id: index + 1,
  name,
  description: `${name} support requests and related incidents.`,
  activeTickets: 0,
  status: 'Active',
}))
