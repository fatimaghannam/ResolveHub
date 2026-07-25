import { useNavigate } from 'react-router-dom'
import TicketForm from '../components/tickets/TicketForm.jsx'
import { createTicket } from '../services/ticketService.js'

function CreateTicketPage() {
  const navigate = useNavigate()
  return (
    <>
      <section className="page-heading"><h2>Create Support Ticket</h2><p>Provide clear details so the IT support team can understand and resolve the issue efficiently.</p></section>
      <TicketForm submitLabel="Create Ticket" onCancel={() => navigate('/employee/tickets')} onSubmit={async (values) => {
        const ticket = await createTicket(values)
        navigate(`/employee/tickets/${ticket.id}`, { replace: true, state: { notice: 'Ticket created successfully.' } })
      }} />
    </>
  )
}

export default CreateTicketPage
