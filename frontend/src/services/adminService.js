import { apiRequest } from './apiClient.js'
import { toQueryString } from './apiClient.js'

export const getAdminAssignments = (signal) =>
  apiRequest('/api/admin/ticket-assignments', { signal })

export const assignAdminTicket = (ticketReference, agentUserId) =>
  apiRequest(`/api/admin/tickets/${encodeURIComponent(ticketReference)}/assign`, {
    method: 'POST',
    body: JSON.stringify({ agentUserId }),
  })

export const getAdminDashboard = (signal) =>
  apiRequest('/api/admin/dashboard', { signal })

export const getAdminTickets = (filters, signal) =>
  apiRequest(`/api/admin/tickets${toQueryString(filters)}`, { signal })

export const getAdminTicket = (ticketReference, signal) =>
  apiRequest(`/api/admin/tickets/${encodeURIComponent(ticketReference)}`, { signal })

export const updateAdminTicketAssignment = (ticketReference, agentUserId) =>
  apiRequest(`/api/admin/tickets/${encodeURIComponent(ticketReference)}/assignment`, {
    method: 'PUT',
    body: JSON.stringify({ agentUserId }),
  })
