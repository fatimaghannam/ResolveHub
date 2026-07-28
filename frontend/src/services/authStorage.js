export const AUTH_STORAGE_KEY = 'resolveHubAuth'
export const EMPLOYEE_ROLE = 'Employee'
export const IT_AGENT_ROLE = 'IT Support Agent'
export const ADMIN_ROLE = 'Administrator'
export const MANAGER_ROLE = 'Manager'

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

export function isItAgent(auth) {
  return auth?.user?.roles?.includes(IT_AGENT_ROLE) === true
}

export function isAdministrator(auth) {
  return auth?.user?.roles?.includes(ADMIN_ROLE) === true
}

export function isManager(auth) {
  return auth?.user?.roles?.includes(MANAGER_ROLE) === true
}
