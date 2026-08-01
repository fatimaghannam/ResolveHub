import {
  ChevronDown,
  ChevronUp,
  FileText,
  Globe2,
  LockKeyhole,
  MessageSquare,
  MoreHorizontal,
  Pencil,
  Paperclip,
  Reply,
  Trash2,
  X,
} from 'lucide-react'
import { useEffect, useId, useMemo, useRef, useState } from 'react'
import {
  addComment,
  deleteComment,
  editComment,
  getComments,
  replyToComment,
  downloadCommentAttachment,
} from '../../services/commentService.js'

const MAX_COMMENT_LENGTH = 5000
const COMMENT_PAGE_SIZE = 5

function initials(name) {
  return name.split(/\s+/).slice(0, 2).map((part) => part[0]).join('').toUpperCase()
}

function formatFileSize(bytes) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function relativeTimestamp(value, fallback) {
  const date = new Date(value)
  const now = new Date()
  const startToday = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const startDate = new Date(date.getFullYear(), date.getMonth(), date.getDate())
  const dayDifference = Math.round((startToday - startDate) / 86400000)
  const time = new Intl.DateTimeFormat(undefined, {
    hour: 'numeric',
    minute: '2-digit',
  }).format(date)
  if (dayDifference === 0) return `Today at ${time}`
  if (dayDifference === 1) return `Yesterday at ${time}`
  return fallback(value)
}

function VisibilityBadge({ visibility }) {
  const isPrivate = visibility === 'Private'
  const Icon = isPrivate ? LockKeyhole : Globe2
  return <span className={`comment-visibility-badge comment-visibility-badge--${visibility.toLowerCase()}`}>
    <Icon size={12} aria-hidden="true" />
    {visibility}
  </span>
}

function CommentFilters({ selected, counts, canViewPrivate, onChange }) {
  const options = ['All', 'Public', ...(canViewPrivate ? ['Private'] : [])]
  return <div className="comment-filters" role="group" aria-label="Filter comments">
    {options.map((option) => <button
      key={option}
      type="button"
      className={selected === option ? 'is-active' : ''}
      aria-pressed={selected === option}
      onClick={() => onChange(option)}
    >
      {option}<span>{counts[option]}</span>
    </button>)}
  </div>
}

function CommentActionsMenu({ authorName, canEdit, canDelete, onEdit, onDelete }) {
  const [open, setOpen] = useState(false)
  const menuRef = useRef(null)

  useEffect(() => {
    if (!open) return undefined
    function close(event) {
      if (event.type === 'keydown' && event.key !== 'Escape') return
      if (event.type === 'pointerdown' && menuRef.current?.contains(event.target)) return
      setOpen(false)
    }
    document.addEventListener('pointerdown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('pointerdown', close)
      document.removeEventListener('keydown', close)
    }
  }, [open])

  return <div className="comment-actions-menu" ref={menuRef}>
    <button
      type="button"
      className="comment-menu-trigger"
      aria-label={`Actions for ${authorName}'s comment`}
      aria-haspopup="menu"
      aria-expanded={open}
      onClick={() => setOpen((value) => !value)}
    ><MoreHorizontal size={18} aria-hidden="true" /></button>
    {open && <div className="comment-menu" role="menu">
      {canEdit && <button type="button" role="menuitem" onClick={() => { setOpen(false); onEdit() }}><Pencil size={14} />Edit comment</button>}
      {canDelete && <button type="button" role="menuitem" className="comment-menu__danger" onClick={() => { setOpen(false); onDelete() }}><Trash2 size={14} />Delete comment</button>}
    </div>}
  </div>
}

