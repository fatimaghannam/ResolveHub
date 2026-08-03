export const TICKET_ACTIVITY_CHANGED_EVENT = 'resolvehub:ticket-activity-changed'

export function notifyTicketActivityChanged(path) {
  const match = path.match(/^\/api\/(?:agent\/|admin\/|manager\/)?tickets\/([^/?#]+)/i)
  if (!match) return
  let ticketReference
  try {
    ticketReference = decodeURIComponent(match[1])
  } catch {
    ticketReference = match[1]
  }
  if (!/^RH-\d{4}-\d+$/i.test(ticketReference)) return
  window.dispatchEvent(new CustomEvent(TICKET_ACTIVITY_CHANGED_EVENT, {
    detail: { ticketReference },
  }))
}
