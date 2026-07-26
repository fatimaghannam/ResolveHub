const standardTicketReferencePattern = /^RH-(\d{4})-(\d{4,})$/
const ticketReferenceYearPattern = /^RH-(\d{4})-/

export function formatTicketReference(ticket) {
  const reference = ticket?.ticketReferenceNumber?.trim()
  if (standardTicketReferencePattern.test(reference)) {
    return reference
  }

  const referenceYear = reference?.match(ticketReferenceYearPattern)?.[1]
  const createdYear = ticket?.createdDate
    ? new Date(ticket.createdDate).getUTCFullYear()
    : null
  const year = referenceYear ?? createdYear ?? new Date().getUTCFullYear()
  const sequence = Number(ticket?.id)

  if (!Number.isSafeInteger(sequence) || sequence < 1) {
    return reference ?? 'Ticket reference unavailable'
  }

  return `RH-${year}-${String(sequence).padStart(4, '0')}`
}
