import { useState } from 'react'
import { Eye, EyeOff } from 'lucide-react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { resetPassword } from '../services/authService.js'
import '../styles/login.css'

function getResetPasswordError(error) {
  if (error.message === 'CONNECTION_ERROR') {
    return 'The server could not be reached. Make sure the backend is running.'
  }

  if (error.status === 429) {
    return 'Too many requests. Please wait and try again.'
  }

  if (error.status === 400) {
    const passwordErrors = error.response?.errors

    if (Array.isArray(passwordErrors) && passwordErrors.length > 0) {
      return passwordErrors.join(' ')
    }

    return (
      error.response?.message ??
      'This password reset link is invalid or has expired.'
    )
  }

  return 'The password could not be reset. Please try again.'
}

function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const email = searchParams.get('email') ?? ''
  const token = searchParams.get('token') ?? ''

  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmation, setShowConfirmation] = useState(false)
  const [message, setMessage] = useState('')
  const [isLoading, setIsLoading] = useState(false)

  async function handleSubmit(event) {
    event.preventDefault()

    if (isLoading) {
      return
    }

    setMessage('')

    if (!email || !token) {
      setMessage('This password reset link is invalid or incomplete.')
      return
    }

    if (!newPassword || !confirmPassword) {
      setMessage('Enter and confirm your new password.')
      return
    }

    if (newPassword !== confirmPassword) {
      setMessage('The password confirmation does not match.')
      return
    }

    try {
      setIsLoading(true)
      await resetPassword({
        email,
        token,
        newPassword,
        confirmPassword,
      })
      navigate('/login', {
        replace: true,
        state: {
          passwordResetMessage:
            'Your password has been reset successfully. Sign in using your new password.',
        },
      })
    } catch (error) {
      setMessage(getResetPasswordError(error))
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <main className="login-page">
      <div className="login-content">
        <section className="login-card auth-card" aria-labelledby="reset-heading">
          <div className="login-card__header">
            <span className="login-card__brand">ResolveHub</span>
            <h1 id="reset-heading">Create New Password</h1>
            <p>Choose a strong password for your ResolveHub account.</p>
          </div>

          <form className="login-form" onSubmit={handleSubmit} noValidate>
            <div className="form-field">
              <label htmlFor="new-password">New password</label>
              <div className="password-input">
                <input
                  id="new-password"
                  name="newPassword"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="new-password"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  disabled={isLoading}
                />
                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowPassword((current) => !current)}
                  aria-label={showPassword ? 'Hide new password' : 'Show new password'}
                  aria-pressed={showPassword}
                  disabled={isLoading}
                >
                  {showPassword ? <Eye size={20} /> : <EyeOff size={20} />}
                </button>
              </div>
            </div>

            <div className="form-field">
              <label htmlFor="confirm-password">Confirm new password</label>
              <div className="password-input">
                <input
                  id="confirm-password"
                  name="confirmPassword"
                  type={showConfirmation ? 'text' : 'password'}
                  autoComplete="new-password"
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  disabled={isLoading}
                />
                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowConfirmation((current) => !current)}
                  aria-label={
                    showConfirmation
                      ? 'Hide password confirmation'
                      : 'Show password confirmation'
                  }
                  aria-pressed={showConfirmation}
                  disabled={isLoading}
                >
                  {showConfirmation ? <Eye size={20} /> : <EyeOff size={20} />}
                </button>
              </div>
            </div>

            <p className="password-guidance">
              Use at least 8 characters with uppercase and lowercase letters, a
              number, and a special character.
            </p>

            <div className="form-message" role="alert" aria-live="polite">
              {message}
            </div>

            <button
              type="submit"
              className="sign-in-button"
              disabled={isLoading || !email || !token}
            >
              {isLoading ? (
                <>
                  <span className="spinner" aria-hidden="true" />
                  Resetting...
                </>
              ) : (
                'Reset Password'
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

export default ResetPasswordPage