function ReplyComposer({ comment, busy, onCancel, onSubmit }) {
  const [message, setMessage] = useState('')
  const [files, setFiles] = useState([])
  const [validation, setValidation] = useState('')
  const fileInputRef = useRef(null)

  async function submit(event) {
    event.preventDefault()
    if (!message.trim() || busy) return
    await onSubmit(message.trim(), files)
  }

  function selectFiles(event) {
    const selected = Array.from(event.target.files ?? [])
    const invalid = selected.find((file) => !/\.(png|jpe?g|gif|webp|pdf|docx?|xlsx?|txt|zip)$/i.test(file.name) || file.size <= 0 || file.size > 10 * 1024 * 1024)
    if (invalid) setValidation(`${invalid.name} is unsupported, empty, or exceeds the 10 MB limit.`)
    else {
      setFiles((current) => [...current, ...selected.filter((file) => !current.some((item) => item.name === file.name && item.size === file.size))].slice(0, 5))
      setValidation('')
    }
    event.target.value = ''
  }

  return <form className="comment-reply-form" onSubmit={submit}>
    <div className="comment-reply-form__heading">
      <strong>Replying to {comment.authorName}</strong>
      <button type="button" onClick={onCancel}>Cancel</button>
    </div>
    <textarea
      value={message}
      maxLength={MAX_COMMENT_LENGTH}
      onChange={(event) => setMessage(event.target.value)}
      placeholder="Write a reply…"
      aria-label={`Reply to ${comment.authorName}`}
      disabled={busy}
      autoFocus
    />
    {validation && <p className="comment-validation" role="alert">{validation}</p>}
    {files.length > 0 && <div className="comment-pending-files">{files.map((file) => <div key={`${file.name}-${file.size}`}><FileText size={14} /><span>{file.name}</span><small>{formatFileSize(file.size)}</small><button type="button" onClick={() => setFiles((current) => current.filter((item) => item !== file))} aria-label={`Remove ${file.name}`}><X size={14} /></button></div>)}</div>}
    <div className="comment-reply-form__footer">
      <span><input ref={fileInputRef} type="file" multiple hidden accept=".png,.jpg,.jpeg,.gif,.webp,.pdf,.doc,.docx,.xls,.xlsx,.txt,.zip" onChange={selectFiles} /><button type="button" className="comment-attach-button" onClick={() => fileInputRef.current?.click()} aria-label="Attach files" title="Attach files"><Paperclip size={15} /></button>{comment.visibility === 'Private' ? <LockKeyhole size={12} /> : <Globe2 size={12} />}This reply will {comment.visibility === 'Private' ? 'remain private' : 'be public'}.</span>
      <button className="button button--primary" type="submit" disabled={!message.trim() || busy}>{busy ? 'Adding…' : 'Add Reply'}</button>
    </div>
  </form>
}

