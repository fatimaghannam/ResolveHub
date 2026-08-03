import { ticketCategories } from '../shared/ticketLookups.js'

// Production contains no fictional business records. These collections will be
// replaced by API-backed repositories as Administrator endpoints are connected.
export const usersMockData = []
export const adminActivity = []
export const categoryData = ticketCategories.map((name, index) => ({
  id: index + 1,
  name,
  description: `${name} support requests and related incidents.`,
  activeTickets: 0,
  status: 'Active',
}))
