import { lazy, Suspense } from 'react'
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
import AdminTicketsPage from './pages/AdminTicketsPage.jsx'
import AdminAssignmentsPage from './pages/AdminAssignmentsPage.jsx'
import AdminUsersPage from './pages/AdminUsersPage.jsx'
import AdminCategoriesPage from './pages/AdminCategoriesPage.jsx'
import AdminActivityPage from './pages/AdminActivityPage.jsx'
import AdminNotificationsPage from './pages/AdminNotificationsPage.jsx'
import AdminProfilePage from './pages/AdminProfilePage.jsx'
import AdminTicketDetailsPage from './pages/AdminTicketDetailsPage.jsx'
import AdminUserDetailsPage from './pages/AdminUserDetailsPage.jsx'
import { ADMIN_ROLE, EMPLOYEE_ROLE, IT_AGENT_ROLE, MANAGER_ROLE } from './services/authStorage.js'
import ManagerDashboardPage from './pages/ManagerDashboardPage.jsx'
import ManagerWorkloadPage from './pages/ManagerWorkloadPage.jsx'
import ManagerActivityPage from './pages/ManagerActivityPage.jsx'

const AdminDashboardPage = lazy(() => import('./pages/AdminDashboardPage.jsx'))

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
          <Route path="tickets/create" element={<CreateTicketPage roleArea="employee" />} />
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
          <Route path="tickets/open" element={<AgentTicketsPage view="open" />} />
          <Route path="tickets/history" element={<AgentTicketsPage view="history" />} />
          <Route path="tickets/:id" element={<AgentTicketDetailsPage />} />
          <Route path="notifications" element={<AgentNotificationsPage />} />
          <Route path="profile" element={<AgentProfilePage />} />
        </Route>
        <Route
          path="/manager"
          element={
            <ProtectedRoute allowedRoles={[MANAGER_ROLE]}>
              <DashboardLayout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<ManagerDashboardPage />} />
          <Route path="tickets" element={<AdminTicketsPage roleArea="manager" />} />
          <Route path="tickets/:ticketReference" element={<AdminTicketDetailsPage roleArea="manager" />} />
          <Route path="assignments" element={<AdminAssignmentsPage roleArea="manager" />} />
          <Route path="workload" element={<ManagerWorkloadPage />} />
          <Route path="activity" element={<ManagerActivityPage />} />
          <Route path="notifications" element={<AdminNotificationsPage roleArea="manager" />} />
          <Route path="profile" element={<AdminProfilePage />} />
        </Route>
        <Route
          path="/admin"
          element={
            <ProtectedRoute allowedRoles={[ADMIN_ROLE]}>
              <DashboardLayout />
            </ProtectedRoute>
          }
        >
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<Suspense fallback={<div className="state-panel" role="status">Loading Administrator dashboard…</div>}><AdminDashboardPage /></Suspense>} />
          <Route path="tickets" element={<AdminTicketsPage />} />
          <Route path="my-tickets" element={<EmployeeTicketsPage roleArea="admin" />} />
          <Route path="my-tickets/:id/edit" element={<EditTicketPage roleArea="admin" />} />
          <Route path="tickets/create" element={<CreateTicketPage roleArea="admin" />} />
          <Route path="tickets/drafts" element={<TicketDraftsPage roleArea="admin" />} />
          <Route path="tickets/drafts/:id" element={<EditTicketDraftPage roleArea="admin" />} />
          <Route path="tickets/:ticketReference" element={<AdminTicketDetailsPage />} />
          <Route path="assignments" element={<AdminAssignmentsPage />} />
          <Route path="users" element={<AdminUsersPage />} />
          <Route path="users/:userId" element={<AdminUserDetailsPage />} />
          <Route path="categories" element={<AdminCategoriesPage />} />
          <Route path="activity" element={<AdminActivityPage />} />
          <Route path="notifications" element={<AdminNotificationsPage />} />
          <Route path="profile" element={<AdminProfilePage />} />
        </Route>
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
