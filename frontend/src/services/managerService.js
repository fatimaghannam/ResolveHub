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
  apiRequest(`/api/manager/tickets/${encodeURIComponent(ticketReference)}/assignment-requests`, {
    method: 'POST',
    body: JSON.stringify({ agentUserId }),
  })

export const getManagerWorkload = (signal) =>
  apiRequest('/api/manager/workload', { signal })



export const getManagerAssignmentRequests = (signal) =>
  apiRequest('/api/manager/assignment-requests', { signal })

export const getManagerAgentAssignmentRequests = (signal) =>
  apiRequest('/api/manager/agent-assignment-requests', { signal })

export const reviewManagerAgentAssignmentRequest = (requestId, decision, reason = null) =>
  apiRequest(`/api/manager/agent-assignment-requests/${requestId}/${decision}`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  })

export const getManagerCancellationRequests = (signal) =>
  apiRequest('/api/manager/cancellation-requests', { signal })

export const reviewManagerCancellationRequest = (requestId, decision, reviewNote = null) =>
  apiRequest(`/api/manager/cancellation-requests/${requestId}/${decision}`, {
    method: 'POST',
    body: JSON.stringify({ reviewNote }),
  })

export const addManagerTicketComment = (ticketReference, message) =>
  apiRequest(`/api/manager/tickets/${encodeURIComponent(ticketReference)}/comments`, {
    method: 'POST',
    body: JSON.stringify({ message }),
  })

export const reportManagerDuplicate = (ticketReference, request) =>
  apiRequest(`/api/manager/tickets/${encodeURIComponent(ticketReference)}/duplicate-reviews`, {
    method: 'POST',
    body: JSON.stringify(request),
  })

export const getManagerNotifications = (signal) =>
  apiRequest('/api/manager/notifications', { signal })

export const markManagerNotificationRead = (notificationId) =>
  apiRequest(`/api/manager/notifications/${notificationId}/read`, { method: 'PATCH' })

export const markAllManagerNotificationsRead = () =>
  apiRequest('/api/manager/notifications/read-all', { method: 'PATCH' })
