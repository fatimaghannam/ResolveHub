import { FilePenLine, Search } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ErrorState, LoadingState } from '../../components/common/States.jsx'
import Toast from '../../components/common/Toast.jsx'
import { TicketPriorityBadge } from '../../components/tickets/TicketBadges.jsx'
import { deleteDraft, getCategories, getDrafts, getPriorities } from '../../services/ticketService.js'
import { formatLocalDateTime } from '../../utils/dateTime.js'

const sortOptions = [
  ['updated-desc', 'Last Updated (Newest First)'],
  ['updated-asc', 'Last Updated (Oldest First)'],
  ['title-asc', 'Title (A–Z)'],
  ['title-desc', 'Title (Z–A)'],
]

function TicketDraftsPage({ roleArea = 'employee' }) {
  const [drafts, setDrafts] = useState(null)
  const [categories, setCategories] = useState([])
  const [priorities, setPriorities] = useState([])
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState('updated-desc')
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
    Promise.all([getDrafts(), getCategories(), getPriorities()])
      .then(([draftItems, categoryItems, priorityItems]) => {
        setDrafts(draftItems)
        setCategories(categoryItems)
        setPriorities(priorityItems)
      })
      .catch(() => setError('Drafts could not be loaded.'))
  }, [reload])

  const visibleDrafts = useMemo(() => {
    if (!drafts) return []
    const query = search.trim().toLocaleLowerCase()
    const filtered = query
      ? drafts.filter((draft) => (draft.title ?? '').toLocaleLowerCase().includes(query))
      : drafts

    return [...filtered].sort((left, right) => {
      if (sort === 'updated-asc')
        return new Date(left.updatedDate).getTime() - new Date(right.updatedDate).getTime()
      if (sort === 'title-asc')
        return (left.title || 'Untitled draft').localeCompare(right.title || 'Untitled draft')
      if (sort === 'title-desc')
        return (right.title || 'Untitled draft').localeCompare(left.title || 'Untitled draft')
      return new Date(right.updatedDate).getTime() - new Date(left.updatedDate).getTime()
    })
  }, [drafts, search, sort])

  const categoryNames = useMemo(() => new Map(categories.map((item) => [item.id, item.name])), [categories])
  const priorityNames = useMemo(() => new Map(priorities.map((item) => [item.id, item.name])), [priorities])
  const createPath = `/${roleArea}/tickets/create`
  const visibleCountLabel = `${visibleDrafts.length.toLocaleString()} ${visibleDrafts.length === 1 ? 'draft' : 'drafts'}`

  if (error) return <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />
  if (!drafts) return <LoadingState message="Loading drafts…" />

  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <section className="page-heading draft-page-heading"><h2>Ticket Drafts</h2><p>Continue incomplete support requests without affecting ticket totals.</p><span className="draft-count" aria-live="polite">{visibleCountLabel}</span></section>
      <section className="draft-toolbar" aria-label="Draft search and sorting">
        <label className="draft-search"><span className="visually-hidden">Search drafts by title</span><Search size={18} aria-hidden="true" /><input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search drafts by title..." /></label>
        <label className="draft-sort"><span>Sort</span><select value={sort} onChange={(event) => setSort(event.target.value)}>{sortOptions.map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
      </section>
      {drafts.length === 0
        ? <section className="panel draft-empty-state"><span className="draft-empty-state__icon"><FilePenLine size={28} aria-hidden="true" /></span><h2>No drafts found</h2><p>Start creating a ticket and save it as a draft to continue later.</p><Link className="button button--primary" to={createPath}>Create Ticket</Link></section>
        : visibleDrafts.length === 0
          ? <section className="panel draft-empty-state"><span className="draft-empty-state__icon"><Search size={28} aria-hidden="true" /></span><h2>No matching drafts</h2><p>Try another search term.</p></section>
          : <section className="draft-grid" aria-label={`${visibleDrafts.length} ticket drafts`}>
            {visibleDrafts.map((draft) => {
              const category = categoryNames.get(draft.ticketCategoryId) || 'Not selected'
              const priority = priorityNames.get(draft.ticketPriorityId) || 'Not selected'
              return <article className="panel draft-card" key={draft.id}>
                <div className="draft-card__content"><h3>{draft.title || 'Untitled draft'}</h3><div className="draft-card__metadata"><span className="badge draft-category-badge">{category}</span>{priority === 'Not selected' ? <span className="badge draft-priority-unset">{priority}</span> : <TicketPriorityBadge value={priority} />}<span className="draft-card__updated">Updated {formatLocalDateTime(draft.updatedDate)}</span></div></div>
                <div className="draft-card__actions"><Link className="button button--primary" to={`/${roleArea}/tickets/drafts/${draft.id}`}>Continue</Link><button className="button button--secondary draft-card__delete" type="button" onClick={() => removeDraft(draft)}>Delete</button></div>
              </article>
            })}
          </section>}
    </>
  )
}

export default TicketDraftsPage
