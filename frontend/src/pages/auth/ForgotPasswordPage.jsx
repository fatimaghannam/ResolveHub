import { useState } from 'react'
import { Link } from 'react-router-dom'
import { requestPasswordReset } from '../../services/authService.js'
import '../../styles/login.css'

const GENERIC_SUCCESS_MESSAGE =
  'If an eligible account exists for that email address, password reset instructions have been sent.'

function getForgotPasswordError(error) {
  if (error.message === 'CONNECTION_ERROR') {
    return 'The server could not be reached. Make sure the backend is running.'
  }

  if (error.status === 400) {
    return 'Enter a valid email address.'
  }

  if (error.status === 429) {
    return 'Too many requests. Please wait and try again.'
  }

  return 'The request could not be completed. Please try again.'
}

function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState('')
  const [isSuccess, setIsSuccess] = useState(false)
  const [isLoading, setIsLoading] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()

    if (isLoading) {
      return
    }

    setMessage('')
    setIsSuccess(false)

    if (!email.trim()) {
      setMessage('Enter your email address.')
      return
    }

    try {
      setIsLoading(true)
      await requestPasswordReset(email.trim())
      setIsSuccess(true)
      setMessage(GENERIC_SUCCESS_MESSAGE)
    } catch (error) {
      setMessage(getForgotPasswordError(error))
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <main className="login-page">
      <div className="login-content">
        <section className="login-card auth-card" aria-labelledby="forgot-heading">
          <div className="login-card__header">
            <span className="login-card__brand">ResolveHub</span>
            <h1 id="forgot-heading">Forgot your password?</h1>
            <p>
              Enter your account email and we will send password reset
              instructions if the account is eligible.
            </p>
          </div>

          <form className="login-form" onSubmit={handleSubmit} noValidate>
            <div className="form-field">
              <label htmlFor="forgot-email">Email address</label>
              <input
                id="forgot-email"
                name="email"
                type="email"
                autoComplete="email"
                placeholder="name@company.com"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                disabled={isLoading}
              />
            </div>

            <div
              className={`form-message${isSuccess ? ' form-message--success' : ''}`}
              role="status"
              aria-live="polite"
            >
              {message}
            </div>

            <button
              type="submit"
              className="sign-in-button"
              disabled={isLoading}
            >
              {isLoading ? (
                <>
                  <span className="spinner" aria-hidden="true" />
                  Sending...
                </>
              ) : (
                'Send Reset Instructions'
              )}
            </button>
          </form>

          <div className="login-card__footer auth-card__footer">
            <Link className="auth-link" to="/login">
              Back to Sign In
            </Link>
          </div>
        </section>
      </div>
    </main>
  )
}

export default ForgotPasswordPage
