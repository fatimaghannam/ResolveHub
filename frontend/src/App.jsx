import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import ForgotPasswordPage from './pages/ForgotPasswordPage.jsx'
import LoginPage from './pages/LoginPage.jsx'
import ResetPasswordPage from './pages/ResetPasswordPage.jsx'
import ProtectedRoute from './components/ProtectedRoute.jsx'
import DashboardLayout from './components/layout/DashboardLayout.jsx'
import EmployeeDashboardPage from './pages/EmployeeDashboardPage.jsx'
import EmployeeTicketsPage from './pages/EmployeeTicketsPage.jsx'
import CreateTicketPage from './pages/CreateTicketPage.jsx'
import EditTicketPage from './pages/EditTicketPage.jsx'
import TicketDetailsPage from './pages/TicketDetailsPage.jsx'
import ComingSoonPage from './pages/ComingSoonPage.jsx'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/login" replace />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route
          path="/employee"
          element={
            <ProtectedRoute employeeOnly>
              <DashboardLayout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<EmployeeDashboardPage />} />
          <Route path="tickets" element={<EmployeeTicketsPage />} />
          <Route path="tickets/create" element={<CreateTicketPage />} />
          <Route path="tickets/:id" element={<TicketDetailsPage />} />
          <Route path="tickets/:id/edit" element={<EditTicketPage />} />
          <Route path="coming-soon" element={<ComingSoonPage />} />
        </Route>
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
