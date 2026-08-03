import { clearStoredAuth, getStoredAuth } from './authStorage.js'
import { notifyTicketActivityChanged } from './ticketActivityEvents.js'

export class ApiError extends Error {
  constructor(message, status = 0, details = null) {
    super(message)
    this.status = status
    this.details = details
  }
}

export async function apiRequest(path, options = {}) {
  const { responseType, ...fetchOptions } = options
  const auth = getStoredAuth()
  const headers = new Headers(options.headers)
  if (options.body && !(options.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  if (auth?.accessToken) {
    headers.set('Authorization', `Bearer ${auth.accessToken}`)
  }

  let response
  try {
    response = await fetch(path, {
      ...fetchOptions,
      cache: fetchOptions.cache ?? 'no-store',
      headers,
    })
  } catch (error) {
    if (error.name === 'AbortError') {
      throw error
    }
    throw new ApiError(
      'The server could not be reached. Make sure the backend is running.',
    )
  }

  if (response.ok && responseType === 'blob') {
    return response.blob()
  }

  let body = null
  if (response.status !== 204) {
    try {
      body = await response.json()
    } catch {
      body = null
    }
  }

  if (!response.ok) {
    if (response.status === 401) {
      clearStoredAuth()
      window.location.assign('/login')
    }
    const validation = body?.errors
      ? Object.values(body.errors).flat().join(' ')
      : null
    const statusMessage = {
      403: 'You do not have permission to access this page.',
      404: 'The requested resource could not be found.',
      500: 'The server encountered an error while processing the request.',
    }[response.status]
    throw new ApiError(
      validation ||
        body?.message ||
        statusMessage ||
        `The request could not be completed (HTTP ${response.status}).`,
      response.status,
      body,
    )
  }

  const method = (fetchOptions.method || 'GET').toUpperCase()
  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    notifyTicketActivityChanged(path)
  }

  return body
}

export function toQueryString(values) {
  const parameters = new URLSearchParams()
  Object.entries(values).forEach(([key, value]) => {
    if (value !== '' && value !== null && value !== undefined) {
      parameters.set(key, value)
    }
  })
  const query = parameters.toString()
  return query ? `?${query}` : ''
}
