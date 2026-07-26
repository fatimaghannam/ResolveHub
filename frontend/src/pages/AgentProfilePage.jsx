import { useOutletContext } from 'react-router-dom'

function displayValue(value) {
  return value || 'Not available'
}

function AgentProfilePage() {
  const { user, role } = useOutletContext()
  const fullName = [user?.firstName, user?.lastName].filter(Boolean).join(' ')

  return (
    <>
      <section className="page-heading">
        <h2>Profile</h2>
        <p>Review the account information available for your ResolveHub profile.</p>
      </section>
      <section className="panel profile-panel">
        <div className="profile-summary">
          <span className="avatar avatar--large">{user?.firstName?.[0]?.toUpperCase() ?? 'A'}</span>
          <div><h2>{displayValue(fullName)}</h2><p>{role}</p></div>
        </div>
        <dl className="profile-details">
          <div><dt>Full name</dt><dd>{displayValue(fullName)}</dd></div>
          <div><dt>Email</dt><dd>{displayValue(user?.email)}</dd></div>
          <div><dt>Role</dt><dd>{displayValue(role)}</dd></div>
          <div><dt>Department</dt><dd>{displayValue(user?.departmentName ?? user?.department)}</dd></div>
        </dl>
      </section>
    </>
  )
}

export default AgentProfilePage
