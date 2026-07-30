import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import { deleteDraft, getDrafts } from '../services/ticketService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'

function TicketDraftsPage({ roleArea = 'employee' }) {
  const [drafts, setDrafts] = useState(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)
  const [toast, setToast] = useState(null)
  const dismissToast = useCallback(() => setToast(null), [])

  async function removeDraft(draft) {
    try {
      await deleteDraft(draft.id)
      setReload((value) => value + 1)
      setToast({
        id: Date.now(),
        type: 'success',
        title: 'Draft Deleted',
        message: `${draft.title || 'Untitled draft'} was deleted.`,
      })
    } catch (requestError) {
      setToast({
        id: Date.now(),
        type: 'error',
        title: 'Unable to Delete Draft',
        message: requestError.message,
      })
    }
  }

  useEffect(() => {
    setError('')
    setDrafts(null)
    getDrafts().then(setDrafts).catch(() => setError('Drafts could not be loaded.'))
  }, [reload])

  if (error) return <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />
  if (!drafts) return <LoadingState message="Loading drafts…" />
  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <section className="page-heading"><h2>Ticket Drafts</h2><p>Continue incomplete support requests without affecting ticket totals.</p></section>
      <section className="panel">
        {drafts.length === 0 ? <p>No saved drafts.</p> : drafts.map((draft) => (
          <div className="draft-row" key={draft.id}>
            <div><strong>{draft.title || 'Untitled draft'}</strong><small>Updated {formatLocalDateTime(draft.updatedDate)}</small></div>
            <div><Link to={`/${roleArea}/tickets/drafts/${draft.id}`}>Continue</Link><button type="button" onClick={() => removeDraft(draft)}>Delete</button></div>
          </div>
        ))}
      </section>
    </>
  )
}

export default TicketDraftsPage
