import { MoreHorizontal, Plus } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { EmptyState, ErrorState, LoadingState } from '../../components/common/States.jsx'
import Toast from '../../components/common/Toast.jsx'
import { createAdminCategory, getAdminCategories, updateAdminCategory, updateAdminCategoryStatus } from '../../services/adminService.js'

const emptyForm = { name: '', description: '' }

function AdminCategoriesPage() {
  const [categories, setCategories] = useState(null)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)
  const [editing, setEditing] = useState(null)
  const [form, setForm] = useState(emptyForm)
  const [fieldErrors, setFieldErrors] = useState({})
  const [saving, setSaving] = useState(false)
  const [statusTarget, setStatusTarget] = useState(null)
  const [updatingStatus, setUpdatingStatus] = useState(false)
  const [toast, setToast] = useState(null)
  const [openMenu, setOpenMenu] = useState(null)
  const [menuPosition, setMenuPosition] = useState(null)
  const menuRef = useRef(null)
  const menuButtonRef = useRef(null)
  const dismissToast = useCallback(() => setToast(null), [])

  useEffect(() => {
    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      getAdminCategories({ search: search.trim(), status }, controller.signal)
        .then((items) => { setCategories(items); setError('') })
        .catch((requestError) => {
          if (requestError.name !== 'AbortError') setError('We could not load the ticket categories. Please try again.')
        })
    }, 200)
    return () => { window.clearTimeout(timer); controller.abort() }
  }, [search, status, reload])

  const positionMenu = useCallback(() => {
    const button = menuButtonRef.current
    if (!button) return
    const rect = button.getBoundingClientRect()
    const width = 168
    const height = menuRef.current?.offsetHeight ?? 86
    const gap = 6
    const edge = 8
    const opensAbove = window.innerHeight - rect.bottom < height + gap && rect.top >= height + gap
    setMenuPosition({
      left: Math.max(edge, Math.min(rect.right - width, window.innerWidth - width - edge)),
      top: Math.max(edge, Math.min(opensAbove ? rect.top - height - gap : rect.bottom + gap, window.innerHeight - height - edge)),
      width,
    })
  }, [])

  useEffect(() => {
    if (!openMenu) return undefined
    positionMenu()
    function closeOutside(event) {
      if (!menuRef.current?.contains(event.target) && !menuButtonRef.current?.contains(event.target)) setOpenMenu(null)
    }
    function closeOnEscape(event) {
      if (event.key === 'Escape') {
        setOpenMenu(null)
        menuButtonRef.current?.focus()
      }
    }
    document.addEventListener('pointerdown', closeOutside)
    window.addEventListener('keydown', closeOnEscape)
    window.addEventListener('resize', positionMenu)
    window.addEventListener('scroll', positionMenu, true)
    return () => {
      document.removeEventListener('pointerdown', closeOutside)
      window.removeEventListener('keydown', closeOnEscape)
      window.removeEventListener('resize', positionMenu)
      window.removeEventListener('scroll', positionMenu, true)
    }
  }, [openMenu, positionMenu])

  useEffect(() => {
    if (!openMenu || !menuPosition) return undefined
    const frame = window.requestAnimationFrame(() =>
      menuRef.current?.querySelector('button')?.focus())
    return () => window.cancelAnimationFrame(frame)
  }, [openMenu, menuPosition])

  function toggleMenu(category, event) {
    event.stopPropagation()
    if (openMenu?.id === category.id) {
      setOpenMenu(null)
      return
    }
    menuButtonRef.current = event.currentTarget
    setMenuPosition(null)
    setOpenMenu(category)
    window.requestAnimationFrame(positionMenu)
  }

  function menuKeyDown(event) {
    const items = [...event.currentTarget.querySelectorAll('button:not(:disabled)')]
    const index = items.indexOf(document.activeElement)
    if (event.key === 'ArrowDown') {
      event.preventDefault(); items[(index + 1) % items.length]?.focus()
    } else if (event.key === 'ArrowUp') {
      event.preventDefault(); items[(index - 1 + items.length) % items.length]?.focus()
    } else if (event.key === 'Home') {
      event.preventDefault(); items[0]?.focus()
    } else if (event.key === 'End') {
      event.preventDefault(); items.at(-1)?.focus()
    }
  }

  function openCreate() {
    setEditing({ mode: 'create' })
    setForm(emptyForm)
    setFieldErrors({})
  }

  function openEdit(category) {
    setOpenMenu(null)
    setEditing({ mode: 'edit', category })
    setForm({ name: category.name, description: category.description })
    setFieldErrors({})
  }

  function validate() {
    const errors = {}
    if (!form.name.trim()) errors.name = 'Category Name is required.'
    if (!form.description.trim()) errors.description = 'Description is required.'
    setFieldErrors(errors)
    return Object.keys(errors).length === 0
  }

  async function saveCategory(event) {
    event.preventDefault()
    if (saving || !validate()) return
    try {
      setSaving(true)
      const request = { name: form.name.trim(), description: form.description.trim() }
      if (editing.mode === 'edit') await updateAdminCategory(editing.category.id, request)
      else await createAdminCategory(request)
      setEditing(null)
      setReload((value) => value + 1)
      setToast({ id: Date.now(), type: 'success', title: editing.mode === 'edit' ? 'Category Updated' : 'Category Created', message: editing.mode === 'edit' ? 'Category updated successfully.' : 'Category created successfully.' })
    } catch (requestError) {
      const message = requestError.message
      if (requestError.status === 409) setFieldErrors({ name: message })
      else setFieldErrors({ form: message })
    } finally {
      setSaving(false)
    }
  }

  async function changeStatus() {
    if (!statusTarget || updatingStatus) return
    const activating = !statusTarget.isActive
    try {
      setUpdatingStatus(true)
      await updateAdminCategoryStatus(statusTarget.id, activating)
      setStatusTarget(null)
      setReload((value) => value + 1)
      setToast({ id: Date.now(), type: 'success', title: activating ? 'Category Activated' : 'Category Deactivated', message: activating ? 'Category activated successfully.' : 'Category deactivated successfully.' })
    } catch (requestError) {
      setToast({ id: Date.now(), type: 'error', title: 'Unable to Update Category', message: requestError.message })
    } finally {
      setUpdatingStatus(false)
    }
  }

  const hasFilters = Boolean(search || status)
  return (
    <>
      {toast && <div className="app-toast-region"><Toast key={toast.id} type={toast.type} title={toast.title} message={toast.message} onDismiss={dismissToast} /></div>}
      <section className="page-heading page-heading--action"><div><h2>Ticket Categories</h2><p>Manage categories used to organize support requests.</p></div><button className="button button--primary" type="button" onClick={openCreate}><Plus size={18} />Add Category</button></section>
      <section className="filter-panel admin-category-filters"><label className="filter-search"><span>Search</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Category name or description" /></label><label><span>Status</span><select value={status} onChange={(event) => setStatus(event.target.value)}><option value="">All</option><option>Active</option><option>Inactive</option></select></label><button className="button button--secondary" type="button" disabled={!hasFilters} onClick={() => { setSearch(''); setStatus('') }}>Clear</button></section>
      {error && <ErrorState message={error} onRetry={() => setReload((value) => value + 1)} />}
      {!error && categories === null && <LoadingState message="Loading categories…" />}
      {!error && categories && <section className="panel admin-categories-panel">{categories.length === 0 ? <EmptyState title={hasFilters ? 'No matching categories' : 'No ticket categories'} message={hasFilters ? 'No categories match the selected filters.' : 'No ticket categories are available.'} /> : <div className="admin-categories-table-wrap"><table className="ticket-table admin-categories-table"><colgroup><col className="category-col--name" /><col className="category-col--description" /><col className="category-col--tickets" /><col className="category-col--status" /><col className="category-col--action" /></colgroup><thead><tr><th>Category Name</th><th>Description</th><th>Active Tickets</th><th>Status</th><th>Action</th></tr></thead><tbody>{categories.map((category) => <tr key={category.id}><td><strong>{category.name}</strong></td><td className="category-description">{category.description}</td><td className="category-active-count">{category.activeTickets}</td><td><span className={`user-status user-status--${category.isActive ? 'active' : 'inactive'}`}>{category.isActive ? 'Active' : 'Inactive'}</span></td><td><button className="category-action-trigger" type="button" aria-label={`Actions for ${category.name}`} aria-haspopup="menu" aria-expanded={openMenu?.id === category.id} onClick={(event) => toggleMenu(category, event)} onKeyDown={(event) => { if (event.key === 'ArrowDown') { event.preventDefault(); if (openMenu?.id !== category.id) toggleMenu(category, event) } }}><MoreHorizontal size={19} aria-hidden="true" /></button></td></tr>)}</tbody></table></div>}</section>}

      {openMenu && menuPosition && createPortal(<div ref={menuRef} className="category-action-dropdown" role="menu" aria-label={`Actions for ${openMenu.name}`} style={menuPosition} onKeyDown={menuKeyDown} onClick={(event) => event.stopPropagation()}><button type="button" role="menuitem" onClick={() => openEdit(openMenu)}>Edit</button><button className={openMenu.isActive ? 'category-action-dropdown__deactivate' : 'category-action-dropdown__activate'} type="button" role="menuitem" onClick={() => { const category = openMenu; setOpenMenu(null); setStatusTarget(category) }}>{openMenu.isActive ? 'Deactivate' : 'Activate'}</button></div>, document.body)}

      {editing && <div className="dialog-backdrop" role="presentation"><form className="dialog admin-category-dialog" role="dialog" aria-modal="true" aria-labelledby="category-dialog-title" onSubmit={saveCategory}><h2 id="category-dialog-title">{editing.mode === 'edit' ? 'Edit Category' : 'Add Category'}</h2><p>{editing.mode === 'edit' ? 'Update this ticket category without affecting existing tickets.' : 'Create a category for organizing support requests.'}</p><div className="admin-category-form"><label><span>Category Name</span><input autoFocus value={form.name} maxLength="100" onChange={(event) => { setForm({ ...form, name: event.target.value }); setFieldErrors({ ...fieldErrors, name: '' }) }} aria-invalid={Boolean(fieldErrors.name)} />{fieldErrors.name && <small className="field-error">{fieldErrors.name}</small>}</label><label><span>Description</span><textarea value={form.description} maxLength="500" rows="4" onChange={(event) => { setForm({ ...form, description: event.target.value }); setFieldErrors({ ...fieldErrors, description: '' }) }} aria-invalid={Boolean(fieldErrors.description)} />{fieldErrors.description && <small className="field-error">{fieldErrors.description}</small>}</label></div>{fieldErrors.form && <p className="form-error" role="alert">{fieldErrors.form}</p>}<div className="dialog__actions"><button className="button button--secondary" type="button" disabled={saving} onClick={() => setEditing(null)}>Cancel</button><button className="button button--primary" type="submit" disabled={saving}>{saving ? (editing.mode === 'edit' ? 'Saving…' : 'Creating…') : (editing.mode === 'edit' ? 'Save Changes' : 'Create Category')}</button></div></form></div>}

      {statusTarget && <div className="dialog-backdrop" role="presentation"><div className="dialog" role="dialog" aria-modal="true" aria-labelledby="category-status-title"><h2 id="category-status-title">{statusTarget.isActive ? 'Deactivate category?' : 'Activate category?'}</h2><p>{statusTarget.isActive ? 'Inactive categories remain visible on existing tickets but cannot be selected for new tickets.' : `Restore ${statusTarget.name} as an available category for new tickets?`}</p><div className="dialog__actions"><button autoFocus className="button button--secondary" type="button" disabled={updatingStatus} onClick={() => setStatusTarget(null)}>Cancel</button><button className={statusTarget.isActive ? 'button button--danger' : 'button button--primary'} type="button" disabled={updatingStatus} onClick={changeStatus}>{updatingStatus ? 'Updating…' : statusTarget.isActive ? 'Deactivate' : 'Activate'}</button></div></div></div>}
    </>
  )
}

export default AdminCategoriesPage
