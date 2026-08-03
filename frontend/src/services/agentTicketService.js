import { apiRequest, toQueryString } from './apiClient.js'

export const getAgentDashboard = (signal) =>
  apiRequest('/api/agent/dashboard', { signal })

export const getAssignedTickets = (filters, signal) =>
  apiRequest(`/api/agent/tickets${toQueryString(filters)}`, { signal })

export const getOpenTickets = (filters, signal) =>
  apiRequest(`/api/agent/tickets/open${toQueryString(filters)}`, { signal })

export const getAgentTicketHistoryList = (filters, signal) =>
  apiRequest(`/api/agent/tickets/history${toQueryString(filters)}`, { signal })

export const requestAgentTicketAssignment = (ticketReference) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/assignment-requests`, {
    method: 'POST',
  })

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

export const markAgentTicketPending = (ticketReference, request) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/pending`, {
    method: 'POST',
    body: JSON.stringify(request),
  })

export const resumeAgentTicketWork = (ticketReference) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/resume-work`, {
    method: 'POST',
  })

export const closeAgentTicket = (ticketReference, request = {}) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/close`, {
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

export const getAgentTicketHistory = (ticketReference, signal) =>
  apiRequest(`/api/agent/tickets/${encodeURIComponent(ticketReference)}/history`, { signal })

export const downloadAgentTicketAttachment = (
  ticketReference,
  attachmentId,
) =>
  apiRequest(
    `/api/agent/tickets/${encodeURIComponent(ticketReference)}/attachments/${attachmentId}/download`,
    { responseType: 'blob' },
  )
