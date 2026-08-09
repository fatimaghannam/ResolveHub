import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import ForgotPasswordPage from './pages/auth/ForgotPasswordPage.jsx'
import LoginPage from './pages/auth/LoginPage.jsx'
import ResetPasswordPage from './pages/auth/ResetPasswordPage.jsx'
import ProtectedRoute from './components/ProtectedRoute.jsx'
import DashboardLayout from './components/layout/DashboardLayout.jsx'
import EmployeeDashboardPage from './pages/employee/EmployeeDashboardPage.jsx'
import EmployeeTicketsPage from './pages/shared/EmployeeTicketsPage.jsx'
import CreateTicketPage from './pages/shared/CreateTicketPage.jsx'
import EditTicketPage from './pages/shared/EditTicketPage.jsx'
import TicketDetailsPage from './pages/shared/TicketDetailsPage.jsx'
import ComingSoonPage from './pages/shared/ComingSoonPage.jsx'
import TicketDraftsPage from './pages/shared/TicketDraftsPage.jsx'
import EditTicketDraftPage from './pages/shared/EditTicketDraftPage.jsx'
import AgentDashboardPage from './pages/agent/AgentDashboardPage.jsx'
import AgentTicketsPage from './pages/agent/AgentTicketsPage.jsx'
import AgentTicketDetailsPage from './pages/agent/AgentTicketDetailsPage.jsx'
import NotificationsPage from './pages/shared/NotificationsPage.jsx'
import ProfilePage from './pages/shared/ProfilePage.jsx'
import AdminTicketsPage from './pages/shared/AdminTicketsPage.jsx'
import AdminAssignmentsPage from './pages/shared/AdminAssignmentsPage.jsx'
import AdminUsersPage from './pages/admin/AdminUsersPage.jsx'
import AdminCategoriesPage from './pages/admin/AdminCategoriesPage.jsx'
import AdminActivityPage from './pages/shared/AdminActivityPage.jsx'
import AdminTicketDetailsPage from './pages/shared/AdminTicketDetailsPage.jsx'
import AdminUserDetailsPage from './pages/admin/AdminUserDetailsPage.jsx'
import { ADMIN_ROLE, EMPLOYEE_ROLE, IT_AGENT_ROLE, MANAGER_ROLE } from './services/authStorage.js'
import ManagerDashboardPage from './pages/manager/ManagerDashboardPage.jsx'
import ManagerWorkloadPage from './pages/shared/ManagerWorkloadPage.jsx'
import AgentWorkloadTicketsPage from './pages/shared/AgentWorkloadTicketsPage.jsx'


const AdminDashboardPage = lazy(() => import('./pages/admin/AdminDashboardPage.jsx'))

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
          <Route path="profile" element={<ProfilePage />} />
          <Route path="notifications" element={<NotificationsPage roleArea="employee" />} />
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
          <Route path="tickets/assigned" element={<AgentTicketsPage />} />
          <Route path="tickets/open" element={<AgentTicketsPage view="open" />} />
          <Route path="tickets/history" element={<Navigate to="/agent/tickets/assigned" replace />} />
          <Route path="tickets/:id" element={<AgentTicketDetailsPage />} />
          <Route path="notifications" element={<NotificationsPage roleArea="agent" />} />
          <Route path="profile" element={<ProfilePage />} />
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
          <Route path="workload/:agentId" element={<AgentWorkloadTicketsPage roleArea="manager" />} />
          <Route path="audit-log" element={<AdminActivityPage roleArea="manager" />} />
         <Route path="activity" element={<Navigate to="/manager/audit-log" replace />} />
          <Route path="notifications" element={<NotificationsPage roleArea="manager" />} />
          <Route path="profile" element={<ProfilePage />} />
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
          <Route path="workload" element={<ManagerWorkloadPage roleArea="admin" />} />
          <Route path="workload/:agentId" element={<AgentWorkloadTicketsPage roleArea="admin" />} />
          <Route path="users" element={<AdminUsersPage />} />
          <Route path="users/:userId" element={<AdminUserDetailsPage />} />
          <Route path="categories" element={<AdminCategoriesPage />} />
          <Route path="audit-log" element={<AdminActivityPage roleArea="admin" />} />
          <Route path="activity" element={<Navigate to="/admin/audit-log" replace />} />
          <Route path="notifications" element={<NotificationsPage roleArea="admin" />} />
          <Route path="profile" element={<ProfilePage />} />
        </Route>
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
