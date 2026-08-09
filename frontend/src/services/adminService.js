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

export const getAdminWorkload = (signal) =>
  apiRequest('/api/admin/users/agents', { signal })

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

export const getAdminAssignmentRequests = (signal) =>
  apiRequest('/api/admin/assignment-requests', { signal })

export const reviewAdminAssignmentRequest = (requestId, decision, reason = null) =>
  apiRequest(`/api/admin/assignment-requests/${requestId}/${decision}`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
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

export const getAdminUsers = (filters = {}, signal) =>
  apiRequest(`/api/admin/users${toQueryString(filters)}`, { signal })

export const getAdminUser = (userId, signal) =>
  apiRequest(`/api/admin/users/${userId}`, { signal })

export const getAdminUserDepartments = (signal) =>
  apiRequest('/api/admin/users/departments', { signal })

export const createAdminUser = (request) =>
  apiRequest('/api/admin/users', {
    method: 'POST',
    body: JSON.stringify(request),
  })

export const resendAdminUserInvitation = (userId) =>
  apiRequest(`/api/admin/users/${userId}/resend-invitation`, { method: 'POST' })

export const updateAdminUserStatus = (userId, isActive) =>
  apiRequest(`/api/admin/users/${userId}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ isActive }),
  })

export const getSystemAuditLog = (filters = {}, signal) =>
  apiRequest(`/api/admin/audit-log${toQueryString(filters)}`, { signal })

export const getAdminCategories = (filters = {}, signal) =>
  apiRequest(`/api/admin/categories${toQueryString(filters)}`, { signal })

export const createAdminCategory = (request) =>
  apiRequest('/api/admin/categories', { method: 'POST', body: JSON.stringify(request) })

export const updateAdminCategory = (categoryId, request) =>
  apiRequest(`/api/admin/categories/${categoryId}`, { method: 'PUT', body: JSON.stringify(request) })

export const updateAdminCategoryStatus = (categoryId, isActive) =>
  apiRequest(`/api/admin/categories/${categoryId}/status`, {
    method: 'PATCH', body: JSON.stringify({ isActive }),
  })