function CommentCard({ comment, replies, formatTimestamp, onReply, onEdit, onDelete, onDownload }) {
  const [editing, setEditing] = useState(false)
  const [editValue, setEditValue] = useState(comment.content)
  const [replying, setReplying] = useState(false)
  const [busy, setBusy] = useState(false)
  const [repliesExpanded, setRepliesExpanded] = useState(false)
  const displayedReplies = replies.length > 2 && !repliesExpanded
    ? [replies[0], replies[replies.length - 1]]
    : replies
  const hiddenReplyCount = Math.max(0, replies.length - 2)

  async function saveEdit(event) {
    event.preventDefault()
    if (!editValue.trim() || busy) return
    setBusy(true)
    try {
      await onEdit(comment.id, editValue.trim())
      setEditing(false)
    } finally { setBusy(false) }
  }

  async function submitReply(value, files) {
    setBusy(true)
    try {
      await onReply(comment.id, value, files)
      setReplying(false)
    } finally { setBusy(false) }
  }

  if (comment.isDeleted) return <div className="comment-thread comment-thread--deleted">
    <article className="comment-deleted-placeholder">
      <span className="comment-avatar comment-avatar--deleted" aria-hidden="true">{initials(comment.authorName)}</span>
      <div><p><strong>{comment.authorName}</strong><span aria-hidden="true"> · </span><time dateTime={comment.createdDate}>{relativeTimestamp(comment.createdDate, formatTimestamp)}</time></p><em>{replies.length > 0 ? 'Original comment deleted' : 'Comment deleted'}</em></div>
    </article>
    {replies.length > 0 && <div className="comment-replies" aria-label={`Replies to deleted comment by ${comment.authorName}`}>
      {displayedReplies.map((reply, index) => <div key={reply.id} className="comment-reply-row">
        {index === 1 && hiddenReplyCount > 0 && !repliesExpanded && <button type="button" className="comment-replies-toggle comment-replies-toggle--between" onClick={() => setRepliesExpanded(true)}><ChevronDown size={14} />View {hiddenReplyCount} more {hiddenReplyCount === 1 ? 'reply' : 'replies'}</button>}
        <CommentCard comment={reply} replies={[]} formatTimestamp={formatTimestamp} onReply={onReply} onEdit={onEdit} onDelete={onDelete} onDownload={onDownload} />
      </div>)}
      {repliesExpanded && replies.length > 2 && <button type="button" className="comment-replies-toggle" onClick={() => setRepliesExpanded(false)}><ChevronUp size={14} />Hide replies</button>}
    </div>}
  </div>

  return <div className="comment-thread">
    <article className={`comment-card ${comment.visibility === 'Private' ? 'comment-card--private' : ''} ${comment.isDeleted ? 'comment-card--deleted' : ''}`}>
      <header className="comment-card__header">
        <span className="comment-avatar" aria-hidden="true">{initials(comment.authorName)}</span>
        <div className="comment-card__author">
          <div className="comment-card__author-line">
            <strong>{comment.authorName}</strong>
            <span className={`comment-role-badge comment-role-badge--${comment.authorRole.toLowerCase().replaceAll(' ', '-')}`}>{comment.authorRole === 'IT Support Agent' ? 'IT Agent' : comment.authorRole}</span>
            {comment.isTicketCreator && <span className="comment-context-badge">Ticket Creator</span>}
            {comment.isAssignedAgent && <span className="comment-context-badge comment-context-badge--agent">Assigned Agent</span>}
          </div>
          <time dateTime={comment.createdDate}>{relativeTimestamp(comment.createdDate, formatTimestamp)}{comment.isEdited && !comment.isDeleted && <span> · edited</span>}</time>
        </div>
        <div className="comment-card__controls">
          <VisibilityBadge visibility={comment.visibility} />
          {(comment.canEdit || comment.canDelete) && !comment.isDeleted && <CommentActionsMenu authorName={comment.authorName} canEdit={comment.canEdit} canDelete={comment.canDelete} onEdit={() => setEditing(true)} onDelete={() => onDelete(comment)} />}
        </div>
      </header>

      {editing ? <form className="comment-inline-form" onSubmit={saveEdit}>
        <textarea value={editValue} maxLength={MAX_COMMENT_LENGTH} onChange={(event) => setEditValue(event.target.value)} disabled={busy} autoFocus />
        <div className="comment-inline-form__actions"><span>{editValue.length} / {MAX_COMMENT_LENGTH}</span><button type="button" className="button button--secondary" onClick={() => setEditing(false)} disabled={busy}>Cancel</button><button type="submit" className="button button--primary" disabled={!editValue.trim() || busy}>{busy ? 'Saving…' : 'Save'}</button></div>
      </form> : <><p className="comment-card__message">{comment.content}</p>{comment.attachments?.length > 0 && <div className="comment-attachments">{comment.attachments.map((attachment) => <button type="button" key={attachment.id} onClick={() => onDownload(comment.id, attachment)}><FileText size={14} /><span>{attachment.fileName}</span><small>{formatFileSize(attachment.fileSizeBytes)}</small></button>)}</div>}</>}

      {!editing && !comment.isDeleted && !comment.parentCommentId && <footer className="comment-card__actions">
        <button type="button" onClick={() => setReplying((value) => !value)} aria-expanded={replying}><Reply size={14} />Reply</button>
      </footer>}
      {replying && <ReplyComposer comment={comment} busy={busy} onCancel={() => setReplying(false)} onSubmit={submitReply} />}
    </article>
    {replies.length > 0 && <div className="comment-replies" aria-label={`Replies to ${comment.authorName}`}>
      {displayedReplies.map((reply, index) => <div key={reply.id} className="comment-reply-row">
        {index === 1 && hiddenReplyCount > 0 && !repliesExpanded && <button type="button" className="comment-replies-toggle comment-replies-toggle--between" onClick={() => setRepliesExpanded(true)} aria-label={`View ${hiddenReplyCount} more replies`}><ChevronDown size={14} />View {hiddenReplyCount} more {hiddenReplyCount === 1 ? 'reply' : 'replies'}</button>}
        <CommentCard comment={reply} replies={[]} formatTimestamp={formatTimestamp} onReply={onReply} onEdit={onEdit} onDelete={onDelete} onDownload={onDownload} />
      </div>)}
      {repliesExpanded && replies.length > 2 && <button type="button" className="comment-replies-toggle" onClick={() => setRepliesExpanded(false)} aria-label={`Hide replies to ${comment.authorName}`}><ChevronUp size={14} />Hide replies</button>}
    </div>}
  </div>
}

