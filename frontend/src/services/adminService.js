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

export const reviewAdminDuplicate = (reviewId, decision, internalNote = null) =>
  apiRequest(`/api/admin/duplicate-reviews/${reviewId}/${decision}`, {
    method: 'POST',
    body: JSON.stringify({ internalNote }),
  })

export const markAdminTicketDuplicate = (ticketReference, request) =>
  apiRequest(`/api/admin/tickets/${encodeURIComponent(ticketReference)}/mark-duplicate`, {
    method: 'POST',
    body: JSON.stringify(request),
  })

export const getAdminNotifications = (signal) =>
  apiRequest('/api/admin/notifications', { signal })

export const markAdminNotificationRead = (notificationId) =>
  apiRequest(`/api/admin/notifications/${notificationId}/read`, { method: 'PATCH' })

export const markAllAdminNotificationsRead = () =>
  apiRequest('/api/admin/notifications/read-all', { method: 'PATCH' })

export const addAdminTicketComment = (ticketReference, message) =>
  apiRequest(`/api/admin/tickets/${encodeURIComponent(ticketReference)}/comments`, {
    method: 'POST',
    body: JSON.stringify({ message }),
  })

export const getAdminUsers = (signal) =>
  apiRequest('/api/admin/users', { signal })

export const updateAdminUserStatus = (userId, isActive) =>
  apiRequest(`/api/admin/users/${userId}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ isActive }),
  })
