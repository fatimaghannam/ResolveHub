export function TicketStatusBadge({ value }) {
  const key = value.toLowerCase().replaceAll(' ', '-')
  return <span className={`badge status-${key}`}>{value}</span>
}

export function TicketPriorityBadge({ value }) {
  return <span className={`badge priority-${value.toLowerCase()}`}>{value}</span>
}
