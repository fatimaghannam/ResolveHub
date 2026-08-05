import { ArrowLeft } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { getAdminUser } from '../services/adminService.js'
import { formatLocalDateTime } from '../utils/dateTime.js'

function AdminUserDetailsPage() {
  const { userId } = useParams()
  const [user, setUser] = useState(null)
  const [error, setError] = useState('')
  const [notFound, setNotFound] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    setError('')
    setNotFound(false)
    getAdminUser(userId, controller.signal)
      .then(setUser)
      .catch((requestError) => {
        if (requestError.name === 'AbortError') return
        if (requestError.status === 404) setNotFound(true)
        else setError(requestError.message)
      })
    return () => controller.abort()
  }, [userId])

  if (notFound) return <EmptyState title="User not found" message="This user account does not exist." action={<Link className="button button--secondary" to="/admin/users">Back to Users</Link>} />
  if (error) return <ErrorState message={error} />
  if (!user) return <LoadingState message="Loading user…" />

  const initials = `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase()
  return (
    <>
      <Link className="back-link back-link--top" to="/admin/users"><ArrowLeft size={18} />Back to Users</Link>
      <section className="page-heading"><h2>{user.firstName} {user.lastName}</h2><p>Read-only account details</p></section>
      <section className="panel profile-panel">
        <div className="admin-user-profile-heading"><span className="profile-avatar" aria-hidden="true">{initials}</span><div><h3>Profile</h3><p>{user.email}</p></div></div>
        <dl className="profile-details"><div><dt>Full name</dt><dd>{user.firstName} {user.lastName}</dd></div><div><dt>Email</dt><dd>{user.email}</dd></div><div><dt>Role</dt><dd>{user.role}</dd></div><div><dt>Department</dt><dd>{user.department ?? '—'}</dd></div><div><dt>Status</dt><dd><span className={`user-status user-status--${user.status.toLowerCase().replaceAll(' ', '-')}`}>{user.status}</span></dd></div></dl>
      </section>
      <section className="panel profile-panel"><h3>Account Information</h3><dl className="profile-details"><div><dt>Created</dt><dd>{formatLocalDateTime(user.createdDate)}</dd></div>{user.lastLoginDate && <div><dt>Last login</dt><dd>{formatLocalDateTime(user.lastLoginDate)}</dd></div>}<div><dt>Account ID</dt><dd>{user.id}</dd></div></dl></section>
    </>
  )
}

export default AdminUserDetailsPage
