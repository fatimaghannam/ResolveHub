function AiSuggestionCard({ suggestion, onApply }) {
  return (
    <section className="ai-card" aria-live="polite">
      <div className="ai-card__heading"><div><strong>AI Suggestions</strong><small>Recommendations only — review before applying.</small></div></div>
      <div className="ai-suggestion-grid">
        <div><small>Category</small><strong>{suggestion.suggestedCategoryName}</strong>{suggestion.categoryReason && <p>{suggestion.categoryReason}</p>}</div>
        <div><small>Priority</small><strong>{suggestion.suggestedPriorityName}</strong>{suggestion.priorityReason && <p>{suggestion.priorityReason}</p>}</div>
      </div>
      <button type="button" className="button button--secondary button--compact" onClick={onApply}>Apply Suggestions</button>
    </section>
  )
}
export default AiSuggestionCard
