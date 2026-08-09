import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import TicketForm from '../../components/tickets/TicketForm.jsx'
import { ErrorState, LoadingState } from '../../components/common/States.jsx'
import {
  getDraft,
  submitDraft,
  updateDraft,
  uploadAttachment,
} from '../../services/ticketService.js'
import { formatTicketReference } from '../../utils/ticketReference.js'

function EditTicketDraftPage({ roleArea = 'employee' }) {
  const { id } = useParams()
  const navigate = useNavigate()
  const [draft, setDraft] = useState(null)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    setDraft(null)
    getDraft(id, controller.signal)
      .then((result) => { if (!controller.signal.aborted) setDraft(result) })
      .catch((requestError) => {
        if (requestError.name !== 'AbortError' && !controller.signal.aborted) {
          setError(requestError.status === 404 ? 'This draft was not found.' : 'The draft could not be loaded.')
        }
      })
    return () => controller.abort()
  }, [id, retry])

  if (error) return <ErrorState message={error} onRetry={() => setRetry((value) => value + 1)} />
  if (!draft) return <LoadingState message="Loading draft…" />
  return (
    <div className="ticket-form-page">
      <section className="page-heading"><h2>Edit Draft</h2><p>Complete the required fields when you are ready to submit this request.</p></section>
      <TicketForm
        mode="draft"
        initialValues={draft}
        submitLabel="Submit Ticket"
        onCancel={() => navigate(`/${roleArea}/tickets/drafts`)}
        onSaveDraft={(values) => updateDraft(id, values)}
        onSubmit={async (values, files) => {
          await updateDraft(id, values)
          const ticket = await submitDraft(id)
          const failed = []
          for (const file of files) {
            try { await uploadAttachment(ticket.id, file) } catch { failed.push(file.name) }
          }
          const destination = roleArea === 'admin' || roleArea === 'manager'
            ? `/${roleArea}/tickets/${formatTicketReference(ticket)}`
            : `/employee/tickets/${ticket.id}`
          navigate(destination, {
            replace: true,
            state: {
              toast: {
                type: failed.length ? 'warning' : 'success',
                title: 'Ticket Created',
                message: failed.length
                  ? `${formatTicketReference(ticket)} was created. These attachments could not be uploaded: ${failed.join(', ')}.`
                  : `${formatTicketReference(ticket)} was created successfully.`,
              },
            },
          })
        }}
      />
    </div>
  )
}

export default EditTicketDraftPage
