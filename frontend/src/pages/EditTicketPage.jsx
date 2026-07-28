import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import TicketForm from '../components/tickets/TicketForm.jsx'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import {
  deleteAttachment,
  getTicket,
  updateTicket,
  uploadAttachment,
} from '../services/ticketService.js'
import { formatTicketReference } from '../utils/ticketReference.js'

function EditTicketPage({ roleArea = 'employee' }) {
  const { id } = useParams()
  const navigate = useNavigate()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    setTicket(null)
    getTicket(id, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setTicket(result)
      })
      .catch((requestError) => {
        if (requestError.name === 'AbortError' || controller.signal.aborted) return
        setError(requestError.status === 404
          ? 'This ticket was not found.'
          : 'The ticket could not be loaded.')
      })
    return () => controller.abort()
  }, [id, retry])

  if (error) return <ErrorState message={error} onRetry={() => setRetry((value) => value + 1)} />
  if (!ticket) return <LoadingState message="Loading ticket…" />
  const ticketPath = ticket
    ? roleArea === 'employee'
      ? `/employee/tickets/${id}`
      : `/${roleArea}/tickets/${formatTicketReference(ticket)}`
    : `/${roleArea}/my-tickets`

  if (!ticket.canEdit) return <div className="state-panel"><h2>Ticket is read-only</h2><p>This ticket can no longer be edited because work has already started.</p><button className="button button--secondary" onClick={() => navigate(ticketPath)}>Back to Ticket</button></div>

  return (
    <>
      <section className="page-heading"><h2>Edit {formatTicketReference(ticket)}</h2><p>Update the issue information while the ticket is still open and unassigned.</p></section>
      <TicketForm
        mode="edit"
        initialValues={{ title: ticket.title, description: ticket.description, ticketCategoryId: ticket.ticketCategoryId, ticketPriorityId: ticket.ticketPriorityId }}
        existingAttachments={ticket.attachments}
        submitLabel="Save Changes"
        onCancel={() => navigate(ticketPath)}
        onDeleteAttachment={async (attachmentId) => {
          await deleteAttachment(id, attachmentId)
          setTicket({ ...ticket, attachments: ticket.attachments.filter((item) => item.id !== attachmentId) })
        }}
        onSubmit={async (values, files) => {
          await updateTicket(id, values)
          const failed = []
          for (const file of files) {
            try {
              await uploadAttachment(id, file)
            } catch {
              failed.push(file.name)
            }
          }
          const notice = failed.length
            ? `Ticket updated. These attachments could not be uploaded: ${failed.join(', ')}.`
            : 'Ticket updated successfully.'
          navigate(ticketPath, { replace: true, state: { notice } })
        }}
      />
    </>
  )
}

export default EditTicketPage
