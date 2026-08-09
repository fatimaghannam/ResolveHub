import { useOutletContext } from 'react-router-dom'
import { accountStatusClassName, formatAccountStatus } from '../utils/accountStatus.js'
import { formatLocalDateTime } from '../utils/dateTime.js'

function ProfilePage() {
  const { user, role } = useOutletContext()
  const fullName = [user?.firstName, user?.lastName].filter(Boolean).join(' ')
  const initials = [user?.firstName, user?.lastName]
    .filter(Boolean)
    .map((name) => name[0])
    .join('')
    .toUpperCase()
  const status = user?.status ?? (user?.isActive === false ? 'Inactive' : 'Active')

  return (
    <div className="admin-user-details-page profile-page">
      <section className="page-heading admin-user-details-heading">
        <h2>Profile</h2>
        <p>View your account information, access level, and current status.</p>
      </section>
      <section className="panel admin-user-details-card">
        <header className="admin-user-identity">
          <span className="profile-avatar admin-user-identity__avatar" aria-hidden="true">{initials}</span>
          <div className="admin-user-identity__content">
            <h3>{fullName}</h3>
            <a href={`mailto:${user?.email}`}>{user?.email}</a>
          </div>
        </header>

        <div className="admin-user-details-divider" />
        <div className="admin-user-information-grid">
          <section aria-labelledby="profile-information-title">
            <h4 id="profile-information-title">Profile Information</h4>
            <dl className="admin-user-information-list">
              <div><dt>Full name</dt><dd>{fullName}</dd></div>
              <div><dt>Email</dt><dd className="admin-user-information-email">{user?.email}</dd></div>
              <div><dt>Role</dt><dd>{role}</dd></div>
            </dl>
          </section>
          <section aria-labelledby="account-information-title">
            <h4 id="account-information-title">Account Information</h4>
            <dl className="admin-user-information-list">
              <div><dt>Account status</dt><dd><span className={`user-status user-status--${accountStatusClassName(status)}`}>{formatAccountStatus(status)}</span></dd></div>
              <div><dt>Created</dt><dd><time dateTime={user?.createdDate}>{formatLocalDateTime(user?.createdDate)}</time></dd></div>
            </dl>
          </section>
        </div>
      </section>
    </div>
  )
}

export default ProfilePage
