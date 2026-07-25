import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import { deleteDraft, getDrafts } from '../services/ticketService.js'

function TicketDraftsPage() {
  const [drafts, setDrafts] = useState(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)

  useEffect(() => {
    setError('')
    setDrafts(null)
    getDrafts().then(setDrafts).catch(() => setError('Drafts could not be loaded.'))
  }, [reload])

  if (error) return <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />
  if (!drafts) return <LoadingState message="Loading drafts…" />
  return (
    <>
      <section className="page-heading"><h2>Ticket Drafts</h2><p>Continue incomplete support requests without affecting ticket totals.</p></section>
      <section className="panel">
        {drafts.length === 0 ? <p>No saved drafts.</p> : drafts.map((draft) => (
          <div className="draft-row" key={draft.id}>
            <div><strong>{draft.title || 'Untitled draft'}</strong><small>Updated {new Date(draft.updatedDate).toLocaleString()}</small></div>
            <div><Link to={`/employee/tickets/drafts/${draft.id}`}>Continue</Link><button type="button" onClick={async () => { await deleteDraft(draft.id); setReload((value) => value + 1) }}>Delete</button></div>
          </div>
        ))}
      </section>
    </>
  )
}

export default TicketDraftsPage
