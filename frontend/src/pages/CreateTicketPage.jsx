import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import TicketForm from '../components/tickets/TicketForm.jsx'
import {
  createDraft,
  createTicket,
  updateDraft,
  uploadAttachment,
} from '../services/ticketService.js'
import { formatTicketReference } from '../utils/ticketReference.js'

function CreateTicketPage() {
  const navigate = useNavigate()
  const [draftId, setDraftId] = useState(null)
  return (
    <>
      <section className="page-heading"><h2>Create Support Ticket</h2><p>Provide clear details so the IT support team can understand and resolve the issue efficiently.</p></section>
      <TicketForm mode="create" submitLabel="Submit Ticket" onCancel={() => navigate('/employee/tickets')} onSaveDraft={async (values) => {
        const draft = draftId
          ? await updateDraft(draftId, values)
          : await createDraft(values)
        setDraftId(draft.id)
      }} onSubmit={async (values, files) => {
        const ticket = await createTicket(values)
        const failed = []
        for (const file of files) {
          try {
            await uploadAttachment(ticket.id, file)
          } catch {
            failed.push(file.name)
          }
        }
        const ticketReference = formatTicketReference(ticket)
        const notice = failed.length
          ? `${ticketReference} was created. These attachments could not be uploaded: ${failed.join(', ')}.`
          : `${ticketReference} was created successfully.`
        navigate(`/employee/tickets/${ticket.id}`, { replace: true, state: { notice } })
      }} />
    </>
  )
}

export default CreateTicketPage
