import { useState } from 'react'
import { Bot } from 'lucide-react'
import { generateTicketSummary, generateTroubleshooting } from '../../services/aiService.js'

function AiTicketTools({ ticketId, allowTroubleshooting = false }) {
  const [summary, setSummary] = useState(''); const [guidance, setGuidance] = useState(null)
  const [busy, setBusy] = useState(''); const [error, setError] = useState('')
  async function run(kind) { try { setBusy(kind); setError(''); if (kind === 'summary') setSummary((await generateTicketSummary(ticketId)).summary); else setGuidance(await generateTroubleshooting(ticketId)) } catch (err) { setError(err.message) } finally { setBusy('') } }
  return <section className="panel ai-ticket-tools"><div className="panel__heading ai-ticket-tools__header"><div className="ai-ticket-tools__title"><span><Bot size={20} aria-hidden="true" /><h2>AI Assistance</h2></span><p>Generated recommendations should be verified.</p></div><div className="ai-ticket-tools__actions"><button className="button button--secondary button--compact" disabled={Boolean(busy)} onClick={() => run('summary')}>{busy === 'summary' ? 'Generating...' : summary ? 'Regenerate Summary' : 'Generate AI Summary'}</button>{allowTroubleshooting && <button className="button button--secondary button--compact" disabled={Boolean(busy)} onClick={() => run('troubleshooting')}>{busy === 'troubleshooting' ? 'Generating steps…' : 'Generate Troubleshooting Steps'}</button>}</div></div>{error && <div className="inline-alert inline-alert--error ai-ticket-tools__error">{error}</div>}{summary && <div className="ai-summary"><strong>AI Summary</strong><p>{summary}</p></div>}{guidance && <div className="ai-output"><strong>{guidance.overview}</strong><ol>{guidance.steps.map((step) => <li key={step}>{step}</li>)}</ol>{guidance.escalationRecommended && <p><b>Escalation is recommended.</b></p>}</div>}</section>
}
export default AiTicketTools
