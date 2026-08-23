import { useEffect, useMemo, useState } from 'react'
import { FileDown } from 'lucide-react'
import { downloadDashboardReport } from '../../services/dashboardReportService.js'

const dateValue = (date) => new Date(date.getTime() - date.getTimezoneOffset() * 60000)
  .toISOString().slice(0, 10)

function rangeFor(preset) {
  const today = new Date()
  const end = dateValue(today)
  if (preset === 'month') return { from: dateValue(new Date(today.getFullYear(), today.getMonth(), 1)), to: end }
  const start = new Date(today)
  start.setDate(start.getDate() - (preset === '90' ? 89 : 29))
  return { from: dateValue(start), to: end }
}

export function DashboardReportButton() {
  const [open, setOpen] = useState(false)
  return <>
    <button className="button button--secondary" type="button" onClick={() => setOpen(true)}><FileDown size={17} aria-hidden="true" />Generate Report</button>
    {open && <DashboardReportDialog onClose={() => setOpen(false)} />}
  </>
}

function DashboardReportDialog({ onClose }) {
  const [preset, setPreset] = useState('month')
  const [custom, setCustom] = useState(rangeFor('month'))
  const [generating, setGenerating] = useState(false)
  const [error, setError] = useState('')
  const selected = useMemo(() => preset === 'custom' ? custom : rangeFor(preset), [preset, custom])
  const invalid = !selected.from || !selected.to || selected.from > selected.to

  useEffect(() => {
    function close(event) { if (event.key === 'Escape' && !generating) onClose() }
    window.addEventListener('keydown', close)
    return () => window.removeEventListener('keydown', close)
  }, [generating, onClose])

  async function generate(event) {
    event.preventDefault()
    if (invalid || generating) return
    setGenerating(true); setError('')
    try {
      const file = await downloadDashboardReport(selected.from, selected.to)
      const url = URL.createObjectURL(file.blob)
      const link = document.createElement('a')
      link.href = url
      link.download = file.fileName || `ResolveHub_Dashboard_Report_${selected.from}_to_${selected.to}.pdf`
      link.click(); URL.revokeObjectURL(url); onClose()
    } catch {
      setError('The dashboard report could not be generated. Please try again.')
    } finally { setGenerating(false) }
  }

  return <div className="dialog-backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget && !generating) onClose() }}>
    <form className="dialog dashboard-report-dialog" role="dialog" aria-modal="true" aria-labelledby="dashboard-report-title" onSubmit={generate}>
      <h2 id="dashboard-report-title">Generate Dashboard Report</h2><p>Create a professional PDF using live management dashboard data.</p>
      <div className="dashboard-report-fields">
        <label><span>Date Range</span><select value={preset} onChange={(event) => { setPreset(event.target.value); setError('') }} disabled={generating}><option value="month">This Month</option><option value="30">Last 30 Days</option><option value="90">Last 90 Days</option><option value="custom">Custom Range</option></select></label>
        {preset === 'custom' && <div className="form-grid"><label><span>Start Date</span><input type="date" value={custom.from} max={custom.to || undefined} onChange={(event) => setCustom({ ...custom, from: event.target.value })} required disabled={generating} /></label><label><span>End Date</span><input type="date" value={custom.to} min={custom.from || undefined} onChange={(event) => setCustom({ ...custom, to: event.target.value })} required disabled={generating} /></label></div>}
        <label><span>Format</span><select value="pdf" disabled><option value="pdf">PDF</option></select></label>
      </div>
      {invalid && <p className="form-error" role="alert">The start date cannot be after the end date.</p>}{error && <p className="form-error" role="alert">{error}</p>}
      <div className="dialog__actions"><button className="button button--secondary" type="button" onClick={onClose} disabled={generating}>Cancel</button><button className="button button--primary" type="submit" disabled={invalid || generating}>{generating ? 'Generating report…' : 'Generate PDF'}</button></div>
    </form>
  </div>
}
