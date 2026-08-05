import { formatLocalDate, formatLocalDateTime, formatLocalTime, parseApiUtcDate } from './dateTime.js'

function localDay(value) {
  const date = parseApiUtcDate(value)
  return !date || Number.isNaN(date.getTime())
    ? null
    : new Date(date.getFullYear(), date.getMonth(), date.getDate())
}

export function activityDateGroup(value, now = new Date()) {
  const day = localDay(value)
  if (!day) return 'Date unavailable'
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const days = Math.round((today - day) / 86400000)
  if (days === 0) return 'Today'
  if (days === 1) return 'Yesterday'
  const weekday = today.getDay() || 7
  if (days > 1 && days < weekday) return 'Earlier this week'
  if (days >= weekday && days < weekday + 7) return 'Last week'
  return formatLocalDate(value)
}

export function formatActivityDate(value) {
  const date = parseApiUtcDate(value)
  if (!date || Number.isNaN(date.getTime())) {
    return { date: '—', time: '—', metadata: 'Date unavailable' }
  }

  const group = activityDateGroup(value)
  const time = formatLocalTime(value)
  return {
    date: formatLocalDate(value),
    time,
    metadata: group === 'Today' || group === 'Yesterday'
      ? `${group} at ${time}`
      : formatLocalDateTime(value),
  }
}
