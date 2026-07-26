import { Navigate, useLocation } from 'react-router-dom'
import { getStoredAuth } from '../services/authStorage.js'

function ProtectedRoute({ children, allowedRoles = [] }) {
  const location = useLocation()
  const auth = getStoredAuth()
  if (!auth) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }
  const roles = auth.user?.roles ?? []
  if (allowedRoles.length > 0 && !allowedRoles.some((role) => roles.includes(role))) {
    return <Navigate to="/login" replace state={{ accessDenied: true }} />
  }
  return children
}

export default ProtectedRoute
