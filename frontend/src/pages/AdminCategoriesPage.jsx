import { categoryData } from '../data/index.js'

function AdminCategoriesPage() {
  return (
    <>
      <section className="page-heading"><h2>Ticket Categories</h2><p>Manage categories used to organize support requests.</p></section>
      <section className="panel"><div className="table-scroll"><table className="ticket-table"><thead><tr><th>Category Name</th><th>Description</th><th>Active Tickets</th><th>Status</th><th>Action</th></tr></thead><tbody>{categoryData.map((category) => <tr key={category.id}><td><strong>{category.name}</strong></td><td>{category.description}</td><td>{category.activeTickets}</td><td><span className="user-status user-status--active">{category.status}</span></td><td><button className="table-action table-action--button" type="button" disabled title="Backend connection pending">Manage</button></td></tr>)}</tbody></table></div></section>
    </>
  )
}

export default AdminCategoriesPage
