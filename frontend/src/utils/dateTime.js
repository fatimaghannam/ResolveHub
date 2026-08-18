export function parseApiUtcDate(value) {
  if (!value) {
    return null
  }

  const text = String(value)
  const hasExplicitTimeZone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(text)
  return new Date(hasExplicitTimeZone ? text : `${text}Z`)
}

const localDateTimeFormatter = new Intl.DateTimeFormat('en-US', {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
  hour12: true,
})

const localDateFormatter = new Intl.DateTimeFormat('en-US', {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
})

const localTimeFormatter = new Intl.DateTimeFormat('en-US', {
  hour: 'numeric',
  minute: '2-digit',
  hour12: true,
})

function formatLocalValue(value, formatter) {
  const date = parseApiUtcDate(value)
  return date && !Number.isNaN(date.getTime()) ? formatter.format(date) : '—'
}

export function formatLocalDateTime(value) {
  return formatLocalValue(value, localDateTimeFormatter)
}

export function formatLocalDate(value) {
  return formatLocalValue(value, localDateFormatter)
}

export function formatLocalTime(value) {
  return formatLocalValue(value, localTimeFormatter)
}

export function formatRelativeTime(value, now = Date.now()) {
  const date = parseApiUtcDate(value)
  if (!date || Number.isNaN(date.getTime())) return 'â€”'

  const minutes = Math.max(0, Math.floor((Number(now) - date.getTime()) / 60000))
  if (minutes < 1) return 'Just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}
