export function notificationTarget(notification, roleArea) {
  if (notification.type === 'AssignmentRequestCreated' && roleArea === 'admin')
    return '/admin/assignments'
  if (notification.type === 'CancellationRequestCreated' && roleArea === 'manager')
    return '/manager/assignments'
  if (!notification.ticketReferenceNumber) return `/${roleArea}/notifications`
  if (roleArea === 'employee') return '/employee/tickets'
  return `/${roleArea}/tickets/${encodeURIComponent(notification.ticketReferenceNumber)}`
}
