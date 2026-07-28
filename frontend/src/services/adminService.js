import { apiRequest } from './apiClient.js'

export const getAdminAssignments = (signal) =>
  apiRequest('/api/admin/ticket-assignments', { signal })

export const assignAdminTicket = (ticketReference, agentUserId) =>
  apiRequest(`/api/admin/tickets/${encodeURIComponent(ticketReference)}/assign`, {
    method: 'POST',
    body: JSON.stringify({ agentUserId }),
  })

export const getAdminDashboard = (signal) =>
  apiRequest('/api/admin/dashboard', { signal })
