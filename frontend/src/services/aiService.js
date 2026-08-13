import { apiRequest } from './apiClient.js'

export const analyzeTicket = (title, description) => apiRequest('/api/ai/tickets/analyze', {
  method: 'POST', body: JSON.stringify({ title, description }),
})
export const generateTicketSummary = (ticketId) => apiRequest(`/api/ai/tickets/${ticketId}/summary`, { method: 'POST' })
export const generateTroubleshooting = (ticketId) => apiRequest(`/api/ai/tickets/${ticketId}/troubleshooting`, { method: 'POST' })
export const sendAiChat = (messages, ticketId = null, pageContext = null) => apiRequest('/api/ai/chat', {
  method: 'POST', body: JSON.stringify({ messages, ticketId, pageContext }),
})
