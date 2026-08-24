import { apiRequest } from './apiClient.js'
import { formatLocalDateTime } from '../utils/dateTime.js'

export function localizeAiTicketTimestamps(response) {
  const tickets = response?.ticketLookup?.tickets
  if (!response?.message || tickets?.length !== 1 || !tickets[0].updatedAt) {
    return response
  }

  return {
    ...response,
    message: response.message.replace(
      /^Last updated:.*$/m,
      `Last updated: ${formatLocalDateTime(tickets[0].updatedAt)}`,
    ),
  }
}

export const analyzeTicket = (title, description) => apiRequest('/api/ai/tickets/analyze', {
  method: 'POST', body: JSON.stringify({ title, description }),
})
export const generateTicketSummary = (ticketId) => apiRequest(`/api/ai/tickets/${ticketId}/summary`, { method: 'POST' })
export const generateTroubleshooting = (ticketId) => apiRequest(`/api/ai/tickets/${ticketId}/troubleshooting`, { method: 'POST' })
export const sendAiChat = async (messages, ticketId = null, pageContext = null) => {
  const response = await apiRequest('/api/ai/chat', {
    method: 'POST', body: JSON.stringify({ messages, ticketId, pageContext }),
  })
  return localizeAiTicketTimestamps(response)
}
