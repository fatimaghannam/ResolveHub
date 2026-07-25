import { Navigate, useLocation } from 'react-router-dom'
import { getStoredAuth, isEmployee } from '../services/authStorage.js'

function ProtectedRoute({ children, employeeOnly = false }) {
  const location = useLocation()
  const auth = getStoredAuth()
  if (!auth) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }
  if (employeeOnly && !isEmployee(auth)) {
    return <Navigate to="/login" replace />
  }
  return children
}

export default ProtectedRoute
