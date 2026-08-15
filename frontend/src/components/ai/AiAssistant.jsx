import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Bot, RotateCcw, Send, X } from 'lucide-react'
import { useLocation } from 'react-router-dom'
import { sendAiChat } from '../../services/aiService.js'

function AiAssistant() {
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState([])
  const [input, setInput] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const inputRef = useRef(null)
  const messagesRef = useRef(null)
  const conversationVersionRef = useRef(0)

  useEffect(() => {
    if (open) inputRef.current?.focus({ preventScroll: true })
  }, [open, busy])

  useLayoutEffect(() => {
    if (!open || !messagesRef.current) return
    messagesRef.current.scrollTop = messagesRef.current.scrollHeight
  }, [open, messages, busy, error])

  async function send(event) {
    event.preventDefault()
    const content = input.trim()
    if (!content || busy) return
    const conversationVersion = conversationVersionRef.current
    const next = [...messages, { role: 'user', content }].slice(-10)
    setMessages(next); setInput(''); setError(''); setBusy(true)
    try {
      const result = await sendAiChat(next, null, getPageContext(pathname))
      if (conversationVersion !== conversationVersionRef.current) return
      setMessages([...next, { role: 'assistant', content: result.message }].slice(-10))
    } catch (err) {
      if (conversationVersion === conversationVersionRef.current) setError(err.message)
    } finally {
      if (conversationVersion === conversationVersionRef.current) setBusy(false)
    }
  }

  function startNewChat() {
    conversationVersionRef.current += 1
    setMessages([])
    setInput('')
    setError('')
    setBusy(false)
    requestAnimationFrame(() => inputRef.current?.focus({ preventScroll: true }))
  }
  return <>
    <button type="button" className="ai-launcher" onClick={() => setOpen(true)} aria-label="Open ResolveHub AI Assistant"><Bot size={21} /></button>
    {open && <aside className="ai-assistant" aria-label="ResolveHub AI Assistant">
      <header><div className="ai-assistant__brand"><img src="/favicon.png" alt="ResolveHub" draggable="false" /><div><strong>ResolveHub AI Assistant</strong><small>Read-only help and guidance</small></div></div><div className="ai-assistant__header-actions"><button type="button" className="ai-assistant__header-button ai-assistant__new-chat" onClick={startNewChat} aria-label="Start new chat" title="Start new chat"><RotateCcw size={19} /></button><button type="button" className="ai-assistant__header-button" onClick={() => setOpen(false)} aria-label="Close assistant"><X size={19} /></button></div></header>
      <div className="ai-assistant__messages" ref={messagesRef}>
        {messages.length === 0 && <p className="ai-assistant__welcome">Ask about ticket statuses, writing a clear request, or safe basic troubleshooting.</p>}
        {messages.map((message, index) => <div key={index} className={`ai-message ai-message--${message.role}`}>{message.content}</div>)}
        {busy && <div className="ai-message ai-message--assistant">Generating response…</div>}
        {error && <div className="inline-alert inline-alert--error">{error}</div>}
      </div>
      <form onSubmit={send}><input ref={inputRef} maxLength="2000" value={input} onChange={(event) => setInput(event.target.value)} placeholder="Ask ResolveHub AI…" /><button disabled={busy || !input.trim()} aria-label="Send"><Send size={18} /></button></form>
    </aside>}
  </>
}

const pageContexts = {
  '/employee/dashboard': 'dashboard', '/employee/tickets': 'my-tickets', '/employee/tickets/create': 'create-ticket', '/employee/notifications': 'notifications', '/employee/profile': 'profile',
  '/agent/dashboard': 'dashboard', '/agent/tickets': 'assigned-tickets', '/agent/tickets/assigned': 'assigned-tickets', '/agent/tickets/open': 'open-tickets', '/agent/notifications': 'notifications', '/agent/profile': 'profile',
  '/manager/dashboard': 'dashboard', '/manager/tickets': 'all-tickets', '/manager/assignments': 'ticket-assignments', '/manager/workload': 'team-workload', '/manager/audit-log': 'audit-log', '/manager/notifications': 'notifications', '/manager/profile': 'profile',
  '/admin/dashboard': 'dashboard', '/admin/tickets': 'all-tickets', '/admin/my-tickets': 'my-tickets', '/admin/tickets/create': 'create-ticket', '/admin/assignments': 'ticket-assignments', '/admin/workload': 'team-workload', '/admin/users': 'users', '/admin/categories': 'categories', '/admin/audit-log': 'audit-log', '/admin/notifications': 'notifications', '/admin/profile': 'profile',
}

function getPageContext(pathname) {
  if (pageContexts[pathname]) return pageContexts[pathname]
  if (!pathname.includes('/tickets/drafts') && /\/(employee|agent|admin|manager)\/tickets\/[^/]+$/.test(pathname)) return 'ticket-details'
  if (/\/(manager|admin)\/workload\/[^/]+$/.test(pathname)) return 'team-workload'
  return null
}
export default AiAssistant
