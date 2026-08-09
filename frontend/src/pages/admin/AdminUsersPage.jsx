import { useCallback, useEffect, useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { MoreHorizontal, Plus } from 'lucide-react'
import { EmptyState, ErrorState, LoadingState } from '../../components/common/States.jsx'
import Toast from '../../components/common/Toast.jsx'
import { createAdminUser, getAdminUserDepartments, getAdminUsers, resendAdminUserInvitation, updateAdminUserStatus } from '../../services/adminService.js'
import { accountStatusClassName, formatAccountStatus } from '../../utils/accountStatus.js'
import { formatLocalDate, formatLocalDateTime, formatLocalTime } from '../../utils/dateTime.js'

const emptyUserForm = { firstName: '', lastName: '', email: '', departmentId: '', role: 'Employee' }

function AdminUsersPage() {
  const { user: authenticatedUser } = useOutletContext()
  const [users, setUsers] = useState(null)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [role, setRole] = useState('')
  const [department, setDepartment] = useState('')
  const [status, setStatus] = useState('')
  const [showAdd, setShowAdd] = useState(false)
  const [departments, setDepartments] = useState([])
  const [form, setForm] = useState(emptyUserForm)
  const [formError, setFormError] = useState('')
  const [creating, setCreating] = useState(false)
  const [statusTarget, setStatusTarget] = useState(null)
  const [updatingStatus, setUpdatingStatus] = useState(false)
  const [reload, setReload] = useState(0)
  const [resendingId, setResendingId] = useState(null)
  const [toast, setToast] = useState(null)
  const dismissToast = useCallback(() => setToast(null), [])
  const filtered = users ?? []

  useEffect(() => {
    const controller = new AbortController()
    getAdminUserDepartments(controller.signal)
      .then(setDepartments)
      .catch((requestError) => {
        if (requestError.name !== 'AbortError') setError(requestError.message)
      })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      const filters = { search: search.trim(), role, status }
      if (role === 'Manager' && department === 'unassigned') {
        filters.unassignedDepartment = true
      } else if (role === 'Manager' && department) {
        filters.departmentId = department
      }
      getAdminUsers(filters, controller.signal)
        .then((rows) => {
          setUsers(rows)
          setError('')
        })
        .catch((requestError) => {
          if (requestError.name !== 'AbortError') setError(requestError.message)
        })
    }, 200)
    return () => {
      window.clearTimeout(timer)
      controller.abort()
    }
  }, [search, role, department, status, reload])

  useEffect(() => {
    function refreshWhenVisible() {
      if (document.visibilityState === 'visible') setReload((value) => value + 1)
    }
    window.addEventListener('focus', refreshWhenVisible)
    document.addEventListener('visibilitychange', refreshWhenVisible)
    return () => {
      window.removeEventListener('focus', refreshWhenVisible)
      document.removeEventListener('visibilitychange', refreshWhenVisible)
    }
  }, [])

  function changeRole(event) {
    const value = event.target.value
    setRole(value)
    if (value !== 'Manager') setDepartment('')
  }

  function clearFilters() {
    setSearch('')
    setRole('')
    setDepartment('')
    setStatus('')
  }

  function isCurrentAdministrator(user) {
    const sameEmail = authenticatedUser?.email &&
      authenticatedUser.email.toLowerCase() === user.email.toLowerCase()
    const sameName = authenticatedUser?.firstName === user.firstName &&
      authenticatedUser?.lastName === user.lastName
    return user.role === 'Administrator' && (sameEmail || sameName)
  }

  function requestStatusChange(user, event) {
    event.currentTarget.closest('details')?.removeAttribute('open')
    setStatusTarget(user)
  }

  async function toggleStatus() {
    const user = statusTarget
    if (!user || updatingStatus) return
    try {
      setError('')
      setUpdatingStatus(true)
      const isActive = user.status !== 'Active'
      await updateAdminUserStatus(user.id, isActive)
      setReload((value) => value + 1)
      setToast({
        id: Date.now(),
        type: 'success',
        title: isActive ? 'User Activated' : 'User Deactivated',
        message: `${user.firstName} ${user.lastName} was ${isActive ? 'activated' : 'deactivated'} successfully.`,
      })
      setStatusTarget(null)
    } catch (requestError) {
      setToast({
        id: Date.now(),
        type: 'error',
        title: 'Unable to Update User',
        message: requestError.message,
      })
    } finally {
      setUpdatingStatus(false)
    }
  }

  function openAddUser() {
    setForm(emptyUserForm)
    setFormError('')
    setShowAdd(true)
  }

  function updateForm(event) {
    const { name, value } = event.target
    setForm((current) => ({
      ...current,
      [name]: value,
      ...(name === 'role' && value !== 'Manager' ? { departmentId: '' } : {}),
    }))
  }

  async function submitUser(event) {
    event.preventDefault()
    if (creating) return
    const request = {
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      email: form.email.trim(),
      departmentId: form.role === 'Manager' ? Number(form.departmentId) : null,
      role: form.role,
    }
    if (!request.firstName || !request.lastName || !request.email || !request.role ||
      (request.role === 'Manager' && !form.departmentId)) {
      setFormError('All fields are required.')
      return
    }
    try {
      setCreating(true)
      setFormError('')
      const result = await createAdminUser(request)
      setReload((value) => value + 1)
      setShowAdd(false)
      setToast({
        id: Date.now(),
        type: result.invitationSent ? 'success' : 'warning',
        title: 'User Created',
        message: result.invitationSent
          ? 'User created with Pending status and invitation sent successfully.'
          : 'User created with Pending status, but the invitation email could not be sent.',
      })
    } catch (requestError) {
      setFormError(requestError.message)
    } finally {
      setCreating(false)
    }
  }

  async function resendInvitation(user, event) {
    event.currentTarget.closest('details')?.removeAttribute('open')
    if (resendingId !== null) return
    try {
      setResendingId(user.id)
      await resendAdminUserInvitation(user.id)
      setToast({ id: Date.now(), type: 'success', title: 'Invitation Sent', message: `A new invitation was sent to the Pending account ${user.email}.` })
    } catch (requestError) {
      setToast({ id: Date.now(), type: 'error', title: 'Invitation Not Sent', message: requestError.message })
    } finally {
      setResendingId(null)
    }
  }

  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <section className="page-heading page-heading--action"><div><h2>Users</h2><p>View and manage ResolveHub user accounts and roles.</p></div><button className="button button--primary" type="button" onClick={openAddUser}><Plus size={18} />Add User</button></section>
      <section className={`filter-panel admin-user-filters${role === 'Manager' ? ' admin-user-filters--manager' : ''}`}>
        <label className="filter-search"><span>Search</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Name or email" /></label>
        <label><span>Role</span><select value={role} onChange={changeRole}><option value="">All</option>{['Employee', 'IT Support Agent', 'Manager', 'Administrator'].map((value) => <option key={value}>{value}</option>)}</select></label>
        {role === 'Manager' && <label><span>Department</span><select value={department} onChange={(event) => setDepartment(event.target.value)}><option value="">All Departments</option><option value="unassigned">Unassigned Department</option>{departments.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
        <label><span>Status</span><select value={status} onChange={(event) => setStatus(event.target.value)}><option value="">All</option><option>Pending</option><option>Active</option><option>Inactive</option></select></label>
        <button className="button button--secondary admin-user-filters__clear" type="button" onClick={clearFilters} disabled={!search && !role && !department && !status}>Clear</button>
      </section>
      {error && <ErrorState message={error} />}
      {!error && users === null && <LoadingState message="Loading users…" />}
      {users !== null &&
      <section className="panel admin-users-panel">
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
              <td><span className={`users-department${user.department ? '' : ' users-department--empty'}`} title={user.department ?? '—'}>{user.department ?? '—'}</span></td>
              <td><span className={`user-status user-status--${accountStatusClassName(user.status)}`}>{formatAccountStatus(user.status)}</span></td>
              <td className="users-created"><time dateTime={user.createdDate} title={formatLocalDateTime(user.createdDate)}><span>{formatLocalDate(user.createdDate)}</span><span>{formatLocalTime(user.createdDate)}</span></time></td>
              <td>
                <details className="row-action-menu">
                  <summary aria-label={`Actions for ${user.firstName} ${user.lastName}`}><MoreHorizontal size={19} aria-hidden="true" /></summary>
                  <div className="row-action-menu__items">
                    <Link to={`/admin/users/${user.id}`}>View</Link>
                    {formatAccountStatus(user.status) === 'Pending' && <button type="button" onClick={(event) => resendInvitation(user, event)} disabled={resendingId !== null}>{resendingId === user.id ? 'Sending…' : 'Resend Invitation'}</button>}
                    {currentAdministrator && user.status === 'Active'
                      ? <span className="row-action-menu__disabled" title="You cannot deactivate your own Administrator account.">Current account</span>
                      : <button type="button" onClick={(event) => requestStatusChange(user, event)}>{user.status === 'Active' ? 'Deactivate' : 'Activate'}</button>}
                  </div>
                </details>
              </td>
            </tr>
          })}</tbody>
        </table></div>}
      </section>}
      {showAdd && <div className="dialog-backdrop" role="presentation"><form className="dialog admin-user-dialog" role="dialog" aria-modal="true" aria-labelledby="add-user-title" onSubmit={submitUser}><h2 id="add-user-title">Add User</h2><p>The user will receive a secure email link to set their password.</p><div className="form-grid"><label><span>First name</span><input name="firstName" value={form.firstName} onChange={updateForm} maxLength={100} autoComplete="given-name" required /></label><label><span>Last name</span><input name="lastName" value={form.lastName} onChange={updateForm} maxLength={100} autoComplete="family-name" required /></label><label><span>Email</span><input name="email" type="email" value={form.email} onChange={updateForm} maxLength={255} autoComplete="email" required /></label><label><span>Role</span><select name="role" value={form.role} onChange={updateForm} required>{['Employee', 'IT Support Agent', 'Manager', 'Administrator'].map((value) => <option key={value}>{value}</option>)}</select></label>{form.role === 'Manager' && <label><span>Department</span><select name="departmentId" value={form.departmentId} onChange={updateForm} required><option value="">Select department</option>{departments.map((department) => <option key={department.id} value={department.id}>{department.name}</option>)}</select></label>}</div>{formError && <p className="form-error" role="alert">{formError}</p>}<div className="dialog__actions"><button autoFocus className="button button--secondary" type="button" onClick={() => setShowAdd(false)} disabled={creating}>Cancel</button><button className="button button--primary" type="submit" disabled={creating}>{creating ? 'Creating…' : 'Create User'}</button></div></form></div>}
      {statusTarget && <div className="dialog-backdrop" role="presentation"><div className="dialog" role="dialog" aria-modal="true" aria-labelledby="user-status-title"><h2 id="user-status-title">{statusTarget.status === 'Active' ? 'Deactivate' : 'Activate'} user?</h2><p>{statusTarget.status === 'Active' ? 'This user will no longer be able to sign in.' : 'This user will regain access to ResolveHub.'}</p><div className="dialog__actions"><button autoFocus className="button button--secondary" type="button" onClick={() => setStatusTarget(null)} disabled={updatingStatus}>Cancel</button><button className={statusTarget.status === 'Active' ? 'button button--danger' : 'button button--primary'} type="button" onClick={toggleStatus} disabled={updatingStatus}>{updatingStatus ? 'Updating…' : statusTarget.status === 'Active' ? 'Deactivate' : 'Activate'}</button></div></div></div>}
    </>
  )
}

export default AdminUsersPage
