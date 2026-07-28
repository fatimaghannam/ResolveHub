export function toLocalDateInputValue(date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function parseLocalDateInput(value) {
  if (!value) return null

  const parts = value.split('-')
  if (parts.length !== 3) return null

  const [year, month, day] = parts.map(Number)
  if (!year || !month || !day) return null

  const date = new Date(year, month - 1, day, 0, 0, 0, 0)
  return date.getFullYear() === year &&
    date.getMonth() === month - 1 &&
    date.getDate() === day
    ? date
    : null
}

export function getUtcDateRange(fromDate, toDate) {
  const localStart = parseLocalDateInput(fromDate)
  const localEnd = parseLocalDateInput(toDate)

  if (!localStart || !localEnd) {
    return { fromUtc: null, toUtcExclusive: null }
  }

  const localEndExclusive = new Date(localEnd)
  localEndExclusive.setDate(localEndExclusive.getDate() + 1)

  return {
    fromUtc: localStart.toISOString(),
    toUtcExclusive: localEndExclusive.toISOString(),
  }
}

export function getLocalQuickDateRange(range, currentDate = new Date()) {
  if (range === 'all') return { fromDate: '', toDate: '' }

  const today = new Date(
    currentDate.getFullYear(),
    currentDate.getMonth(),
    currentDate.getDate(),
  )
  const start = new Date(today)
  const end = new Date(today)

  switch (range) {
    case 'yesterday':
      start.setDate(start.getDate() - 1)
      end.setDate(end.getDate() - 1)
      break
    case 'last7Days':
      start.setDate(start.getDate() - 6)
      break
    case 'last30Days':
      start.setDate(start.getDate() - 29)
      break
    default:
      return null
  }

  return {
    fromDate: toLocalDateInputValue(start),
    toDate: toLocalDateInputValue(end),
  }
}
export const STANDARD_DATE_RANGE_OPTIONS = [
  ['all', 'All Dates'],
  ['yesterday', 'Yesterday'],
  ['last7Days', 'Last 7 Days'],
  ['last30Days', 'Last 30 Days'],
  ['custom', 'Custom Range'],
]
