import { useOutletContext } from 'react-router-dom'

function AdminProfilePage() {
  const { user, role } = useOutletContext()
  const fullName = [user?.firstName, user?.lastName].filter(Boolean).join(' ')
  const details = [
    ['Full name', fullName],
    ['Email', user?.email],
    ['Role', role],
    ['Department', user?.departmentName ?? user?.department],
  ].filter(([, value]) => value)

  return (
    <>
      <section className="page-heading"><h2>Profile</h2><p>Review your authenticated ResolveHub account information.</p></section>
      <section className="panel profile-panel"><dl className="profile-details">{details.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl><p className="profile-note">Profile editing will be available after the Administrator account API is connected.</p></section>
    </>
  )
}

export default AdminProfilePage
