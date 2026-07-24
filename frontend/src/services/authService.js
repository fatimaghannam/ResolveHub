async function sendAuthRequest(path, body) {
  let response

  try {
    response = await fetch(path, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    })
  } catch {
    throw new Error('CONNECTION_ERROR')
  }

  let responseBody = null

  try {
    responseBody = await response.json()
  } catch {
    // A safe fallback is used below when an error response has no JSON body.
  }

  if (!response.ok) {
    const error = new Error('REQUEST_FAILED')
    error.status = response.status
    error.response = responseBody
    throw error
  }

  if (responseBody === null) {
    throw new Error('INVALID_RESPONSE')
  }

  return responseBody
}

export function loginUser(email, password) {
  return sendAuthRequest('/api/auth/login', { email, password })
}

export function requestPasswordReset(email) {
  return sendAuthRequest('/api/auth/forgot-password', { email })
}

export function resetPassword({
  email,
  token,
  newPassword,
  confirmPassword,
}) {
  return sendAuthRequest('/api/auth/reset-password', {
    email,
    token,
    newPassword,
    confirmPassword,
  })
}
