/* oxlint-disable react/only-export-components -- provider and its colocated hook form one theme API. */
import { createContext, useCallback, useContext, useMemo, useState } from 'react'
import { getStoredAuth } from '../services/authStorage.js'

const THEME_STORAGE_PREFIX = 'resolvehub-theme:'
const LEGACY_THEME_STORAGE_KEY = 'resolvehub-theme'
const ThemeContext = createContext(null)

function getUserIdentifier(user) {
  if (user?.id !== undefined && user?.id !== null) return String(user.id)
  return user?.email ? user.email.trim().toLowerCase() : null
}

function getThemeStorageKey(user) {
  const identifier = getUserIdentifier(user)
  return identifier ? `${THEME_STORAGE_PREFIX}${encodeURIComponent(identifier)}` : null
}

function readUserTheme(user) {
  const storageKey = getThemeStorageKey(user)
  if (!storageKey) return 'light'
  try {
    return localStorage.getItem(storageKey) === 'dark' ? 'dark' : 'light'
  } catch {
    return 'light'
  }
}

function isAuthPage() {
  return ['/login', '/forgot-password', '/reset-password', '/'].includes(window.location.pathname)
}

function readInitialTheme() {
  if (isAuthPage()) return 'light'
  return readUserTheme(getStoredAuth()?.user)
}

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme
  document.documentElement.style.colorScheme = theme
}

try {
  localStorage.removeItem(LEGACY_THEME_STORAGE_KEY)
} catch {
}

applyTheme(readInitialTheme())

export function ThemeProvider({ children }) {
  const [theme, setThemeState] = useState(readInitialTheme)

  const setTheme = useCallback((nextTheme) => {
    const normalizedTheme = nextTheme === 'dark' ? 'dark' : 'light'
    applyTheme(normalizedTheme)
    const storageKey = getThemeStorageKey(getStoredAuth()?.user)
    try {
      if (storageKey) localStorage.setItem(storageKey, normalizedTheme)
    } catch {
    }
    setThemeState(normalizedTheme)
  }, [])

  const activateUserTheme = useCallback((user) => {
    const userTheme = readUserTheme(user)
    applyTheme(userTheme)
    setThemeState(userTheme)
  }, [])

  const resetToAuthTheme = useCallback(() => {
    applyTheme('light')
    setThemeState('light')
  }, [])

  const toggleTheme = useCallback(() => {
    setTheme(theme === 'dark' ? 'light' : 'dark')
  }, [setTheme, theme])

  const value = useMemo(
    () => ({ theme, setTheme, toggleTheme, activateUserTheme, resetToAuthTheme }),
    [activateUserTheme, resetToAuthTheme, setTheme, theme, toggleTheme],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}

export function useTheme() {
  const value = useContext(ThemeContext)
  if (!value) throw new Error('useTheme must be used within ThemeProvider')
  return value
}
