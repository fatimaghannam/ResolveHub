import { apiRequest } from './apiClient.js'

export const getTicketActivity = (ticketReference, signal) =>
  apiRequest(`/api/tickets/${encodeURIComponent(ticketReference)}/activity`, { signal })

export const getTicketActivitySummary = (ticketReference, signal) =>
  apiRequest(`/api/tickets/${encodeURIComponent(ticketReference)}/activity-summary`, { signal })
