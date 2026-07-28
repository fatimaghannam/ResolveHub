import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
const tooltipStyle = {
  border: '1px solid #dfe7f0',
  borderRadius: 8,
  boxShadow: '0 8px 24px rgba(20, 39, 68, .1)',
}

function ChartHeading({ title, description }) {
  return <div className="chart-heading"><h2>{title}</h2><p>{description}</p></div>
}

export function TicketStatusChart({ data, totalTickets }) {
  const ticketStatusChartData = data.map((item, index) => ({
    name: item.name, value: item.value,
    color: ['#1769c2', '#6f42a6', '#d17a00', '#087b8c', '#18794e', '#68778c'][index % 6],
  }))
  const summary = ticketStatusChartData
    .map((item) => `${item.name}: ${item.value}`)
    .join(', ')

  return (
    <section className="panel chart-panel">
      <ChartHeading title="Ticket Status Overview" description="Current distribution of tickets by workflow status." />
      <div className="chart-box chart-box--pie" role="img" aria-label={`Ticket status overview. ${summary}. Total: ${totalTickets}.`}>
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie data={ticketStatusChartData} dataKey="value" nameKey="name" cx="50%" cy="45%" innerRadius={58} outerRadius={86} paddingAngle={2} isAnimationActive={false}>
              {ticketStatusChartData.map((item) => <Cell fill={item.color} key={item.name} />)}
            </Pie>
            <Tooltip formatter={(value, name) => [`${value} tickets`, name]} contentStyle={tooltipStyle} />
            <Legend verticalAlign="bottom" iconType="circle" />
          </PieChart>
        </ResponsiveContainer>
        <div className="pie-total" aria-hidden="true"><strong>{totalTickets}</strong><span>Total Tickets</span></div>
      </div>
    </section>
  )
}

export function TicketTrendChart({ data: monthlyTicketTrend }) {
  const summary = monthlyTicketTrend
    .map((item) => `${item.month}: ${item.created} created and ${item.resolved} resolved`)
    .join(', ')

  return (
    <section className="panel chart-panel">
      <ChartHeading title="Created vs Resolved" description="Monthly comparison of incoming tickets and completed work." />
      <div className="chart-box" role="img" aria-label={`Created versus resolved tickets for the most recent six months. ${summary}.`}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={monthlyTicketTrend} margin={{ top: 8, right: 12, left: -18, bottom: 0 }}>
            <CartesianGrid stroke="#e7edf4" strokeDasharray="3 3" vertical={false} />
            <XAxis dataKey="month" tick={{ fill: '#68778c', fontSize: 12 }} />
            <YAxis allowDecimals={false} tick={{ fill: '#68778c', fontSize: 12 }} />
            <Tooltip formatter={(value, name) => [`${value} tickets`, name]} contentStyle={tooltipStyle} />
            <Legend verticalAlign="bottom" />
            <Line name="Created Tickets" type="monotone" dataKey="created" stroke="#1769c2" strokeWidth={2.5} dot={{ r: 3 }} activeDot={{ r: 5 }} isAnimationActive={false} />
            <Line name="Resolved Tickets" type="monotone" dataKey="resolved" stroke="#18794e" strokeWidth={2.5} dot={{ r: 3 }} activeDot={{ r: 5 }} isAnimationActive={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </section>
  )
}

export function TicketCategoryChart({ data }) {
  const ticketsByCategory = data.map((item) => ({ category: item.name, tickets: item.value }))
  const summary = ticketsByCategory
    .map((item) => `${item.category}: ${item.tickets}`)
    .join(', ')

  return (
    <section className="panel chart-panel">
      <ChartHeading title="Tickets by Category" description="Ticket volume grouped by support category." />
      <div className="chart-box chart-box--category" role="img" aria-label={`Tickets by category, highest to lowest. ${summary}.`}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={ticketsByCategory} layout="vertical" margin={{ top: 0, right: 18, left: 18, bottom: 0 }}>
            <CartesianGrid stroke="#e7edf4" strokeDasharray="3 3" horizontal={false} />
            <XAxis type="number" allowDecimals={false} tick={{ fill: '#68778c', fontSize: 12 }} />
            <YAxis type="category" dataKey="category" width={96} tick={{ fill: '#334359', fontSize: 12 }} />
            <Tooltip formatter={(value) => [`${value} tickets`, 'Tickets']} contentStyle={tooltipStyle} />
            <Bar dataKey="tickets" name="Tickets" fill="#1769c2" radius={[0, 5, 5, 0]} isAnimationActive={false} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </section>
  )
}
