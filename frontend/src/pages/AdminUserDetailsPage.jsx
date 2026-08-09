import { ArrowLeft } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { EmptyState, ErrorState, LoadingState } from '../components/common/States.jsx'
import { getAdminUser } from '../services/adminService.js'
import { accountStatusClassName, formatAccountStatus } from '../utils/accountStatus.js'
import { formatLocalDateTime } from '../utils/dateTime.js'
import UserAvatar from '../components/common/UserAvatar.jsx'

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

  const fullName = `${user.firstName} ${user.lastName}`.trim()
  const displayStatus = formatAccountStatus(user.status)
  const showDepartment = user.role === 'Manager' || Boolean(user.department)
  return (
    <div className="admin-user-details-page">
      <Link className="back-link back-link--top" to="/admin/users"><ArrowLeft size={18} />Back to Users</Link>
      <section className="page-heading admin-user-details-heading"><h2>User Details</h2><p>View account information, access level, and current status.</p></section>
      <section className="panel admin-user-details-card">
        <header className="admin-user-identity">
          <UserAvatar
            className="admin-user-identity__avatar"
            firstName={user.firstName}
            lastName={user.lastName}
            imagePath={user.profileImagePath}
            aria-hidden="true"
          />
          <div className="admin-user-identity__content">
            <h3>{fullName}</h3>
            <a href={`mailto:${user.email}`}>{user.email}</a>
          </div>
        </header>

        <div className="admin-user-details-divider" />
        <div className="admin-user-information-grid">
          <section aria-labelledby="profile-information-title">
            <h4 id="profile-information-title">Profile Information</h4>
            <dl className="admin-user-information-list">
              <div><dt>Full name</dt><dd>{fullName}</dd></div>
              <div><dt>Email</dt><dd className="admin-user-information-email">{user.email}</dd></div>
              <div><dt>Role</dt><dd>{user.role}</dd></div>
              {showDepartment && <div><dt>Department</dt><dd>{user.department ?? '—'}</dd></div>}
            </dl>
          </section>
          <section aria-labelledby="account-information-title">
            <h4 id="account-information-title">Account Information</h4>
            <dl className="admin-user-information-list">
              <div><dt>Account status</dt><dd><span className={`user-status user-status--${accountStatusClassName(user.status)}`}>{displayStatus}</span></dd></div>
              <div><dt>Created</dt><dd><time dateTime={user.createdDate}>{formatLocalDateTime(user.createdDate)}</time></dd></div>
              {user.lastLoginDate && <div><dt>Last login</dt><dd><time dateTime={user.lastLoginDate}>{formatLocalDateTime(user.lastLoginDate)}</time></dd></div>}
            </dl>
          </section>
        </div>
      </section>
    </div>
  )
}

export default AdminUserDetailsPage
