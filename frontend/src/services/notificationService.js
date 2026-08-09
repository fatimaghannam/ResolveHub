import { apiRequest } from './apiClient.js'

export const getNotifications = (limit = 100, signal) =>
  apiRequest(`/api/notifications?limit=${limit}`, { signal })

export const markNotificationRead = (notificationId) =>
  apiRequest(`/api/notifications/${notificationId}/read`, { method: 'PATCH' })

export const markAllNotificationsRead = () =>
  apiRequest('/api/notifications/read-all', { method: 'PATCH' })
