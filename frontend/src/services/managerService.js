import { apiRequest, toQueryString } from './apiClient.js'

export const getManagerDashboard = (signal) =>
  apiRequest('/api/manager/dashboard', { signal })

export const getManagerTickets = (filters, signal) =>
  apiRequest(`/api/manager/tickets${toQueryString(filters)}`, { signal })

export const getManagerTicket = (ticketReference, signal) =>
  apiRequest(`/api/manager/tickets/${encodeURIComponent(ticketReference)}`, { signal })

export const getManagerAssignments = (signal) =>
  apiRequest('/api/manager/assignments', { signal })

export const assignManagerTicket = (ticketReference, agentUserId) =>
  apiRequest(`/api/manager/tickets/${encodeURIComponent(ticketReference)}/assign`, {
    method: 'POST',
    body: JSON.stringify({ agentUserId }),
  })

export const getManagerWorkload = (signal) =>
  apiRequest('/api/manager/workload', { signal })

export const getManagerActivity = (signal) =>
  apiRequest('/api/manager/activity', { signal })
