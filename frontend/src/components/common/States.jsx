export function LoadingState({ message = 'Loading…' }) {
  return <div className="state-panel" role="status"><span className="dashboard-spinner" />{message}</div>
}

export function EmptyState({ title, message, action }) {
  return <div className="state-panel"><h2>{title}</h2><p>{message}</p>{action}</div>
}

export function ErrorState({ message, onRetry }) {
  return (
    <div className="state-panel state-panel--error" role="alert">
      <h2>Something went wrong</h2><p>{message}</p>
      {onRetry && <button className="button button--secondary" onClick={onRetry}>Try Again</button>}
    </div>
  )
}
