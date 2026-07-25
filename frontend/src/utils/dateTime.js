export function parseApiUtcDate(value) {
  if (!value) {
    return null
  }

  const text = String(value)
  const hasExplicitTimeZone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(text)
  return new Date(hasExplicitTimeZone ? text : `${text}Z`)
}

const localDateTimeFormatter = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
  second: '2-digit',
})

const localDateFormatter = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
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
