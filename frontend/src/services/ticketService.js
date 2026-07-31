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
export const uploadAttachment = (ticketId, file) => {
  const form = new FormData()
  form.append('file', file)
  return apiRequest(`/api/tickets/${ticketId}/attachments`, {
    method: 'POST',
    body: form,
  })
}
export const deleteAttachment = (ticketId, attachmentId) =>
  apiRequest(`/api/tickets/${ticketId}/attachments/${attachmentId}`, {
    method: 'DELETE',
  })
export const downloadAttachment = (ticketId, attachmentId) =>
  apiRequest(`/api/tickets/${ticketId}/attachments/${attachmentId}/download`, {
    responseType: 'blob',
  })
export const addTicketComment = (ticketId, request) =>
  apiRequest(`/api/tickets/${ticketId}/comments`, {
    method: 'POST',
    body: JSON.stringify(request),
  })
export const getDrafts = () => apiRequest('/api/ticket-drafts')
export const getDraft = (id, signal) =>
  apiRequest(`/api/ticket-drafts/${id}`, { signal })
export const createDraft = (values) =>
  apiRequest('/api/ticket-drafts', {
    method: 'POST',
    body: JSON.stringify(values),
  })
export const updateDraft = (id, values) =>
  apiRequest(`/api/ticket-drafts/${id}`, {
    method: 'PUT',
    body: JSON.stringify(values),
  })
export const deleteDraft = (id) =>
  apiRequest(`/api/ticket-drafts/${id}`, { method: 'DELETE' })
export const submitDraft = (id) =>
  apiRequest(`/api/ticket-drafts/${id}/submit`, { method: 'POST' })
