export function formatAccountStatus(status) {
  const normalized = String(status ?? '').replaceAll(/[_-]/g, ' ').trim()

  if (/^pending\s*setup$/i.test(normalized) || /^pendingsetup$/i.test(normalized)) {
    return 'Pending'
  }

  return normalized
}

export function accountStatusClassName(status) {
  return formatAccountStatus(status).toLowerCase().replaceAll(' ', '-')
}
