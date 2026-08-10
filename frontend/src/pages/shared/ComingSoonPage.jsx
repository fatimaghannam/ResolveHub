import { Link } from 'react-router-dom'

function ComingSoonPage({ roleArea = 'employee' }) {
  return <div className="state-panel"><h2>Notifications</h2><Link className="button button--primary" to={`/${roleArea}/dashboard`}>Back to Dashboard</Link></div>
}

export default ComingSoonPage
