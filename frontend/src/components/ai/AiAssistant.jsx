import { useState } from 'react'
import { Bot, Send, X } from 'lucide-react'
import { useLocation } from 'react-router-dom'
import { sendAiChat } from '../../services/aiService.js'

function AiAssistant() {
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState([])
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function send(event) {
    event.preventDefault()
    const content = input.trim()
    if (!content || busy) return
    const next = [...messages, { role: 'user', content }].slice(-10)
    setMessages(next); setInput(''); setError(''); setBusy(true)
    try {
      const result = await sendAiChat(next, null, getPageContext(pathname))
      setMessages([...next, { role: 'assistant', content: result.message }].slice(-10))
    } catch (err) { setError(err.message) } finally { setBusy(false) }
  }
  return <>
    <button type="button" className="ai-launcher" onClick={() => setOpen(true)} aria-label="Open ResolveHub AI Assistant"><Bot size={21} /></button>
    {open && <aside className="ai-assistant" aria-label="ResolveHub AI Assistant">
      <header><div><strong>ResolveHub AI Assistant</strong><small>Read-only help and guidance</small></div><button type="button" onClick={() => setOpen(false)} aria-label="Close assistant"><X size={19} /></button></header>
      <div className="ai-assistant__messages">
        {messages.length === 0 && <p className="ai-assistant__welcome">Ask about ticket statuses, writing a clear request, or safe basic troubleshooting.</p>}
        {messages.map((message, index) => <div key={index} className={`ai-message ai-message--${message.role}`}>{message.content}</div>)}
        {busy && <div className="ai-message ai-message--assistant">Generating response…</div>}
        {error && <div className="inline-alert inline-alert--error">{error}</div>}
      </div>
      <form onSubmit={send}><input maxLength="2000" value={input} onChange={(event) => setInput(event.target.value)} placeholder="Ask ResolveHub AI…" /><button disabled={busy || !input.trim()} aria-label="Send"><Send size={18} /></button></form>
    </aside>}
  </>
}

function getPageContext(pathname) {
  if (pathname.endsWith('/dashboard')) return 'dashboard'
  if (pathname.endsWith('/tickets/create')) return 'create-ticket'
  if (pathname.endsWith('/my-tickets') || pathname === '/employee/tickets') return 'my-tickets'
  if (/\/(employee|agent|admin|manager)\/tickets\/[^/]+$/.test(pathname)) return 'ticket-details'
  if (pathname.endsWith('/tickets/assigned')) return 'assigned-tickets'
  if (pathname.endsWith('/tickets/open')) return 'open-tickets'
  if (pathname.endsWith('/tickets')) return 'all-tickets'
  if (pathname.endsWith('/assignments')) return 'ticket-assignments'
  if (pathname.includes('/workload')) return 'team-workload'
  if (pathname.includes('/users')) return 'users'
  if (pathname.endsWith('/categories')) return 'categories'
  if (pathname.endsWith('/audit-log')) return 'audit-log'
  if (pathname.endsWith('/notifications')) return 'notifications'
  if (pathname.endsWith('/profile')) return 'profile'
  return null
}
export default AiAssistant
