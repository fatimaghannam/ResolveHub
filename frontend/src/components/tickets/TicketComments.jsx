import { LockKeyhole } from 'lucide-react'

const MAX_COMMENT_LENGTH = 5000

function TicketComments({
  comments,
  helperText,
  message,
  onMessageChange,
  visibility = 'Public',
  onVisibilityChange,
  onSubmit,
  isSubmitting,
  canComment,
  publicOnly = false,
  readOnlyMessage,
  formatTimestamp,
}) {
  const trimmedMessage = message.trim()

  return (
    <section className="panel dashboard-section comments-panel">
      <div className="panel__heading">
        <div>
          <h2>Comments</h2>
          <p>{helperText}</p>
        </div>
      </div>

      {comments.length === 0
        ? <p className="comments-empty">No comments have been added yet.</p>
        : (
          <div className="comments-list">
            {comments.map((comment) => (
              <article className="comment-item" key={comment.id}>
                <header className="comment-item__header">
                  <div className="comment-item__identity">
                    <strong>{comment.authorName}</strong>
                    {comment.authorRole && (
                      <span className="comment-item__role">{comment.authorRole}</span>
                    )}
                    <span className={`badge comment-visibility--${comment.visibility.toLowerCase()}`}>
                      {comment.visibility === 'Private' && (
                        <LockKeyhole size={12} aria-hidden="true" />
                      )}
                      {comment.visibility}
                    </span>
                  </div>
                  <time dateTime={comment.createdDate}>
                    {formatTimestamp(comment.createdDate)}
                  </time>
                </header>
                <p className="comment-item__message">{comment.content}</p>
              </article>
            ))}
          </div>
        )}

      {canComment
        ? (
          <form className="comment-composer" onSubmit={onSubmit}>
            <label htmlFor="ticket-comment">Add comment</label>
            <textarea
              id="ticket-comment"
              value={message}
              onChange={(event) => onMessageChange(event.target.value)}
              maxLength={MAX_COMMENT_LENGTH}
              disabled={isSubmitting}
            />
            <div className="comment-composer__count" aria-live="polite">
              {message.length.toLocaleString()} / {MAX_COMMENT_LENGTH.toLocaleString()}
            </div>

            {publicOnly
              ? (
                <p className="comment-composer__public-note">
                  <span className="badge comment-visibility--public">Public</span>
                  Visible to everyone with access to this ticket.
                </p>
              )
              : (
                <fieldset className="comment-visibility">
                  <legend>Visibility</legend>
                  <label>
                    <input
                      type="radio"
                      name="comment-visibility"
                      value="Public"
                      checked={visibility === 'Public'}
                      onChange={(event) => onVisibilityChange(event.target.value)}
                      disabled={isSubmitting}
                    />
                    <span>
                      <strong>Public</strong>
                      <small>Visible to everyone with ticket access</small>
                    </span>
                  </label>
                  <label>
                    <input
                      type="radio"
                      name="comment-visibility"
                      value="Private"
                      checked={visibility === 'Private'}
                      onChange={(event) => onVisibilityChange(event.target.value)}
                      disabled={isSubmitting}
                    />
                    <span>
                      <strong>Private</strong>
                      <small>Visible only to requester and assigned Agent</small>
                    </span>
                  </label>
                </fieldset>
              )}

            <button
              className="button button--secondary"
              type="submit"
              disabled={!trimmedMessage || isSubmitting}
            >
              {isSubmitting ? 'Adding…' : 'Add Comment'}
            </button>
          </form>
        )
        : readOnlyMessage && <p className="comments-read-only">{readOnlyMessage}</p>}
    </section>
  )
}

export default TicketComments
