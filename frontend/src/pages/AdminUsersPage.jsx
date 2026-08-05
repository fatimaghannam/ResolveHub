import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { MoreHorizontal, Plus } from 'lucide-react'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import Toast from '../components/common/Toast.jsx'
import { getAdminUsers, updateAdminUserStatus } from '../services/adminService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'

function AdminUsersPage() {
  const { user: authenticatedUser } = useOutletContext()
  const [users, setUsers] = useState(null)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [role, setRole] = useState('')
  const [status, setStatus] = useState('')
  const [showAdd, setShowAdd] = useState(false)
  const [toast, setToast] = useState(null)
  const dismissToast = useCallback(() => setToast(null), [])
  const filtered = useMemo(() => (users ?? []).filter((user) => {
    const query = search.trim().toLowerCase()
    return (!query || `${user.firstName} ${user.lastName} ${user.email}`.toLowerCase().includes(query)) &&
      (!role || user.role === role) && (!status || user.status === status)
  }), [users, search, role, status])

  useEffect(() => {
    const controller = new AbortController()
    getAdminUsers(controller.signal)
      .then(setUsers)
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setError(requestError.message)
      })
    return () => controller.abort()
  }, [])

  function isCurrentAdministrator(user) {
    const sameEmail = authenticatedUser?.email &&
      authenticatedUser.email.toLowerCase() === user.email.toLowerCase()
    const sameName = authenticatedUser?.firstName === user.firstName &&
      authenticatedUser?.lastName === user.lastName
    return user.role === 'Administrator' && (sameEmail || sameName)
  }

  async function toggleStatus(user, event) {
    event.currentTarget.closest('details')?.removeAttribute('open')
    try {
      setError('')
      const isActive = user.status !== 'Active'
      await updateAdminUserStatus(user.id, isActive)
      setUsers((current) => current.map((item) => item.id === user.id
        ? { ...item, status: isActive ? 'Active' : 'Inactive' }
        : item))
      setToast({
        id: Date.now(),
        type: 'success',
        title: isActive ? 'User Activated' : 'User Deactivated',
        message: `${user.firstName} ${user.lastName} was ${isActive ? 'activated' : 'deactivated'} successfully.`,
      })
    } catch (requestError) {
      setToast({
        id: Date.now(),
        type: 'error',
        title: 'Unable to Update User',
        message: requestError.message,
      })
    }
  }

  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <section className="page-heading page-heading--action"><div><h2>Users</h2><p>View and manage ResolveHub user accounts and roles.</p></div><button className="button button--primary" type="button" onClick={() => setShowAdd(true)}><Plus size={18} />Add User</button></section>
      <section className="filter-panel admin-user-filters">
        <label className="filter-search"><span>Search</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Name or email" /></label>
        <label><span>Role</span><select value={role} onChange={(event) => setRole(event.target.value)}><option value="">All</option>{['Employee', 'IT Support Agent', 'Manager', 'Administrator'].map((value) => <option key={value}>{value}</option>)}</select></label>
        <label><span>Status</span><select value={status} onChange={(event) => setStatus(event.target.value)}><option value="">All</option><option>Active</option><option>Inactive</option></select></label>
      </section>
      {error && <ErrorState message={error} />}
      {!error && users === null && <LoadingState message="Loading users…" />}
      {users !== null &&
      <section className="panel">
        <div className="results-count">{filtered.length} user{filtered.length === 1 ? '' : 's'}</div>
        {filtered.length === 0 ? <EmptyState title="No users found" message="Try changing the current search or filters." /> : <div className="table-scroll admin-users-table-wrap"><table className="ticket-table admin-users-table">
          <colgroup>
            <col className="users-col--name" />
            <col className="users-col--email" />
            <col className="users-col--role" />
            <col className="users-col--department" />
            <col className="users-col--status" />
            <col className="users-col--created" />
            <col className="users-col--actions" />
          </colgroup>
          <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Department</th><th>Status</th><th>Created</th><th>Action</th></tr></thead>
          <tbody>{filtered.map((user) => {
            const currentAdministrator = isCurrentAdministrator(user)
            return <tr key={user.id}>
              <td><strong className="users-cell-ellipsis" title={`${user.firstName} ${user.lastName}`}>{user.firstName} {user.lastName}</strong></td>
              <td><span className="users-cell-ellipsis" title={user.email}>{user.email}</span></td>
              <td><span className="users-role" title={user.role}>{user.role}</span></td>
              <td><span className="users-department" title={user.department}>{user.department}</span></td>
              <td><span className={`user-status user-status--${user.status.toLowerCase()}`}>{user.status}</span></td>
              <td className="users-created">{formatLocalDateTime(user.createdDate)}</td>
              <td>
                <details className="row-action-menu">
                  <summary aria-label={`Actions for ${user.firstName} ${user.lastName}`}><MoreHorizontal size={19} aria-hidden="true" /></summary>
                  <div className="row-action-menu__items">
                    <Link to={`/admin/users/${user.id}`}>View</Link>
                    {currentAdministrator && user.status === 'Active'
                      ? <span className="row-action-menu__disabled" title="You cannot deactivate your own Administrator account.">Current account</span>
                      : <button type="button" onClick={(event) => toggleStatus(user, event)}>{user.status === 'Active' ? 'Deactivate' : 'Activate'}</button>}
                  </div>
                </details>
              </td>
            </tr>
          })}</tbody>
        </table></div>}
      </section>}
      {showAdd && <div className="dialog-backdrop" role="presentation"><div className="dialog admin-user-dialog" role="dialog" aria-modal="true" aria-labelledby="add-user-title"><h2 id="add-user-title">Add User</h2><p>User creation will be enabled when the Administrator API is connected.</p><div className="form-grid">{['First name', 'Last name', 'Email', 'Department'].map((label) => <label key={label}><span>{label}</span><input disabled /></label>)}<label><span>Role</span><select disabled><option>Employee</option></select></label></div><div className="dialog__actions"><button autoFocus className="button button--secondary" type="button" onClick={() => setShowAdd(false)}>Close</button><button className="button button--primary" type="button" disabled>Backend connection pending</button></div></div></div>}
    </>
  )
}

export default AdminUsersPage
