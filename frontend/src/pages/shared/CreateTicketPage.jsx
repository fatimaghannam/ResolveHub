import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import TicketForm from '../../components/tickets/TicketForm.jsx'
import {
  createDraft,
  createTicket,
  updateDraft,
  uploadAttachment,
} from '../../services/ticketService.js'
import { formatTicketReference } from '../../utils/ticketReference.js'

function CreateTicketPage({ roleArea = 'employee' }) {
  const navigate = useNavigate()
  const [draftId, setDraftId] = useState(null)
  const isManagement = roleArea === 'admin' || roleArea === 'manager'

  return (
    <div className="ticket-form-page">
      <section className="page-heading">
        <h2>Create Support Ticket</h2>
        <p>Provide clear details so the IT support team can understand and resolve the issue efficiently.</p>
      </section>
      <TicketForm mode="create" submitLabel="Submit Ticket" onCancel={() => navigate(`/${roleArea}/tickets`)} onSaveDraft={async (values) => {
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
        const destination = isManagement
          ? `/${roleArea}/tickets`
          : `/employee/tickets/${ticket.id}`
        navigate(destination, {
          replace: true,
          state: {
            toast: {
              type: failed.length ? 'warning' : 'success',
              title: 'Ticket Created',
              message: notice,
            },
          },
        })
      }} />
    </div>
  )
}

export default CreateTicketPage
