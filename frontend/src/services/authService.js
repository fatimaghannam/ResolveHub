export async function loginUser(email, password) {
  let response

  try {
    response = await fetch('/api/auth/login', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ email, password }),
    })
  } catch {
    throw new Error('CONNECTION_ERROR')
  }

  if (!response.ok) {
    const error = new Error('LOGIN_FAILED')
    error.status = response.status
    throw error
  }

  try {
    return await response.json()
  } catch {
    throw new Error('INVALID_RESPONSE')
  }
}
