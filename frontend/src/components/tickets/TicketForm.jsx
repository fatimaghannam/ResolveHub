import { useEffect, useState } from 'react'
import { getCategories, getPriorities } from '../../services/ticketService.js'
import { ErrorState, LoadingState } from '../common/States.jsx'

function TicketForm({ initialValues, submitLabel, onSubmit, onCancel }) {
  const [values, setValues] = useState(initialValues ?? { title: '', description: '', ticketCategoryId: '', ticketPriorityId: '' })
  const [lookups, setLookups] = useState(null)
  const [errors, setErrors] = useState({})
  const [loadingError, setLoadingError] = useState('')
  const [lookupRequest, setLookupRequest] = useState(0)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    setLoadingError('')
    setLookups(null)
    Promise.all([
      getCategories(controller.signal),
      getPriorities(controller.signal),
    ])
      .then(([categories, priorities]) => setLookups({ categories, priorities }))
      .catch((error) => {
        if (error.name !== 'AbortError') {
          setLoadingError(error.message)
        }
      })
    return () => controller.abort()
  }, [lookupRequest])

  function validate() {
    const next = {}
    if (values.title.trim().length < 5) next.title = 'Enter at least 5 characters.'
    if (values.description.trim().length < 10) next.description = 'Enter at least 10 characters.'
    if (!values.ticketCategoryId) next.ticketCategoryId = 'Select a category.'
    if (!values.ticketPriorityId) next.ticketPriorityId = 'Select a priority.'
    setErrors(next)
    return Object.keys(next).length === 0
  }

  async function submit(event) {
    event.preventDefault()
    if (!validate() || saving) return
    try {
      setSaving(true); setErrors({})
      await onSubmit({
        title: values.title.trim(),
        description: values.description.trim(),
        ticketCategoryId: Number(values.ticketCategoryId),
        ticketPriorityId: Number(values.ticketPriorityId),
      })
    } catch (error) {
      setErrors({ form: error.message })
    } finally { setSaving(false) }
  }

  if (loadingError) {
    return (
      <ErrorState
        message={loadingError}
        onRetry={() => setLookupRequest((current) => current + 1)}
      />
    )
  }
  if (!lookups) return <LoadingState message="Loading ticket options…" />

  return (
    <form className="ticket-form panel" onSubmit={submit} noValidate>
      {errors.form && <div className="inline-alert inline-alert--error" role="alert">{errors.form}</div>}
      <label><span>Title <b aria-hidden="true">*</b></span><small>Briefly summarize the issue.</small>
        <input maxLength="200" value={values.title} onChange={(e) => setValues({ ...values, title: e.target.value })} aria-describedby="title-error title-count" />
        <span className="field-meta"><em id="title-error">{errors.title}</em><small id="title-count">{values.title.length}/200</small></span>
      </label>
      <label><span>Description <b aria-hidden="true">*</b></span><small>Include what happened, when it started, and any troubleshooting attempted.</small>
        <textarea rows="8" maxLength="5000" value={values.description} onChange={(e) => setValues({ ...values, description: e.target.value })} />
        <span className="field-meta"><em>{errors.description}</em><small>{values.description.length}/5000</small></span>
      </label>
      <div className="form-grid">
        <label><span>Category <b>*</b></span><select value={values.ticketCategoryId} onChange={(e) => setValues({ ...values, ticketCategoryId: e.target.value })}><option value="">Select category</option>{lookups.categories.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select><em>{errors.ticketCategoryId}</em></label>
        <label><span>Priority <b>*</b></span><select value={values.ticketPriorityId} onChange={(e) => setValues({ ...values, ticketPriorityId: e.target.value })}><option value="">Select priority</option>{lookups.priorities.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select><em>{errors.ticketPriorityId}</em></label>
      </div>
      <div className="form-actions"><button type="button" className="button button--secondary" onClick={onCancel} disabled={saving}>Cancel</button><button className="button button--primary" disabled={saving}>{saving ? 'Saving…' : submitLabel}</button></div>
    </form>
  )
}

export default TicketForm
