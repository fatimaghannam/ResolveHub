import {
  CheckCircle2,
  CircleAlert,
  Info,
  TriangleAlert,
  X,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'

const icons = {
  success: CheckCircle2,
  error: CircleAlert,
  warning: TriangleAlert,
  info: Info,
}

function Toast({
  type = 'info',
  title,
  message,
  onDismiss,
  duration = 4000,
}) {
  const [closing, setClosing] = useState(false)
  const closeTimer = useRef(null)
  const Icon = icons[type] ?? icons.info

  function dismiss() {
    if (closing) return
    setClosing(true)
    closeTimer.current = window.setTimeout(onDismiss, 250)
  }

  useEffect(() => {
    const fadeTimer = window.setTimeout(
      () => setClosing(true),
      Math.max(0, duration - 250),
    )
    const dismissTimer = window.setTimeout(onDismiss, duration)
    return () => {
      window.clearTimeout(fadeTimer)
      window.clearTimeout(dismissTimer)
      window.clearTimeout(closeTimer.current)
    }
  }, [duration, onDismiss])

  return (
    <div
      className={`app-toast app-toast--${type}${closing ? ' app-toast--closing' : ''}`}
      role={type === 'error' ? 'alert' : 'status'}
      aria-live="polite"
      onKeyDown={(event) => {
        if (event.key === 'Escape') dismiss()
      }}
    >
      <Icon className="app-toast__icon" size={21} aria-hidden="true" />
      <div className="app-toast__content">
        <strong>{title}</strong>
        <p>{message}</p>
      </div>
      <button
        className="app-toast__close"
        type="button"
        aria-label="Dismiss notification"
        onClick={dismiss}
      >
        <X size={17} />
      </button>
    </div>
  )
}

export default Toast
