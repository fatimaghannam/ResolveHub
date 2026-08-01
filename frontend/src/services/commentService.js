import { apiRequest } from './apiClient.js'

export const getComments = (endpoint, { visibility = 'All', page = 1, pageSize = 15 } = {}, signal) => {
  const query = new URLSearchParams({ visibility, page: String(page), pageSize: String(pageSize) })
  return apiRequest(`${endpoint}?${query}`, { signal })
}

export const addComment = (endpoint, request) => {
  const body = new FormData()
  body.append('Content', request.message)
  body.append('Visibility', request.visibility)
  if (request.parentCommentId) body.append('ParentCommentId', String(request.parentCommentId))
  request.files?.forEach((file) => body.append('Attachments', file))
  return apiRequest(endpoint, { method: 'POST', body })
}

export const replyToComment = (endpoint, commentId, message, files = []) =>
  addComment(endpoint, { message, visibility: 'Public', parentCommentId: commentId, files })

export const editComment = (endpoint, commentId, message) =>
  apiRequest(`${endpoint}/${commentId}`, {
    method: 'PUT',
    body: JSON.stringify({ message }),
  })

export const deleteComment = (endpoint, commentId) =>
  apiRequest(`${endpoint}/${commentId}`, { method: 'DELETE' })

export const downloadCommentAttachment = async (endpoint, commentId, attachment) => {
  const blob = await apiRequest(`${endpoint}/${commentId}/attachments/${attachment.id}`, {
    responseType: 'blob',
  })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = attachment.fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
