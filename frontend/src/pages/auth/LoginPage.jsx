import { useState } from 'react'
import { Eye, EyeOff } from 'lucide-react'
import { useLocation, useNavigate } from 'react-router-dom'
import { loginUser } from '../../services/authService.js'
import {
  AUTH_STORAGE_KEY,
  clearStoredAuth,
  isEmployee,
  isItAgent,
  isAdministrator,
  isManager,
} from '../../services/authStorage.js'
import '../../styles/login.css'
import { useTheme } from '../../theme/ThemeContext.jsx'

const supportEmail = import.meta.env.VITE_SUPPORT_EMAIL?.trim()
const validSupportEmail = supportEmail &&
  /^[^\s@?&#]+@[^\s@?&#]+\.[^\s@?&#]+$/.test(supportEmail)

function createSupportMailto(accountEmail) {
  if (!validSupportEmail) return null

  const subject = 'ResolveHub Support Request'
  const enteredEmail = accountEmail.trim()
  const body = `Hello ResolveHub Support Team,

I'm reaching out for help with accessing my ResolveHub account.

Account email:${enteredEmail ? ` ${enteredEmail}` : ''}

Issue:

Description:


Thank you.`

  return `mailto:${encodeURIComponent(supportEmail)}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`
}

function getLoginError(error) {
  if (error.status === 400) {
    return 'Please check your email address and password and try again.'
  }

  if (error.status === 401) {
    return 'The email address or password is incorrect.'
  }

  if (error.status === 403) {
    return error.response?.message ?? 'This account cannot sign in. Please contact your system administrator.'
  }

  if (error.status === 423) {
    return 'Your account is temporarily locked after repeated failed attempts.'
  }

  if (error.status === 429) {
    return 'Too many login attempts. Please wait and try again.'
  }

  if (error.message === 'CONNECTION_ERROR') {
    return 'The server could not be reached. Make sure the backend is running.'
  }

  return 'Sign in was unsuccessful. Please try again.'
}

function LoginPage() {
  const { activateUserTheme, resetToAuthTheme } = useTheme()
  const location = useLocation()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [rememberMe, setRememberMe] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [message, setMessage] = useState(
    location.state?.passwordResetMessage ??
      (location.state?.accessDenied
        ? 'Your account does not have permission to access that page.'
        : ''),
  )

  async function handleSubmit(event) {
    event.preventDefault()
    setMessage('')

    if (!email.trim() || !password) {
      setMessage('Please enter your email address and password.')
      return
    }

    try {
      setIsLoading(true)

      const authData = await loginUser(email.trim(), password)

      const storage = rememberMe ? localStorage : sessionStorage
      const otherStorage = rememberMe ? sessionStorage : localStorage

      storage.setItem(AUTH_STORAGE_KEY, JSON.stringify(authData))
      otherStorage.removeItem(AUTH_STORAGE_KEY)
      activateUserTheme(authData.user)

      if (isEmployee(authData)) {
        navigate('/employee/dashboard', { replace: true })
      } else if (isItAgent(authData)) {
        navigate('/agent/dashboard', { replace: true })
      } else if (isAdministrator(authData)) {
        navigate('/admin/dashboard', { replace: true })
      } else if (isManager(authData)) {
        navigate('/manager/dashboard', { replace: true })
      } else {
        clearStoredAuth()
        resetToAuthTheme()
        setMessage('This account role is not supported in the dashboard yet.')
      }
    } catch (error) {
      setMessage(getLoginError(error))
    } finally {
      setIsLoading(false)
    }
  }

  function handleForgotPassword() {
    navigate('/forgot-password')
  }

  function handleSupportContact(event) {
    if (validSupportEmail) return
    event.preventDefault()
    setMessage('IT support contact is currently unavailable.')
  }

  const supportMailto = createSupportMailto(email)

  return (
    <main className="login-page">
      <div className="login-content">
        <section className="login-card" aria-labelledby="login-heading">
          <div className="login-card__header">
            <span className="login-card__brand">ResolveHub</span>
            <h1 id="login-heading">Welcome Back</h1>
            <p>Sign in to access your ResolveHub account.</p>
          </div>

          <form className="login-form" onSubmit={handleSubmit} noValidate>
            <div className="form-field">
              <label htmlFor="email">Email address</label>
              <input
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                placeholder="name@company.com"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                disabled={isLoading}
              />
            </div>

            <div className="form-field">
              <label htmlFor="password">Password</label>

              <div className="password-input">
                <input
                  id="password"
                  name="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  placeholder="Enter your password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  disabled={isLoading}
                />

                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowPassword((current) => !current)}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  aria-pressed={showPassword}
                  disabled={isLoading}
                >
                  {showPassword ? <Eye size={20} /> : <EyeOff size={20} />}
                </button>
              </div>
            </div>

            <div className="form-options">
              <label className="remember-me">
                <input
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(event) => setRememberMe(event.target.checked)}
                  disabled={isLoading}
                />
                <span>Remember me</span>
              </label>

              <button
                type="button"
                className="text-button"
                onClick={handleForgotPassword}
                disabled={isLoading}
              >
                Forgot Password?
              </button>
            </div>

            <div className="form-message" role="alert" aria-live="polite">
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
                  Signing in...
                </>
              ) : (
                'Sign In'
              )}
            </button>
          </form>

          <div className="login-card__footer">
            <p>
              Need help accessing your account?{' '}
              <a
                href={supportMailto ?? '#'}
                onClick={handleSupportContact}
                aria-label="Contact IT Support by email"
              >
                Contact IT Support
              </a>
            </p>
          </div>
        </section>
      </div>
    </main>
  )
}

export default LoginPage