function DeletedCommentsGroup({ comments, formatTimestamp }) {
  const [expanded, setExpanded] = useState(false)
  if (comments.length === 1)
    return <CommentCard comment={comments[0]} replies={[]} formatTimestamp={formatTimestamp} />
  return <div className="deleted-comments-group">
    <button type="button" className="deleted-comments-group__toggle" aria-expanded={expanded} onClick={() => setExpanded((value) => !value)}>
      {expanded ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
      {comments.length} deleted comments
    </button>
    {expanded && <div className="deleted-comments-group__items">{comments.map((comment) => <CommentCard key={comment.id} comment={comment} replies={[]} formatTimestamp={formatTimestamp} />)}</div>}
  </div>
}

function CommentSkeletons() {
  return <div className="comments-list comments-skeletons" aria-label="Loading comments" aria-busy="true">
    {[1, 2].map((item) => <div className="comment-skeleton" key={item}><span /><div><i /><i /><i /></div></div>)}
  </div>
}

function CommentComposer({ canViewPrivate, onSubmit }) {
  const [message, setMessage] = useState('')
  const [visibility, setVisibility] = useState('Public')
  const [submitting, setSubmitting] = useState(false)
  const [validation, setValidation] = useState('')
  const [files, setFiles] = useState([])
  const textareaRef = useRef(null)
  const fileInputRef = useRef(null)

  function updateMessage(value) {
    setMessage(value)
    setValidation('')
    requestAnimationFrame(() => {
      const textarea = textareaRef.current
      if (!textarea) return
      textarea.style.height = 'auto'
      textarea.style.height = `${Math.min(textarea.scrollHeight, 240)}px`
    })
  }

  function selectFiles(event) {
    const allowed = /\.(png|jpe?g|gif|webp|pdf|docx?|xlsx?|txt|zip)$/i
    const selected = Array.from(event.target.files ?? [])
    const invalid = selected.find((file) => !allowed.test(file.name) || file.size <= 0 || file.size > 10 * 1024 * 1024)
    if (invalid) {
      setValidation(`${invalid.name} is unsupported, empty, or exceeds the 10 MB limit.`)
      event.target.value = ''
      return
    }
    setFiles((current) => [...current, ...selected.filter((file) =>
      !current.some((existing) => existing.name === file.name && existing.size === file.size))].slice(0, 5))
    setValidation('')
    event.target.value = ''
  }

  async function submit(event) {
    event.preventDefault()
    const trimmed = message.trim()
    if (!trimmed || submitting) {
      if (!trimmed) setValidation('Enter a comment before submitting.')
      return
    }
    setSubmitting(true)
    setValidation('')
    try {
      await onSubmit({ message: trimmed, visibility, files })
      setMessage('')
      setFiles([])
      if (textareaRef.current) textareaRef.current.style.height = 'auto'
    } catch {
      // Preserve the draft so the user can retry.
    } finally { setSubmitting(false) }
  }

  return <form className="comment-composer" onSubmit={submit}>
    <div className="comment-composer__heading"><label htmlFor="ticket-comment">Write an update or ask a question</label><span>{message.length.toLocaleString()} / {MAX_COMMENT_LENGTH.toLocaleString()}</span></div>
    <textarea ref={textareaRef} id="ticket-comment" value={message} onChange={(event) => updateMessage(event.target.value)} maxLength={MAX_COMMENT_LENGTH} placeholder="Write an update or ask a question…" disabled={submitting} rows={1} />
    {validation && <p className="comment-validation" role="alert">{validation}</p>}
    {files.length > 0 && <div className="comment-pending-files">{files.map((file) => <div key={`${file.name}-${file.size}`}><FileText size={14} /><span>{file.name}</span><small>{formatFileSize(file.size)}</small><button type="button" onClick={() => setFiles((current) => current.filter((item) => item !== file))} aria-label={`Remove ${file.name}`}><X size={14} /></button></div>)}</div>}
    <div className="comment-composer__toolbar"><div className="comment-composer__tools"><input ref={fileInputRef} type="file" multiple hidden accept=".png,.jpg,.jpeg,.gif,.webp,.pdf,.doc,.docx,.xls,.xlsx,.txt,.zip" onChange={selectFiles} /><button type="button" onClick={() => fileInputRef.current?.click()} aria-label="Attach files" title="Attach files"><Paperclip size={16} /></button></div><fieldset className={`comment-visibility ${canViewPrivate ? '' : 'comment-visibility--single'}`}><legend>Visibility</legend>{['Public', ...(canViewPrivate ? ['Private'] : [])].map((option) => {
      const Icon = option === 'Private' ? LockKeyhole : Globe2
      return <label key={option}><input type="radio" name="comment-visibility" value={option} checked={visibility === option} onChange={(event) => setVisibility(event.target.value)} disabled={submitting} /><Icon size={14} aria-hidden="true" /><strong>{option}</strong></label>
    })}</fieldset><button className="button button--primary comment-submit" type="submit" disabled={!message.trim() || submitting}>{submitting && <span className="button-spinner" aria-hidden="true" />}{submitting ? 'Sending…' : 'Send'}</button></div>
  </form>
}

function TicketComments({ comments: initialComments = [], endpoint, canViewPrivate, canComment, readOnlyMessage, formatTimestamp, onNotify }) {
  const [expanded, setExpanded] = useState(false)
  const [comments, setComments] = useState(initialComments)
  const [filter, setFilter] = useState('All')
  const [page, setPage] = useState(1)
  const [pageInfo, setPageInfo] = useState({
    totalThreads: initialComments.filter((comment) => !comment.parentCommentId).length,
    totalVisibleComments: initialComments.length,
    publicCount: initialComments.filter((comment) => comment.visibility === 'Public').length,
    privateCount: initialComments.filter((comment) => comment.visibility === 'Private').length,
    hasMore: false,
  })
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [loadVersion, setLoadVersion] = useState(0)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [deleting, setDeleting] = useState(false)
  const [showBackToLatest, setShowBackToLatest] = useState(false)
  const sectionRef = useRef(null)
  const latestRef = useRef(null)
  const contentId = useId()

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setLoadError('')
    getComments(endpoint, { visibility: filter, page, pageSize: COMMENT_PAGE_SIZE }, controller.signal).then((response) => {
      if (!controller.signal.aborted) {
        setComments((current) => page === 1 ? response.items : [...current, ...response.items])
        setPageInfo(response)
      }
    }).catch((requestError) => {
      if (requestError.name !== 'AbortError') setLoadError(requestError.message)
    }).finally(() => {
      if (!controller.signal.aborted) setLoading(false)
    })
    return () => controller.abort()
  }, [endpoint, filter, page, loadVersion])

  useEffect(() => {
    function updateBackToLatest() {
      const section = sectionRef.current?.getBoundingClientRect()
      const latest = latestRef.current?.getBoundingClientRect()
      setShowBackToLatest(Boolean(section && latest && section.top < -320 && latest.top > window.innerHeight + 180))
    }
    updateBackToLatest()
    window.addEventListener('scroll', updateBackToLatest, { passive: true })
    window.addEventListener('resize', updateBackToLatest)
    return () => {
      window.removeEventListener('scroll', updateBackToLatest)
      window.removeEventListener('resize', updateBackToLatest)
    }
  }, [comments])

  const counts = useMemo(() => ({
    All: pageInfo.publicCount + pageInfo.privateCount,
    Public: pageInfo.publicCount,
    Private: pageInfo.privateCount,
  }), [pageInfo])
  const roots = comments.filter((comment) => !comment.parentCommentId)
  const repliesFor = (id) => comments.filter((comment) => comment.parentCommentId === id)
  const timelineGroups = []
  roots.forEach((comment) => {
      const standaloneDeleted = comment.isDeleted &&
        !comments.some((reply) => reply.parentCommentId === comment.id)
      const previous = timelineGroups[timelineGroups.length - 1]
      if (standaloneDeleted && previous?.type === 'deleted') previous.comments.push(comment)
      else timelineGroups.push(standaloneDeleted
        ? { type: 'deleted', comments: [comment] }
        : { type: 'thread', comment })
  })

  function changeFilter(nextFilter) {
    setFilter(nextFilter)
    setPage(1)
    setComments([])
  }

  function notify(type, title, detail) {
    onNotify?.(type, title, detail)
  }

  function toggleComments() {
    if (!expanded) {
      setExpanded(true)
      return
    }

    setExpanded(false)
    requestAnimationFrame(() => {
      sectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    })
  }

  async function create(request) {
    try {
      const created = await addComment(endpoint, request)
      if (filter === 'All' || filter === created.visibility)
        setComments((current) => [...current, created])
      setPageInfo((current) => ({ ...current,
        totalThreads: current.totalThreads + 1,
        totalVisibleComments: current.totalVisibleComments + 1,
        publicCount: current.publicCount + (created.visibility === 'Public' ? 1 : 0),
        privateCount: current.privateCount + (created.visibility === 'Private' ? 1 : 0),
      }))
      notify('success', 'Comment Added', 'Comment added successfully.')
    } catch (requestError) {
      notify('error', 'Unable to Add Comment', requestError.message)
      throw requestError
    }
  }

  async function reply(commentId, value, files = []) {
    try {
      const created = await replyToComment(endpoint, commentId, value, files)
      setComments((current) => [...current, created])
      setPageInfo((current) => ({ ...current,
        totalVisibleComments: current.totalVisibleComments + 1,
        publicCount: current.publicCount + (created.visibility === 'Public' ? 1 : 0),
        privateCount: current.privateCount + (created.visibility === 'Private' ? 1 : 0),
      }))
      notify('success', 'Reply Added', 'Reply added successfully.')
    } catch (requestError) {
      notify('error', 'Unable to Add Reply', requestError.message)
      throw requestError
    }
  }

  async function edit(commentId, value) {
    try {
      const updated = await editComment(endpoint, commentId, value)
      setComments((current) => current.map((item) => item.id === commentId ? updated : item))
      notify('success', 'Comment Updated', 'Comment updated successfully.')
    } catch (requestError) {
      notify('error', 'Unable to Update Comment', requestError.message)
      throw requestError
    }
  }

  async function remove() {
    if (!deleteTarget || deleting) return
    setDeleting(true)
    try {
      await deleteComment(endpoint, deleteTarget.id)
      setComments((current) => current.filter((item) => item.id !== deleteTarget.id))
      setPageInfo((current) => ({ ...current,
        totalThreads: current.totalThreads - (deleteTarget.parentCommentId ? 0 : 1),
        totalVisibleComments: Math.max(0, current.totalVisibleComments - 1),
        publicCount: Math.max(0, current.publicCount - (deleteTarget.visibility === 'Public' ? 1 : 0)),
        privateCount: Math.max(0, current.privateCount - (deleteTarget.visibility === 'Private' ? 1 : 0)),
      }))
      notify('success', 'Comment Deleted', 'Comment deleted successfully.')
      setDeleteTarget(null)
      setPage(1)
      setLoadVersion((value) => value + 1)
    } catch (requestError) {
      notify('error', 'Unable to Delete Comment', requestError.message)
      if (requestError.status === 409) {
        setPage(1)
        setLoadVersion((value) => value + 1)
      }
      setDeleteTarget(null)
    } finally {
      setDeleting(false)
    }
  }

  const emptyTitle = filter === 'Private' ? 'No private comments yet.' : filter === 'Public' ? 'No public comments yet.' : 'No comments yet.'
  const commentCount = pageInfo.totalVisibleComments

  return <section className={`panel dashboard-section comments-panel ${expanded ? 'comments-panel--expanded' : 'comments-panel--collapsed'}`} ref={sectionRef}>
    <div className="comments-heading">
      <div><h2>Comments</h2><p>Discuss updates and questions related to this ticket.</p></div>
      <button type="button" className="comments-toggle" aria-expanded={expanded} aria-controls={contentId} onClick={toggleComments}>
        <span>{expanded ? 'Hide Comments' : `View Comments (${commentCount})`}</span>
        {expanded ? <ChevronUp size={17} aria-hidden="true" /> : <ChevronDown size={17} aria-hidden="true" />}
      </button>
    </div>
    <p className="comments-count" aria-live="polite">{commentCount.toLocaleString()} {commentCount === 1 ? 'comment' : 'comments'}</p>
    <div className="comments-collapsible" aria-hidden={!expanded}>
      <div id={contentId} className="comments-collapsible__inner" inert={!expanded}>
    <CommentFilters selected={filter} counts={counts} canViewPrivate={canViewPrivate} onChange={changeFilter} />
    {!loading && !loadError && <p className="comments-summary">Showing {comments.length} of {pageInfo.totalVisibleComments} visible comments</p>}
    {loading && page === 1 ? <CommentSkeletons /> : loadError ? <div className="comments-load-error" role="alert"><strong>Comments could not be loaded.</strong><span>{loadError}</span><button type="button" onClick={() => setLoadVersion((value) => value + 1)}>Try again</button></div> : roots.length === 0 ? <div className="comments-empty"><MessageSquare size={22} /><strong>{emptyTitle}</strong><span>Start the conversation with an update or question.</span></div> : <div className="comments-list">{timelineGroups.map((group) => group.type === 'deleted' ? <DeletedCommentsGroup key={`deleted-${group.comments[0].id}`} comments={group.comments} formatTimestamp={formatTimestamp} /> : <CommentCard key={group.comment.id} comment={group.comment} replies={repliesFor(group.comment.id)} formatTimestamp={formatTimestamp} onReply={reply} onEdit={edit} onDelete={setDeleteTarget} onDownload={(commentId, attachment) => downloadCommentAttachment(endpoint, commentId, attachment).catch((error) => notify('error', 'Unable to Download File', error.message))} />)}</div>}
    {pageInfo.hasMore && <div className="comments-load-more"><button type="button" className="button button--secondary" onClick={() => setPage((value) => value + 1)} disabled={loading}>{loading ? 'Loading…' : 'Load more comments'}</button></div>}
    <div ref={latestRef}>{canComment ? <CommentComposer canViewPrivate={canViewPrivate} onSubmit={create} /> : readOnlyMessage && <p className="comments-read-only">{readOnlyMessage}</p>}</div>
    {showBackToLatest && <button type="button" className="comments-back-latest" onClick={() => latestRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })}><ChevronDown size={15} />Back to latest</button>}
    {deleteTarget && <><div className="dialog-backdrop" onClick={() => !deleting && setDeleteTarget(null)} aria-hidden="true" /><section className="dialog" role="dialog" aria-modal="true" aria-labelledby="delete-comment-title" aria-describedby="delete-comment-description"><h2 id="delete-comment-title">Delete comment?</h2><p id="delete-comment-description">This action cannot be undone.</p><div className="dialog__actions"><button type="button" className="button button--secondary" onClick={() => setDeleteTarget(null)} disabled={deleting}>Cancel</button><button type="button" className="button button--danger" onClick={remove} disabled={deleting} autoFocus>{deleting ? 'Deleting…' : 'Delete'}</button></div></section></>}
      </div>
    </div>
  </section>
}

export default TicketComments
