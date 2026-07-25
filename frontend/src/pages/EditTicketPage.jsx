import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import TicketForm from '../components/tickets/TicketForm.jsx'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import { getTicket, updateTicket } from '../services/ticketService.js'

function EditTicketPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [ticket, setTicket] = useState(null)
  const [error, setError] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    getTicket(id, controller.signal).then(setTicket).catch((requestError) => setError(requestError.status === 404 ? 'This ticket was not found.' : requestError.message))
    return () => controller.abort()
  }, [id])

  if (error) return <ErrorState message={error} />
  if (!ticket) return <LoadingState message="Loading ticket…" />
  if (!ticket.canEdit) return <div className="state-panel"><h2>Ticket is read-only</h2><p>This ticket can no longer be edited because work has already started.</p><button className="button button--secondary" onClick={() => navigate(`/employee/tickets/${id}`)}>Back to Ticket</button></div>

  return (
    <>
      <section className="page-heading"><h2>Edit {ticket.ticketReferenceNumber}</h2><p>Update the issue information while the ticket is still open and unassigned.</p></section>
      <TicketForm
        initialValues={{ title: ticket.title, description: ticket.description, ticketCategoryId: ticket.ticketCategoryId, ticketPriorityId: ticket.ticketPriorityId }}
        submitLabel="Save Changes"
        onCancel={() => navigate(`/employee/tickets/${id}`)}
        onSubmit={async (values) => {
          await updateTicket(id, values)
          navigate(`/employee/tickets/${id}`, { replace: true, state: { notice: 'Ticket updated successfully.' } })
        }}
      />
    </>
  )
}

export default EditTicketPage
