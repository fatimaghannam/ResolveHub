import { apiRequest, toQueryString } from './apiClient.js'

export const getManagerDashboard = (signal) =>
  apiRequest('/api/manager/dashboard', { signal })

export const getManagerTickets = (filters, signal) =>
  apiRequest(`/api/manager/tickets${toQueryString(filters)}`, { signal })

export const getManagerTicket = (ticketReference, signal) =>
  apiRequest(`/api/manager/tickets/${encodeURIComponent(ticketReference)}`, { signal })

export const getManagerAssignments = (filters = {}, signal) =>
  apiRequest(`/api/manager/assignments${toQueryString(filters)}`, { signal })

export const assignManagerTicket = (ticketReference, agentUserId) =>
  apiRequest(`/api/manager/tickets/${encodeURIComponent(ticketReference)}/assign`, {
    method: 'POST',
    body: JSON.stringify({ agentUserId }),
  })

export const getManagerWorkload = (signal) =>
  apiRequest('/api/manager/workload', { signal })

export const getManagerActivity = (signal) =>
  apiRequest('/api/manager/activity', { signal })

export const getManagerAssignmentRequests = (signal) =>
  apiRequest('/api/manager/assignment-requests', { signal })

export const reviewManagerAssignmentRequest = (requestId, decision) =>
  apiRequest(`/api/manager/assignment-requests/${requestId}/${decision}`, {
    method: 'POST',
  })

export const addManagerTicketComment = (ticketReference, message) =>
  apiRequest(`/api/manager/tickets/${encodeURIComponent(ticketReference)}/comments`, {
    method: 'POST',
    body: JSON.stringify({ message }),
  })
