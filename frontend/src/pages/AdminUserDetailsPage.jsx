import { ArrowLeft } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { EmptyState } from '../components/common/States.jsx'
import { usersMockData } from '../data/index.js'
import { formatLocalDate } from '../utils/dateTime.js'

function AdminUserDetailsPage() {
  const { userId } = useParams()
  const user = usersMockData.find((item) => item.id === Number(userId))
  if (!user) return <EmptyState title="User not found" message="This temporary user record is not available." action={<Link className="button button--secondary" to="/admin/users">Back to Users</Link>} />
  return (
    <>
      <Link className="back-link back-link--top" to="/admin/users"><ArrowLeft size={18} />Back to Users</Link>
      <section className="page-heading"><h2>{user.firstName} {user.lastName}</h2><p>Read-only account details</p></section>
      <section className="panel profile-panel"><dl className="profile-details"><div><dt>Email</dt><dd>{user.email}</dd></div><div><dt>Role</dt><dd>{user.role}</dd></div><div><dt>Department</dt><dd>{user.department}</dd></div><div><dt>Status</dt><dd>{user.status}</dd></div><div><dt>Created</dt><dd>{formatLocalDate(user.createdDate)}</dd></div></dl></section>
    </>
  )
}

export default AdminUserDetailsPage
