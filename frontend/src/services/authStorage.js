export const AUTH_STORAGE_KEY = 'resolveHubAuth'
export const EMPLOYEE_ROLE = 'Employee'

export function getStoredAuth() {
  const raw =
    localStorage.getItem(AUTH_STORAGE_KEY) ??
    sessionStorage.getItem(AUTH_STORAGE_KEY)
  if (!raw) return null

  try {
    const auth = JSON.parse(raw)
    if (!auth.accessToken || Date.parse(auth.expiresAtUtc) <= Date.now()) {
      clearStoredAuth()
      return null
    }
    return auth
  } catch {
    clearStoredAuth()
    return null
  }
}

export function clearStoredAuth() {
  localStorage.removeItem(AUTH_STORAGE_KEY)
  sessionStorage.removeItem(AUTH_STORAGE_KEY)
}

export function isEmployee(auth) {
  return auth?.user?.roles?.includes(EMPLOYEE_ROLE) === true
}
