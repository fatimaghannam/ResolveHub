import { useCallback, useEffect, useRef, useState } from 'react'
import { Bot, Paperclip, Save, Send, Upload } from 'lucide-react'
import { analyzeTicket } from '../../services/aiService.js'
import AiSuggestionCard from '../ai/AiSuggestionCard.jsx'
import { getCategories, getPriorities } from '../../services/ticketService.js'
import { ErrorState, LoadingState } from '../common/States.jsx'
import Toast from '../common/Toast.jsx'

const emptyValues = {
  title: '',
  description: '',
  ticketCategoryId: '',
  ticketPriorityId: '',
}
const allowedExtensions = ['png', 'jpg', 'jpeg', 'pdf', 'docx', 'txt', 'log', 'zip']
const maxFileSize = 10 * 1024 * 1024

function formatBytes(value) {
  return value < 1024 * 1024
    ? `${Math.ceil(value / 1024)} KB`
    : `${(value / 1024 / 1024).toFixed(1)} MB`
}

function TicketForm({
  mode = 'create',
  initialValues,
  existingAttachments = [],
  submitLabel,
  onSubmit,
  onSaveDraft,
  onDeleteAttachment,
  onCancel,
}) {
  const [values, setValues] = useState({ ...emptyValues, ...initialValues })
  const [lookups, setLookups] = useState(null)
  const [files, setFiles] = useState([])
  const [fileErrors, setFileErrors] = useState([])
  const [errors, setErrors] = useState({})
  const [loadingError, setLoadingError] = useState('')
  const [lookupRequest, setLookupRequest] = useState(0)
  const [saving, setSaving] = useState(false)
  const [savingDraft, setSavingDraft] = useState(false)
  const [toast, setToast] = useState(null)
  const [aiSuggestion, setAiSuggestion] = useState(null)
  const [aiBusy, setAiBusy] = useState(false)
  const [aiError, setAiError] = useState('')
  const dismissToast = useCallback(() => setToast(null), [])
  const fileInput = useRef(null)

  useEffect(() => {
    const controller = new AbortController()
    setLoadingError('')
    setLookups(null)
    Promise.all([getCategories(controller.signal), getPriorities(controller.signal)])
      .then(([categories, priorities]) => {
        if (!controller.signal.aborted) {
          setLookups({ categories, priorities })
        }
      })
      .catch((error) => {
        if (error.name !== 'AbortError' && !controller.signal.aborted) {
          setLoadingError('Ticket options could not be loaded.')
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

  function payload() {
    return {
      title: values.title.trim(),
      description: values.description.trim(),
      ticketCategoryId: values.ticketCategoryId
        ? Number(values.ticketCategoryId)
        : null,
      ticketPriorityId: values.ticketPriorityId
        ? Number(values.ticketPriorityId)
        : null,
    }
  }

  function addFiles(incoming) {
    const accepted = []
    const nextErrors = []
    const remaining = 5 - existingAttachments.length - files.length
    Array.from(incoming).forEach((file) => {
      const extension = file.name.split('.').pop()?.toLowerCase()
      if (!allowedExtensions.includes(extension)) {
        nextErrors.push(`${file.name}: file type is not allowed.`)
      } else if (file.size > maxFileSize) {
        nextErrors.push(`${file.name}: exceeds the 10 MB limit.`)
      } else if (accepted.length >= remaining) {
        nextErrors.push(`${file.name}: a ticket may have at most 5 files.`)
      } else {
        accepted.push(file)
      }
    })
    setFiles((current) => [...current, ...accepted])
    setFileErrors(nextErrors)
  }

  async function analyze() {
    if (values.title.trim().length < 5 || values.description.trim().length < 10 || aiBusy) {
      setAiError('Enter a title and at least 10 description characters first.'); return
    }
    try { setAiBusy(true); setAiError(''); setAiSuggestion(await analyzeTicket(values.title.trim(), values.description.trim())) }
    catch (error) { setAiError(error.message) } finally { setAiBusy(false) }
  }

  async function submit(event) {
    event.preventDefault()
    if (!validate() || saving) return
    try {
      setSaving(true)
      setErrors({})
      await onSubmit(payload(), files)
    } catch (error) {
      setErrors({ form: error.message })
    } finally {
      setSaving(false)
    }
  }

  async function saveDraft() {
    if (!onSaveDraft || savingDraft) return
    try {
      setSavingDraft(true)
      setErrors({})
      await onSaveDraft(payload())
      setToast({
        id: Date.now(),
        type: 'success',
        title: 'Draft Saved',
        message: 'Your ticket draft was saved successfully.',
      })
    } catch (error) {
      setErrors({ form: error.message })
    } finally {
      setSavingDraft(false)
    }
  }

  if (loadingError) {
    return <ErrorState message={loadingError} onRetry={() => setLookupRequest((value) => value + 1)} />
  }
  if (!lookups) return <LoadingState message="Loading ticket options…" />
  const currentCategoryMissing = mode === 'edit' && values.ticketCategoryId &&
    !lookups.categories.some((item) => Number(item.id) === Number(values.ticketCategoryId))

  return (
    <form className="ticket-form panel" onSubmit={submit} noValidate>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      {errors.form && <div className="inline-alert inline-alert--error" role="alert">{errors.form}</div>}
      <label>
        <span>Title <b aria-hidden="true">*</b></span>
        <input
          maxLength="200"
          placeholder="Brief description of your issue…"
          value={values.title}
          onChange={(event) => setValues({ ...values, title: event.target.value })}
          aria-describedby="title-error title-count"
        />
        <span className="field-meta"><em id="title-error">{errors.title}</em><small id="title-count">{values.title.length} / 200</small></span>
      </label>

      <label>
        <span>Description <b aria-hidden="true">*</b></span>
        <textarea
          rows="6"
          maxLength="5000"
          placeholder="Please describe what happened, when it started, any error messages you saw, and any troubleshooting you already tried…"
          value={values.description}
          onChange={(event) => setValues({ ...values, description: event.target.value })}
        />
        <span className="field-meta"><em>{errors.description}</em><small>{values.description.length} / 5000</small></span>
      </label>

      <div className="form-grid">
        <label>
          <span>Category <b aria-hidden="true">*</b></span>
          <select value={values.ticketCategoryId} onChange={(event) => setValues({ ...values, ticketCategoryId: event.target.value })}>
            <option value="">Select category…</option>
            {currentCategoryMissing && <option value={values.ticketCategoryId}>{values.ticketCategoryName} (Inactive)</option>}
            {lookups.categories.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
          <em>{errors.ticketCategoryId}</em>
        </label>
        <label>
          <span>Priority <b aria-hidden="true">*</b></span>
          <select value={values.ticketPriorityId} onChange={(event) => setValues({ ...values, ticketPriorityId: event.target.value })}>
            <option value="">Select priority…</option>
            {lookups.priorities.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
          <em>{errors.ticketPriorityId}</em>
        </label>
      </div>
      <div className="ai-analysis-action">
        <button type="button" className="button button--secondary" disabled={aiBusy} onClick={analyze}><Bot size={17} />{aiBusy ? 'Analyzing ticket…' : aiSuggestion ? 'Re-analyze' : 'Analyze with AI'}</button>
        {aiError && <span className="ai-error" role="alert">{aiError}</span>}
      </div>
      {aiSuggestion && <AiSuggestionCard suggestion={aiSuggestion} onApply={() => {
        setValues({ ...values, ticketCategoryId: String(aiSuggestion.suggestedCategoryId), ticketPriorityId: String(aiSuggestion.suggestedPriorityId) })
        setErrors({ ...errors, ticketCategoryId: '', ticketPriorityId: '' })
      }} />}

      <section className="attachment-field" aria-labelledby="attachment-title">
        <div><strong id="attachment-title">Attachments <small>(optional)</small></strong><p>PNG, JPG, PDF, DOCX, TXT, LOG or ZIP. Maximum 10 MB each — up to 5 files.</p></div>
        <button
          type="button"
          className="attachment-dropzone"
          onClick={() => fileInput.current?.click()}
          onDragOver={(event) => event.preventDefault()}
          onDrop={(event) => {
            event.preventDefault()
            addFiles(event.dataTransfer.files)
          }}
        >
          <Upload size={20} aria-hidden="true" />
          <span>Drop files here or click to browse</span>
        </button>
        <input
          ref={fileInput}
          className="visually-hidden"
          type="file"
          multiple
          accept=".png,.jpg,.jpeg,.pdf,.docx,.txt,.log,.zip"
          onChange={(event) => {
            addFiles(event.target.files)
            event.target.value = ''
          }}
        />
        {existingAttachments.map((file) => (
          <div className="attachment-row" key={file.id}>
            <Paperclip size={16} /><span>{file.fileName}</span><small>{formatBytes(file.fileSizeBytes)}</small>
            {onDeleteAttachment && <button type="button" onClick={async () => {
              try {
                await onDeleteAttachment(file.id)
              } catch (error) {
                setErrors({ form: error.message || 'The attachment could not be removed.' })
              }
            }}>Remove</button>}
          </div>
        ))}
        {files.map((file, index) => (
          <div className="attachment-row" key={`${file.name}-${file.lastModified}`}>
            <Paperclip size={16} /><span>{file.name}</span><small>{formatBytes(file.size)}</small>
            <button type="button" onClick={() => setFiles(files.filter((_, itemIndex) => itemIndex !== index))}>Remove</button>
          </div>
        ))}
        {fileErrors.map((message) => <em key={message}>{message}</em>)}
      </section>

      <div className="form-actions form-actions--split">
        <div>
          <button type="button" className="button button--secondary" onClick={onCancel} disabled={saving || savingDraft}>Cancel</button>
          {onSaveDraft && (
            <button type="button" className="button button--secondary" onClick={saveDraft} disabled={saving || savingDraft}>
              <Save size={17} />{savingDraft ? 'Saving…' : 'Save as Draft'}
            </button>
          )}
        </div>
        <button className="button button--primary" disabled={saving || savingDraft}>
          <Send size={17} />{saving ? 'Saving…' : submitLabel}
        </button>
      </div>
      {mode === 'draft' && <small className="draft-note">Files are uploaded when this draft is submitted as a ticket.</small>}
    </form>
  )
}

export default TicketForm
