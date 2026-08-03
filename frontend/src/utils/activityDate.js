const dateFormatter = new Intl.DateTimeFormat(undefined, {
  month: 'short', day: 'numeric', year: 'numeric',
})

const timeFormatter = new Intl.DateTimeFormat(undefined, {
  hour: 'numeric', minute: '2-digit',
})

function localDay(value) {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
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
  return dateFormatter.format(day)
}

export function formatActivityDate(value) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return { date: '—', time: '—', metadata: 'Date unavailable' }
  const group = activityDateGroup(value)
  const time = timeFormatter.format(date)
  const dateText = dateFormatter.format(date)
  return {
    date: dateText,
    time,
    metadata: `${group === 'Today' || group === 'Yesterday' ? group : dateText} · ${time}`,
  }
}
