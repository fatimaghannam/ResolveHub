import { ticketCategories } from '../shared/ticketLookups.js'
import { getMockUserName } from './users.js'

// Temporary shared frontend data until the Administrator ticket APIs are implemented.
const ticketSeeds = [
  { id: 1072, ticketReferenceNumber: 'RH-2026-1072', title: 'VPN access unavailable after password change', requesterId: 11, categoryName: 'Network', priorityName: 'Critical', statusName: 'Open', assignedAgentId: null, createdDate: '2026-07-27T08:15:00Z' },
  { id: 1070, ticketReferenceNumber: 'RH-2026-1070', title: 'Finance shared printer is offline', requesterId: 12, categoryName: 'Hardware', priorityName: 'High', statusName: 'Assigned', assignedAgentId: 6, createdDate: '2026-07-26T14:20:00Z' },
  { id: 1069, ticketReferenceNumber: 'RH-2026-1069', title: 'Suspicious email attachment reported', requesterId: 13, categoryName: 'Security', priorityName: 'Critical', statusName: 'Open', assignedAgentId: null, createdDate: '2026-07-26T10:05:00Z' },
  { id: 1068, ticketReferenceNumber: 'RH-2026-1068', title: 'Outlook calendar is not synchronizing', requesterId: 14, categoryName: 'Email', priorityName: 'Medium', statusName: 'In Progress', assignedAgentId: 6, createdDate: '2026-07-25T16:45:00Z' },
  { id: 1065, ticketReferenceNumber: 'RH-2026-1065', title: 'Intermittent Wi-Fi in conference room', requesterId: 15, categoryName: 'Network', priorityName: 'High', statusName: 'Open', assignedAgentId: null, createdDate: '2026-07-24T11:30:00Z' },
  { id: 1064, ticketReferenceNumber: 'RH-2026-1064', title: 'Payroll application closes unexpectedly', requesterId: 16, categoryName: 'Software', priorityName: 'High', statusName: 'In Progress', assignedAgentId: 7, createdDate: '2026-07-23T09:10:00Z' },
  { id: 1062, ticketReferenceNumber: 'RH-2026-1062', title: 'New employee workstation setup', requesterId: 17, categoryName: 'Hardware', priorityName: 'Medium', statusName: 'Pending', assignedAgentId: 8, createdDate: '2026-07-22T13:35:00Z' },
  { id: 1059, ticketReferenceNumber: 'RH-2026-1059', title: 'Cannot open archived project folder', requesterId: 18, categoryName: 'Access Request', priorityName: 'Low', statusName: 'Resolved', assignedAgentId: 8, createdDate: '2026-07-20T08:50:00Z' },
  { id: 1057, ticketReferenceNumber: 'RH-2026-1057', title: 'Browser certificate warning on intranet', requesterId: 19, categoryName: 'Security', priorityName: 'Medium', statusName: 'Assigned', assignedAgentId: 7, createdDate: '2026-07-18T12:40:00Z' },
  { id: 1054, ticketReferenceNumber: 'RH-2026-1054', title: 'Teams microphone is not detected', requesterId: 20, categoryName: 'Hardware', priorityName: 'Low', statusName: 'Resolved', assignedAgentId: 6, createdDate: '2026-07-16T15:25:00Z' },
  { id: 1051, ticketReferenceNumber: 'RH-2026-1051', title: 'Distribution list delivery delayed', requesterId: 21, categoryName: 'Email', priorityName: 'Medium', statusName: 'Pending', assignedAgentId: 8, createdDate: '2026-07-14T10:15:00Z' },
  { id: 1048, ticketReferenceNumber: 'RH-2026-1048', title: 'Ethernet connection drops periodically', requesterId: 22, categoryName: 'Network', priorityName: 'High', statusName: 'In Progress', assignedAgentId: 7, createdDate: '2026-07-12T07:55:00Z' },
]

export const ticketMockData = ticketSeeds.map((ticket) => ({
  ...ticket,
  requesterName: getMockUserName(ticket.requesterId),
  assignedAgentName: ticket.assignedAgentId
    ? getMockUserName(ticket.assignedAgentId)
    : null,
}))

export { ticketCategories }
