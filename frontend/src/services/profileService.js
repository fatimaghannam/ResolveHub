import { apiRequest } from './apiClient.js'

export function uploadProfilePhoto(file) {
  const body = new FormData()
  body.append('photo', file)
  return apiRequest('/api/profile/photo', { method: 'POST', body })
}

export function removeProfilePhoto() {
  return apiRequest('/api/profile/photo', { method: 'DELETE' })
}
