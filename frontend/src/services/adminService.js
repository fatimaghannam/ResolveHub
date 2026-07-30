import { apiRequest } from './apiClient.js'
import { toQueryString } from './apiClient.js'

export const getAdminAssignments = (filters = {}, signal) =>
  apiRequest(`/api/admin/ticket-assignments${toQueryString(filters)}`, { signal })

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

export const removeAdminDuplicateTicket = (
  ticketReference,
  originalTicketReference,
) =>
  apiRequest(
    `/api/admin/tickets/${encodeURIComponent(ticketReference)}/remove-duplicate`,
    {
      method: 'POST',
      body: JSON.stringify({
        originalTicketReference,
        confirmed: true,
      }),
    },
  )

export const getAdminUsers = (signal) =>
  apiRequest('/api/admin/users', { signal })

export const updateAdminUserStatus = (userId, isActive) =>
  apiRequest(`/api/admin/users/${userId}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ isActive }),
  })
