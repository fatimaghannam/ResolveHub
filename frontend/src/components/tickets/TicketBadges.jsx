export function TicketStatusBadge({ value }) {
  const key = value.toLowerCase().replaceAll(' ', '-')
  return <span className={`badge ticket-status-badge status-${key}`}>{value}</span>
}

export function TicketPriorityBadge({ value }) {
  return <span className={`badge priority-${value.toLowerCase()}`}>{value}</span>
}

export function PendingApprovalBadge() {
  return (
    <span className="badge badge--pending-approval">
      Waiting for Manager Approval
    </span>
  )
}
