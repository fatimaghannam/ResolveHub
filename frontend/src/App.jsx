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
import TicketDraftsPage from './pages/TicketDraftsPage.jsx'
import EditTicketDraftPage from './pages/EditTicketDraftPage.jsx'
import AgentDashboardPage from './pages/AgentDashboardPage.jsx'
import AgentTicketsPage from './pages/AgentTicketsPage.jsx'
import AgentTicketDetailsPage from './pages/AgentTicketDetailsPage.jsx'
import AgentNotificationsPage from './pages/AgentNotificationsPage.jsx'
import AgentProfilePage from './pages/AgentProfilePage.jsx'
import { EMPLOYEE_ROLE, IT_AGENT_ROLE } from './services/authStorage.js'

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
            <ProtectedRoute allowedRoles={[EMPLOYEE_ROLE]}>
              <DashboardLayout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<EmployeeDashboardPage />} />
          <Route path="tickets" element={<EmployeeTicketsPage />} />
          <Route path="tickets/create" element={<CreateTicketPage />} />
          <Route path="tickets/drafts" element={<TicketDraftsPage />} />
          <Route path="tickets/drafts/:id" element={<EditTicketDraftPage />} />
          <Route path="tickets/:id" element={<TicketDetailsPage />} />
          <Route path="tickets/:id/edit" element={<EditTicketPage />} />
          <Route path="coming-soon" element={<ComingSoonPage />} />
        </Route>
        <Route
          path="/agent"
          element={
            <ProtectedRoute allowedRoles={[IT_AGENT_ROLE]}>
              <DashboardLayout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<AgentDashboardPage />} />
          <Route path="tickets" element={<AgentTicketsPage />} />
          <Route path="tickets/:id" element={<AgentTicketDetailsPage />} />
          <Route path="notifications" element={<AgentNotificationsPage />} />
          <Route path="profile" element={<AgentProfilePage />} />
        </Route>
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
