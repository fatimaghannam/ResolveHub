import * as demoData from './demo/index.js'
import * as productionData from './production/index.js'
import { ticketCategories } from './shared/ticketLookups.js'

const requestedMode = import.meta.env.VITE_DATA_MODE?.trim().toLowerCase()

export const dataMode = requestedMode === 'demo'
  ? 'demo'
  : requestedMode === 'production'
    ? 'production'
    : import.meta.env.DEV
      ? 'demo'
      : 'production'

const source = dataMode === 'demo' ? demoData : productionData

export const {
  adminActivity,
  adminNotifications,
  adminStatistics,
  categoryData,
  monthlyTicketTrend,
  ticketsByCategory,
  ticketMockData,
  ticketStatusChartData,
  unassignedTickets,
  userOverview,
  usersMockData,
} = source

export { ticketCategories }
