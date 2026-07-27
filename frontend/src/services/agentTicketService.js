import { apiRequest, toQueryString } from './apiClient.js'

export const getAgentDashboard = (signal) =>
  apiRequest('/api/agent/dashboard', { signal })

export const getAssignedTickets = (filters, signal) =>
  apiRequest(`/api/agent/tickets${toQueryString(filters)}`, { signal })

export const getAgentTicketDetails = (ticketReference, signal) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}`, { signal })

export const updateAgentTicketStatus = (ticketReference, request) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/status`, {
    method: 'PATCH',
    body: JSON.stringify(request),
  })

export const resolveAgentTicket = (ticketReference, request) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/resolve`, {
    method: 'POST',
    body: JSON.stringify(request),
  })

export const getAgentTicketComments = (ticketReference, signal) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/comments`, { signal })

export const addAgentTicketComment = (ticketReference, request) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/comments`, {
    method: 'POST',
    body: JSON.stringify(request),
  })

export const getAgentInternalNotes = (ticketReference, signal) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/internal-notes`, { signal })

export const addAgentInternalNote = (ticketReference, request) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/internal-notes`, {
    method: 'POST',
    body: JSON.stringify(request),
  })

export const getAgentTicketHistory = (ticketReference, signal) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/history`, { signal })
