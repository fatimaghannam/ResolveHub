import { apiRequest, toQueryString } from './apiClient.js'

export const getDashboard = (signal) =>
  apiRequest('/api/employee/dashboard', { signal })
export const getTickets = (filters, signal) =>
  apiRequest(`/api/tickets${toQueryString(filters)}`, { signal })
export const getTicket = (id, signal) =>
  apiRequest(`/api/tickets/${id}`, { signal })
export const createTicket = (values) =>
  apiRequest('/api/tickets', {
    method: 'POST',
    body: JSON.stringify(values),
  })
export const updateTicket = (id, values) =>
  apiRequest(`/api/tickets/${id}`, {
    method: 'PUT',
    body: JSON.stringify(values),
  })
export const cancelTicket = (id, reason) =>
  apiRequest(`/api/tickets/${id}/cancel`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  })
export const getCategories = (signal) =>
  apiRequest('/api/ticket-categories', { signal })
export const getPriorities = (signal) =>
  apiRequest('/api/ticket-priorities', { signal })
export const getStatuses = (signal) =>
  apiRequest('/api/ticket-statuses', { signal })
