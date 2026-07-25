import { Link } from 'react-router-dom'

function ComingSoonPage() {
  return <div className="state-panel"><h2>Coming soon</h2><p>This feature is planned for a future ResolveHub release.</p><Link className="button button--primary" to="/employee/dashboard">Back to Dashboard</Link></div>
}

export default ComingSoonPage
