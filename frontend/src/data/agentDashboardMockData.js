// Temporary frontend data until the IT Agent dashboard APIs are implemented.

export const agentTicketStats = [
  { label: 'Active Assigned Tickets', value: 9, tone: 'blue' },
  { label: 'In Progress', value: 3, tone: 'cyan' },
  { label: 'Pending', value: 3, tone: 'amber' },
  { label: 'High Priority Open', value: 3, tone: 'amber' },
  { label: 'Critical Open', value: 2, tone: 'red' },
  { label: 'Resolved This Month', value: 3, tone: 'green' },
]

export const mockAssignedAgent = 'Emily Carter'

export const agentTickets = [
  { id: 101, ticketReferenceNumber: 'RH-2026-1048', title: 'VPN access unavailable after password change', description: 'The corporate VPN rejects the updated password even though other company services accept it.', requester: 'Olivia Bennett', category: 'Access Request', priority: 'Critical', status: 'Assigned', createdDate: '2026-07-26T07:35:00Z' },
  { id: 102, ticketReferenceNumber: 'RH-2026-1044', title: 'Finance shared printer is offline', description: 'The shared printer on the finance floor appears offline for every team member.', requester: 'Daniel Brooks', category: 'Hardware', priority: 'High', status: 'In Progress', createdDate: '2026-07-25T14:20:00Z' },
  { id: 103, ticketReferenceNumber: 'RH-2026-1039', title: 'Suspicious email attachment reported', description: 'An unexpected attachment was received from an unfamiliar sender and has not been opened.', requester: 'Sophia Mitchell', category: 'Security', priority: 'Critical', status: 'Pending', createdDate: '2026-07-25T09:10:00Z' },
  { id: 104, ticketReferenceNumber: 'RH-2026-1037', title: 'Outlook calendar is not synchronizing', description: 'Recent calendar changes are visible on the web but not in the desktop application.', requester: 'Ethan Parker', category: 'Email', priority: 'Medium', status: 'Assigned', createdDate: '2026-07-24T16:45:00Z' },
  { id: 105, ticketReferenceNumber: 'RH-2026-1031', title: 'Intermittent Wi-Fi in conference room', description: 'Wireless connections drop repeatedly during meetings in the main conference room.', requester: 'Ava Collins', category: 'Network', priority: 'High', status: 'In Progress', createdDate: '2026-07-23T11:25:00Z' },
  { id: 106, ticketReferenceNumber: 'RH-2026-1026', title: 'Payroll application closes unexpectedly', description: 'The payroll application closes when the monthly reporting screen is opened.', requester: 'Michael Reed', category: 'Software', priority: 'High', status: 'Pending', createdDate: '2026-07-22T08:40:00Z' },
  { id: 107, ticketReferenceNumber: 'RH-2026-1020', title: 'New employee workstation setup', description: 'A standard laptop, monitor, and approved applications are needed for a new team member.', requester: 'Jessica Morgan', category: 'Hardware', priority: 'Medium', status: 'Assigned', createdDate: '2026-07-21T13:05:00Z' },
  { id: 108, ticketReferenceNumber: 'RH-2026-1014', title: 'Cannot open archived project folder', description: 'Access to the archived project folder was restored after permissions were reviewed.', requester: 'Ryan Cooper', category: 'Access Request', priority: 'Low', status: 'Resolved', createdDate: '2026-07-19T10:30:00Z' },
  { id: 109, ticketReferenceNumber: 'RH-2026-1008', title: 'Browser certificate warning on intranet', description: 'The intranet certificate chain was corrected and the browser warning is resolved.', requester: 'Hannah Foster', category: 'Security', priority: 'High', status: 'Resolved', createdDate: '2026-07-17T15:15:00Z' },
  { id: 110, ticketReferenceNumber: 'RH-2026-1002', title: 'Teams microphone is not detected', description: 'Microsoft Teams does not list the connected USB headset microphone as an input device.', requester: 'Brandon Turner', category: 'Software', priority: 'Medium', status: 'In Progress', createdDate: '2026-07-15T12:00:00Z' },
  { id: 111, ticketReferenceNumber: 'RH-2026-0997', title: 'Distribution list delivery delayed', description: 'Messages sent to the department distribution list arrive after a significant delay.', requester: 'Olivia Bennett', category: 'Email', priority: 'Low', status: 'Pending', createdDate: '2026-07-12T09:50:00Z' },
  { id: 112, ticketReferenceNumber: 'RH-2026-0991', title: 'Ethernet connection drops periodically', description: 'The wired network connection is now stable after the damaged cable was replaced.', requester: 'Daniel Brooks', category: 'Network', priority: 'Medium', status: 'Resolved', createdDate: '2026-07-09T14:35:00Z' },
]

export const priorityAttentionTickets = agentTickets.filter(
  (ticket) => ['Critical', 'High'].includes(ticket.priority),
).slice(0, 3)

export const recentAssignedTickets = agentTickets.slice(0, 5)

export const agentFilterOptions = {
  statuses: ['Assigned', 'In Progress', 'Pending', 'Resolved'],
  categories: ['Hardware', 'Software', 'Network', 'Email', 'Access Request', 'Security', 'Other'],
  priorities: ['Low', 'Medium', 'High', 'Critical'],
}
