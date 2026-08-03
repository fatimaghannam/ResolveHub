import {
  Activity, AlertCircle, Archive, ArrowRight, CheckCircle2, ChevronDown,
  CirclePause, CirclePlay, Clock3, FileText, MessageSquareText, Paperclip,
  RefreshCcw, Tag, UserRoundCheck,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { getTicketActivity, getTicketActivitySummary } from '../../services/ticketActivityService.js'
import { TICKET_ACTIVITY_CHANGED_EVENT } from '../../services/ticketActivityEvents.js'
import { activityDateGroup, formatActivityDate } from '../../utils/activityDate.js'

const filterDefinitions = [
  ['All', () => true],
  ['Status', (item) => /status|resolved|closed|reopened|cancelled|duplicate/i.test(item.activityType)],
  ['Assignment', (item) => /assign/i.test(item.activityType)],
  ['Work Time', (item) => /work/i.test(item.activityType)],
  ['Comments', (item) => /comment|reply/i.test(item.activityType)],
  ['Attachments', (item) => /attachment/i.test(item.activityType)],
]

const iconRules = [
  [/created/i, FileText, 'created'], [/reassign|assign/i, UserRoundCheck, 'assignment'],
  [/paused/i, CirclePause, 'paused'], [/resumed/i, RefreshCcw, 'work'],
  [/work started/i, CirclePlay, 'work'], [/resolved/i, CheckCircle2, 'resolved'],
  [/closed/i, Archive, 'closed'], [/duplicate|cancelled/i, AlertCircle, 'danger'],
  [/comment|reply/i, MessageSquareText, 'comment'], [/attachment/i, Paperclip, 'attachment'],
  [/reopened|status/i, RefreshCcw, 'status'], [/priority|category/i, Tag, 'status'],
]

function activityVisual(type) {
  const match = iconRules.find(([pattern]) => pattern.test(type))
  return match ? { Icon: match[1], tone: match[2] } : { Icon: Activity, tone: 'default' }
}

const dateParts = formatActivityDate

function duplicateFingerprint(item) {
  return [item.activityType, item.performerId, item.oldValue, item.newValue,
    item.description, item.isInternal].map((value) => value ?? '').join('\u001f')
}

function prepareActivities(items) {
  const sorted = [...items].sort((left, right) =>
    new Date(right.occurredAt).getTime() - new Date(left.occurredAt).getTime() || right.id - left.id)
  const latestByFingerprint = new Map()
  return sorted.filter((item) => {
    const timestamp = new Date(item.occurredAt).getTime()
    const fingerprint = duplicateFingerprint(item)
    const previous = latestByFingerprint.get(fingerprint)
    latestByFingerprint.set(fingerprint, timestamp)
    return previous == null || !Number.isFinite(timestamp) || Math.abs(previous - timestamp) > 5000
  })
}

function workState(summary) {
  if (summary?.isWorkSessionActive) return { label: 'Working', tone: 'active' }
  if (summary?.currentStatus === 'Pending') return { label: 'Paused', tone: 'paused' }
  if (summary?.currentStatus === 'Resolved') return { label: 'Completed', tone: 'completed' }
  if (summary?.currentStatus === 'Closed') return { label: 'Closed', tone: 'closed' }
  if (summary?.firstWorkStartedAt) return { label: 'Paused', tone: 'paused' }
  return { label: 'Not started', tone: 'idle' }
}

function formatMinutes(minutes) {
  if (!minutes) return '0m'
  return minutes < 60 ? `${minutes}m` : `${Math.floor(minutes / 60)}h ${minutes % 60}m`
}

function formatLiveDuration(milliseconds) {
  const seconds = Math.max(0, Math.floor(milliseconds / 1000))
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  const remainder = seconds % 60
  return hours ? `${hours}h ${minutes}m` : `${minutes}m ${String(remainder).padStart(2, '0')}s`
}

function workActionLabel(type, active) {
  if (active) return 'Working'
  if (/resolved/i.test(type)) return 'Resolved'
  if (/closed/i.test(type)) return 'Closed'
  if (/paused/i.test(type)) return 'Paused'
  if (/resumed/i.test(type)) return 'Resumed'
  return 'Started'
}

function ActivityCard({ item, workContext }) {
  const { Icon, tone } = activityVisual(item.activityType)
  const occurred = dateParts(item.occurredAt)
  const isAssignment = /ticket assigned|ticket reassigned/i.test(item.activityType)
  const isClosed = /ticket closed/i.test(item.activityType)
  const isAttachment = /attachment/i.test(item.activityType)
  const hasChange = !isAssignment && !isAttachment && (item.oldValue || item.newValue)
  const changeLabel = /status|work|resolved|closed|reopened/i.test(item.activityType) ? 'Status'
    : /priority/i.test(item.activityType) ? 'Priority'
      : /category/i.test(item.activityType) ? 'Category'
        : /assign/i.test(item.activityType) ? 'Assignment' : 'Change'
  return <article className={`activity-event activity-event--${tone}`}>
    <span className="activity-event__icon"><Icon size={18} aria-hidden="true" /></span>
    <div className="activity-event__content">
      <div className="activity-event__heading">
        <h4>{item.activityType}</h4>
        {item.workDurationMinutes != null && <span className="activity-event__duration"><Clock3 size={13} />{item.workDurationMinutes}m recorded</span>}
      </div>
      {item.description && <p className="activity-event__description">{item.description}</p>}
      {isAssignment && <p className="activity-event__assignment"><span>Assigned to</span><strong>{item.newValue || 'IT Support Agent'}</strong><small>IT Support Agent</small></p>}
      {isAttachment && <p className="activity-event__assignment"><span>File</span><strong>{item.newValue || 'Attachment'}</strong><small>Uploaded attachment</small></p>}
      {hasChange && <p className="activity-event__change"><small>{changeLabel}</small><span>{item.oldValue || '—'}</span><ArrowRight size={14} aria-hidden="true" /><strong>{item.newValue || '—'}</strong></p>}
      {workContext && !isClosed && <p className="activity-event__session"><span>{workContext.label}</span>{item.workDurationMinutes != null && <strong>{formatMinutes(item.workDurationMinutes)}</strong>}</p>}
      <p className="activity-event__meta"><strong>{item.performerFullName || '—'}</strong><span>{item.performerRole || '—'}</span><span>{occurred.metadata}</span></p>
    </div>
  </article>
}

function AssignmentRequestGroup({ group }) {
  const approved = group.items.filter((item) => /approved/i.test(item.activityType)).length
  const rejected = group.items.filter((item) => /rejected/i.test(item.activityType)).length
  const latest = group.items[0]
  const latestDate = dateParts(latest.occurredAt)
  return <details className="assignment-activity-group">
    <summary><span className="activity-event__icon"><UserRoundCheck size={18} aria-hidden="true" /></span><span><strong>Assignment requests · {group.items.length} events</strong><small>{approved} approved · {rejected} rejected · Latest {latestDate.time}</small></span><ChevronDown size={16} aria-hidden="true" /></summary>
    <div className="assignment-activity-group__items">{group.items.map((item) => <ActivityCard item={item} key={item.id} />)}</div>
  </details>
}

function groupAssignmentRequests(items) {
  const result = []
  let pending = []
  const flush = () => {
    if (pending.length >= 3) result.push({ type: 'assignment-group', id: `assignment-${pending[0].id}`, occurredAt: pending[0].occurredAt, items: pending })
    else result.push(...pending.map((item) => ({ type: 'activity', id: item.id, occurredAt: item.occurredAt, item })))
    pending = []
  }
  items.forEach((item) => {
    if (/assignment request/i.test(item.activityType)) pending.push(item)
    else { flush(); result.push({ type: 'activity', id: item.id, occurredAt: item.occurredAt, item }) }
  })
  flush()
  return result
}

export default function TicketActivityLog({ ticketReference }) {
  const [open, setOpen] = useState(true)
  const [filter, setFilter] = useState('All')
  const [summary, setSummary] = useState(null)
  const [activities, setActivities] = useState([])
  const [summaryError, setSummaryError] = useState('')
  const [timelineError, setTimelineError] = useState('')
  const [summaryLoading, setSummaryLoading] = useState(true)
  const [timelineLoading, setTimelineLoading] = useState(true)
  const [summaryLoadedAt, setSummaryLoadedAt] = useState(Date.now())
  const [clock, setClock] = useState(Date.now())
  const [refreshing, setRefreshing] = useState(false)
  const refreshController = useRef(null)

  const refreshActivity = useCallback(async ({ initial = false } = {}) => {
    refreshController.current?.abort()
    const controller = new AbortController()
    refreshController.current = controller
    if (initial) {
      setSummaryLoading(true); setTimelineLoading(true)
    } else setRefreshing(true)
    setSummaryError(''); setTimelineError('')
    const [summaryResult, timelineResult] = await Promise.allSettled([
      getTicketActivitySummary(ticketReference, controller.signal),
      getTicketActivity(ticketReference, controller.signal),
    ])
    if (controller.signal.aborted) return
    if (summaryResult.status === 'fulfilled') {
      setSummary(summaryResult.value)
      setSummaryLoadedAt(Date.now())
      setClock(Date.now())
    } else if (summaryResult.reason?.name !== 'AbortError') setSummaryError(summaryResult.reason.message)
    if (timelineResult.status === 'fulfilled') setActivities(prepareActivities(timelineResult.value))
    else if (timelineResult.reason?.name !== 'AbortError') setTimelineError(timelineResult.reason.message)
    setSummaryLoading(false); setTimelineLoading(false); setRefreshing(false)
  }, [ticketReference])

  useEffect(() => {
    setSummary(null); setActivities([])
    refreshActivity({ initial: true })
    const synchronize = (event) => {
      if (event.detail?.ticketReference?.toLowerCase() === ticketReference.toLowerCase()) {
        refreshActivity()
      }
    }
    window.addEventListener(TICKET_ACTIVITY_CHANGED_EVENT, synchronize)
    return () => {
      window.removeEventListener(TICKET_ACTIVITY_CHANGED_EVENT, synchronize)
      refreshController.current?.abort()
    }
  }, [refreshActivity, ticketReference])

  useEffect(() => {
    if (!summary?.isWorkSessionActive) return undefined
    const timer = window.setInterval(() => setClock(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [summary?.isWorkSessionActive])

  const counts = useMemo(() => Object.fromEntries(filterDefinitions.map(([name, matcher]) =>
    [name, groupAssignmentRequests(activities.filter(matcher)).length])), [activities])
  const visible = useMemo(() => activities.filter(filterDefinitions.find(([name]) => name === filter)[1]), [activities, filter])
  const presentationItems = useMemo(() => groupAssignmentRequests(visible), [visible])
  const groups = useMemo(() => presentationItems.reduce((result, item) => {
    const label = activityDateGroup(item.occurredAt)
    if (!result[label]) result[label] = []
    result[label].push(item)
    return result
  }, {}), [presentationItems])
  const firstWork = dateParts(summary?.firstWorkStartedAt)
  const state = workState(summary)
  const workActivities = useMemo(() => activities.filter((item) => /work started|work resumed|work paused|resolved|closed/i.test(item.activityType)), [activities])
  const sessionStarts = useMemo(() => activities.filter((item) => /work started|work resumed/i.test(item.activityType)), [activities])
  const sessionNumbers = useMemo(() => {
    let number = 0
    return Object.fromEntries([...activities].reverse().map((item) => {
      if (/work started|work resumed/i.test(item.activityType)) number += 1
      return [item.id, /work started|work resumed/i.test(item.activityType)
        ? { label: `Session #${number}` }
        : /work paused|ticket resolved/i.test(item.activityType) && item.workDurationMinutes != null
          ? { label: `Session #${Math.max(number, 1)} duration` } : null]
    }))
  }, [activities])
  const sessionCount = sessionStarts.length
  const liveAddedMinutes = summary?.isWorkSessionActive ? Math.max(0, Math.floor((clock - summaryLoadedAt) / 60000)) : 0
  const liveTotalMinutes = (summary?.totalWorkMinutes || 0) + liveAddedMinutes
  const liveTotalTime = formatMinutes(liveTotalMinutes)
  const currentSessionMinutes = summary?.currentSessionStartedAt
    ? Math.max(0, Math.floor((clock - new Date(summary.currentSessionStartedAt).getTime()) / 60000)) : 0
  const completedSessionDurations = workActivities.filter((item) => item.workDurationMinutes != null).map((item) => item.workDurationMinutes)
  const averageSession = completedSessionDurations.length
    ? formatMinutes(Math.round(completedSessionDurations.reduce((total, minutes) => total + minutes, 0) / completedSessionDurations.length)) : '—'
  const lastWorkActivity = workActivities[0]
  const lastWorkDate = dateParts(lastWorkActivity?.occurredAt)
  const lastWorkLabel = summary?.isWorkSessionActive ? 'Working' : lastWorkActivity
    ? workActionLabel(lastWorkActivity.activityType, false) : '—'
  const currentSessionLive = summary?.currentSessionStartedAt
    ? formatLiveDuration(clock - new Date(summary.currentSessionStartedAt).getTime()) : '0m 00s'
  const emptyMessages = {
    All: 'No activity has been recorded yet.', Status: 'No status activity yet.',
    Assignment: 'No assignment activity yet.', 'Work Time': 'No work sessions have been recorded.',
    Comments: 'No comment activity yet.', Attachments: 'No attachment activity has been recorded.',
  }

  return <section className="panel activity-log">
    <button className="activity-log__toggle" type="button" onClick={() => setOpen((value) => !value)} aria-expanded={open} aria-controls="ticket-activity-content" aria-label={`${open ? 'Collapse' : 'Expand'} ticket activity log`}>
      <span className="activity-log__title"><span className="activity-log__title-icon"><Activity size={19} /></span><span><strong>Activity Log</strong><small>Complete audit trail of this ticket.</small></span></span>
      <span className="activity-log__header-metrics"><span><strong>{timelineLoading ? '—' : activities.length}</strong> Activities</span><i /><span><strong>{summary ? liveTotalTime : '—'}</strong> worked</span><em className={`live-work-indicator live-work-indicator--${state.tone}`}><i aria-hidden="true" />{state.label}</em>{refreshing && <span className="activity-log__sync" role="status">Syncing…</span>}</span>
      <ChevronDown className="activity-log__chevron" size={20} aria-hidden="true" />
    </button>
    {open && <div className="activity-log__body" id="ticket-activity-content">
      {summaryError && <p className="activity-log__error">Summary: {summaryError}</p>}
      {summaryLoading && <div className="activity-summary-skeleton" aria-label="Loading activity summary" />}
      {summary && <dl className="activity-summary">
        <div className="activity-summary__work"><dt>Total work time</dt><dd>{liveTotalTime}</dd><small><i className={`work-dot work-dot--${state.tone}`} />{summary.isWorkSessionActive ? `Current session · ${currentSessionLive}` : state.label}</small></div>
        <div><dt>Current work state</dt><dd><span className={`live-work-indicator live-work-indicator--${state.tone}`}><i aria-hidden="true" />{state.label}</span></dd><small>{summary.isWorkSessionActive ? `Active session · ${currentSessionLive}` : summary.firstWorkStartedAt ? 'Based on latest work action' : 'No session recorded'}</small></div>
        <div><dt>Sessions</dt><dd>{sessionCount || '—'}</dd><small>{sessionCount ? `${sessionCount} ${sessionCount === 1 ? 'Session' : 'Sessions'}` : 'No sessions recorded'}</small></div>
        <div><dt>First work started</dt><dd>{summary.firstWorkStartedAt ? firstWork.date : '—'}</dd><small>{summary.firstWorkStartedAt ? firstWork.time : '—'}</small></div>
        <div><dt>Last work activity</dt><dd>{lastWorkLabel}</dd><small>{summary.isWorkSessionActive ? `Current session · ${formatMinutes(currentSessionMinutes)}` : lastWorkActivity ? `${lastWorkDate.date} · ${lastWorkDate.time}` : '—'}</small></div>
        <div><dt>Average session</dt><dd>{averageSession}</dd><small>{completedSessionDurations.length ? `Across ${completedSessionDurations.length} completed ${completedSessionDurations.length === 1 ? 'session' : 'sessions'}` : '—'}</small></div>
      </dl>}
      <nav className="activity-filters" aria-label="Filter activity">
        {filterDefinitions.map(([name]) => <button className={filter === name ? 'is-active' : ''} type="button" key={name} aria-pressed={filter === name} onClick={() => setFilter(name)}>{name}<span>{counts[name]}</span></button>)}
      </nav>
      {timelineError && <p className="activity-log__error">Timeline: {timelineError}</p>}
      {timelineLoading && <div className="activity-timeline-loading"><i /><i /><i /></div>}
      {!timelineLoading && !timelineError && <div className="activity-timeline">
        {Object.entries(groups).map(([label, items]) => <section className="activity-day" key={label}><h3><span>{label}</span></h3><div className="activity-day__events">{items.map((entry) => entry.type === 'assignment-group'
          ? <AssignmentRequestGroup group={entry} key={entry.id} />
          : <ActivityCard item={entry.item} workContext={sessionNumbers[entry.item.id]} key={entry.id} />)}</div></section>)}
        {!visible.length && <div className="activity-timeline__empty"><Activity size={22} /><strong>{emptyMessages[filter]}</strong><span>New events will appear here automatically.</span></div>}
      </div>}
    </div>}
  </section>
}
