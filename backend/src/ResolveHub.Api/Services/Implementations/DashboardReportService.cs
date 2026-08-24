using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Reports;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Services.Implementations;

public sealed class DashboardReportService(ApplicationDbContext db) : IDashboardReportService
{
    private const string BrandBlue = "1769C2";
    private static readonly string[] StatusOrder =
        [TicketStatusNames.Open, TicketStatusNames.Assigned, TicketStatusNames.InProgress,
         TicketStatusNames.Pending, TicketStatusNames.Resolved, TicketStatusNames.Closed,
         TicketStatusNames.Cancelled, TicketStatusNames.Duplicate];
    private static readonly IReadOnlyDictionary<string, string> StatusColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TicketStatusNames.Open] = "#2563EB", [TicketStatusNames.Assigned] = "#7C3AED",
            [TicketStatusNames.InProgress] = "#D97706", [TicketStatusNames.Pending] = "#0891B2",
            [TicketStatusNames.Resolved] = "#15803D", [TicketStatusNames.Closed] = "#64748B",
            [TicketStatusNames.Cancelled] = "#DC2626", [TicketStatusNames.Duplicate] = "#5B21B6"
        };

    public async Task<DashboardReportFile> CreateAsync(
        DashboardReportRequest request, string generatedBy, string role,
        CancellationToken token)
    {
        var data = await BuildDataAsync(request, generatedBy, role, token);
        var bytes = CreatePdf(data);
        return new(bytes, $"ResolveHub_Dashboard_Report_{data.From:yyyy-MM-dd}_to_{data.To:yyyy-MM-dd}.pdf");
    }

    private async Task<DashboardReportData> BuildDataAsync(
        DashboardReportRequest request, string generatedBy, string role,
        CancellationToken token)
    {
        var from = request.From!.Value;
        var to = request.To!.Value;
        var timeZone = ResolveTimeZone(request.TimeZone);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), timeZone);
        var toExclusive = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), timeZone);
        var tickets = db.Tickets.AsNoTracking().Where(ticket => !ticket.IsDeleted);
        var periodTickets = tickets.Where(ticket =>
            ticket.CreatedDate >= fromUtc && ticket.CreatedDate < toExclusive);
        var periodCounts = await periodTickets.GroupBy(_ => 1).Select(group => new
        {
            Total = group.Count(),
            Open = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.Open),
            InProgress = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.InProgress),
            Unassigned = group.Count(ticket => ticket.AssignedToUserAccountID == null &&
                !ticket.TicketStatus.IsFinalStatus),
            Critical = group.Count(ticket => ticket.TicketPriority.Name == "Critical" &&
                !ticket.TicketStatus.IsFinalStatus)
        }).SingleOrDefaultAsync(token);
        var resolved = await tickets.CountAsync(ticket =>
            ticket.ResolvedDate >= fromUtc && ticket.ResolvedDate < toExclusive, token);

        var statusRows = await periodTickets.GroupBy(ticket => new
            { ticket.TicketStatus.Name, ticket.TicketStatus.SortOrder })
            .Select(group => new { group.Key.Name, Value = group.Count(), group.Key.SortOrder })
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ToListAsync(token);
        var categoryRows = await periodTickets.GroupBy(ticket => new
            { ticket.TicketCategory.Name, ticket.TicketCategory.SortOrder })
            .Select(group => new { group.Key.Name, Value = group.Count(), group.Key.SortOrder })
            .OrderByDescending(item => item.Value).ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Name).ToListAsync(token);
        var priorityRows = await periodTickets.GroupBy(ticket => new
            { ticket.TicketPriority.Name, ticket.TicketPriority.SortOrder })
            .Select(group => new { group.Key.Name, Value = group.Count(), group.Key.SortOrder })
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ToListAsync(token);
        var trendRows = await tickets.Where(ticket =>
                (ticket.CreatedDate >= fromUtc && ticket.CreatedDate < toExclusive) ||
                (ticket.ResolvedDate >= fromUtc && ticket.ResolvedDate < toExclusive))
            .Select(ticket => new { ticket.CreatedDate, ticket.ResolvedDate }).ToListAsync(token);
        var trend = BuildTrend(from, to, trendRows.Select(item =>
            (item.CreatedDate, item.ResolvedDate)).ToList(), timeZone);
        var workloads = await GetWorkloadsAsync(fromUtc, toExclusive, token);

        var metrics = new List<DashboardReportMetric>();
        if (role == RoleNames.Admin)
            metrics.Add(new("Users Added", await db.Users.CountAsync(user =>
                user.CreatedDate >= fromUtc && user.CreatedDate < toExclusive, token)));
        metrics.AddRange([
            new("Total Tickets", periodCounts?.Total ?? 0),
            new("Open Tickets", periodCounts?.Open ?? 0),
            new("In Progress", periodCounts?.InProgress ?? 0),
            new("Unassigned Tickets", periodCounts?.Unassigned ?? 0),
            new("Resolved in Period", resolved)]);
        if (role == RoleNames.Manager)
            metrics.Add(new("Critical Tickets", periodCounts?.Critical ?? 0));

        return new DashboardReportData(from, to, generatedBy, role,
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone), metrics,
            statusRows.Select(item => new DashboardReportChartItem(item.Name, item.Value)).ToList(),
            trend, categoryRows.Select(item => new DashboardReportChartItem(item.Name, item.Value)).ToList(),
            priorityRows.Select(item => new DashboardReportChartItem(item.Name, item.Value)).ToList(),
            workloads, periodCounts?.Total ?? 0, resolved);
    }

    private async Task<IReadOnlyCollection<DashboardReportWorkloadItem>> GetWorkloadsAsync(
        DateTime fromUtc, DateTime toExclusive, CancellationToken token)
    {
        var agents = await db.Users.AsNoTracking()
            .Where(user => user.IsActive && user.UserAccountRoles.Any(item =>
                item.Role.Name == RoleNames.ITSupportAgent))
            .OrderBy(user => user.FirstName).ThenBy(user => user.LastName)
            .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName })
            .ToListAsync(token);
        var assignments = await db.ActivityLogs.AsNoTracking()
            .Where(item => item.CreatedDate >= fromUtc && item.CreatedDate < toExclusive &&
                item.EntityType == "Ticket" &&
                (item.ActionType == TicketHistoryActionNames.TicketAssigned ||
                 item.ActionType == TicketHistoryActionNames.TicketReassigned) &&
                db.Tickets.Any(ticket => !ticket.IsDeleted &&
                    ticket.TicketReferenceNumber == item.EntityID))
            .Select(item => new { item.EntityID, item.NewValue })
            .ToListAsync(token);
        var worked = await db.TicketWorkSessions.AsNoTracking()
            .Where(item => !item.Ticket.IsDeleted && item.StartedAt < toExclusive &&
                (item.EndedAt == null || item.EndedAt >= fromUtc))
            .Select(item => new { item.ITAgentUserAccountID, item.TicketID })
            .Distinct().ToListAsync(token);
        var completed = await db.TicketHistory.AsNoTracking()
            .Where(item => !item.Ticket.IsDeleted &&
                item.CreatedDate >= fromUtc && item.CreatedDate < toExclusive &&
                (item.ActionType == TicketHistoryActionNames.TicketResolved ||
                 item.ActionType == TicketHistoryActionNames.TicketClosed))
            .Select(item => new
            {
                AgentId = item.PerformedByUserAccountID,
                item.TicketID,
                item.ActionType
            }).ToListAsync(token);

        return agents.Select(agent => new DashboardReportWorkloadItem(
            agent.Name,
            assignments.Where(item => item.NewValue == agent.Id.ToString())
                .Select(item => item.EntityID).Distinct().Count(),
            worked.Count(item => item.ITAgentUserAccountID == agent.Id),
            completed.Where(item => item.AgentId == agent.Id &&
                    item.ActionType == TicketHistoryActionNames.TicketResolved)
                .Select(item => item.TicketID).Distinct().Count(),
            completed.Where(item => item.AgentId == agent.Id &&
                    item.ActionType == TicketHistoryActionNames.TicketClosed)
                .Select(item => item.TicketID).Distinct().Count()))
            .ToList();
    }

    private static IReadOnlyCollection<DashboardReportTrendItem> BuildTrend(
        DateOnly from, DateOnly to,
        IReadOnlyCollection<(DateTime Created, DateTime? Resolved)> rows,
        TimeZoneInfo timeZone)
    {
        var localRows = rows.Select(item => (
            Created: TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(item.Created, DateTimeKind.Utc), timeZone),
            Resolved: item.Resolved.HasValue ? TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(item.Resolved.Value, DateTimeKind.Utc), timeZone) : (DateTime?)null))
            .ToList();
        var days = to.DayNumber - from.DayNumber + 1;
        var bucket = days <= 31 ? 1 : days <= 90 ? 7 : 30;
        var result = new List<DashboardReportTrendItem>();
        for (var start = from; start <= to; start = start.AddDays(bucket))
        {
            var end = start.AddDays(bucket - 1) > to ? to : start.AddDays(bucket - 1);
            var startUtc = start.ToDateTime(TimeOnly.MinValue);
            var endExclusive = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var label = start.ToString("MMM d", CultureInfo.InvariantCulture);
            result.Add(new(label,
                localRows.Count(item => item.Created >= startUtc && item.Created < endExclusive),
                localRows.Count(item => item.Resolved >= startUtc && item.Resolved < endExclusive)));
        }
        return result;
    }

    private static byte[] CreatePdf(DashboardReportData report)
    {
        var statuses = AggregateStatuses(report.Statuses);
        var sections = ReportSectionNames(report.Role);
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(34);
            page.DefaultTextStyle(style => style.FontSize(9).FontColor("334155"));
            page.Header().Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("ResolveHub").FontSize(20).Bold().FontColor(BrandBlue);
                    column.Item().Text("Management Dashboard Report").FontSize(14).SemiBold();
                });
                row.ConstantItem(180).AlignRight().Text($"{report.From:MMM d, yyyy} – {report.To:MMM d, yyyy}")
                    .FontColor(Colors.Grey.Darken1);
            });
            page.Content().PaddingVertical(16).Column(column =>
            {
                column.Spacing(14);
                column.Item().Background("F8FAFC").Border(1).BorderColor("E2E8F0").Padding(10)
                    .Text($"Reporting Period: {report.From:MMM d, yyyy} – {report.To:MMM d, yyyy}\n" +
                          $"Generated: {report.GeneratedAt:MMM d, yyyy, h:mm tt} {report.GeneratedAt:zzz}\n" +
                          $"Generated by: {report.GeneratedBy}  |  Role: {report.Role}");
                Section(column, "EXECUTIVE SUMMARY");
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn();
                    });
                    foreach (var metric in report.Metrics)
                        table.Cell().Padding(3).Background("F8FAFC").Border(1).BorderColor("E2E8F0")
                            .Padding(8).MinHeight(54).Column(card =>
                            {
                                card.Item().Text(metric.Name).FontSize(7).FontColor("64748B");
                                card.Item().Text(metric.Value.ToString()).FontSize(16).Bold().FontColor(BrandBlue);
                            });
                });
                column.Item().Text($"During the selected period, {report.CreatedDuringPeriod} tickets were created and {report.ResolvedDuringPeriod} were resolved.")
                    .FontColor("475569");
                column.Item().ShowEntire().Column(section =>
                {
                    Section(section, "TICKET STATUS OVERVIEW");
                    section.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem(1.15f).Height(190).Svg(DonutSvg(statuses));
                        row.RelativeItem().PaddingLeft(14).Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.ConstantColumn(14); columns.RelativeColumn(); columns.ConstantColumn(28); });
                            foreach (var status in statuses)
                            {
                                table.Cell().PaddingVertical(4).Text("●").FontColor(ColorForStatus(status.Name));
                                table.Cell().PaddingVertical(4).Text(status.Name);
                                table.Cell().PaddingVertical(4).AlignRight().Text(status.Value.ToString()).Bold();
                            }
                        });
                    });
                });
                if (sections.Contains("CREATED VS RESOLVED"))
                    ChartSection(column, "CREATED VS RESOLVED", 145, LineSvg(report.Trend));
                if (sections.Contains("TICKETS BY CATEGORY") && report.Categories.Count == 0)
                    ChartSection(column, "TICKETS BY CATEGORY", 110, CategorySvg(report.Categories));
                else if (sections.Contains("TICKETS BY CATEGORY"))
                {
                    var categoryPages = report.Categories.Chunk(18).ToList();
                    for (var index = 0; index < categoryPages.Count; index++)
                        ChartSection(column, index == 0 ? "TICKETS BY CATEGORY" :
                            "TICKETS BY CATEGORY (CONTINUED)",
                            CategoryPdfHeight(categoryPages[index]),
                            CategorySvg(categoryPages[index]));
                }
                if (sections.Contains("PRIORITY OVERVIEW"))
                    ChartSection(column, "PRIORITY OVERVIEW",
                        CategoryPdfHeight(report.Priorities), CategorySvg(report.Priorities));
                column.Item().EnsureSpace(120).Column(section =>
                {
                    Section(section, "IT AGENT WORKLOAD (REPORTING PERIOD)");
                    section.Item().PaddingTop(7).Text("Distinct tickets assigned, worked, resolved, or closed during the selected reporting period.")
                        .FontSize(8).FontColor("64748B");
                    section.Item().PaddingTop(7).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.2f); columns.RelativeColumn(); columns.RelativeColumn();
                            columns.RelativeColumn(); columns.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            foreach (var title in new[] { "Agent", "Assigned", "Worked", "Resolved", "Closed" })
                                header.Cell().Background(BrandBlue).Padding(5).Text(title).Bold().FontColor(Colors.White);
                        });
                        foreach (var agent in report.Workloads)
                        {
                            Cell(table, agent.Name); Cell(table, agent.Assigned.ToString());
                            Cell(table, agent.Worked.ToString()); Cell(table, agent.Resolved.ToString());
                            Cell(table, agent.Closed.ToString());
                        }
                        if (report.Workloads.Count == 0)
                            table.Cell().ColumnSpan(5).Padding(8).Text("No active IT Agents are available.");
                    });
                });
                column.Item().PaddingTop(8).AlignCenter().Text("END OF REPORT").FontSize(8).Bold().FontColor("64748B");
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor("64748B"));
                text.Span("ResolveHub  •  Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages();
            });
        })).GeneratePdf();
    }

    private static void ChartSection(
        ColumnDescriptor column, string title, float height, string svg) =>
        column.Item().ShowEntire().Column(section =>
        {
            Section(section, title);
            section.Item().PaddingTop(10).Height(height).Svg(svg);
        });

    private static void Section(ColumnDescriptor column, string title) =>
        column.Item().PaddingTop(4).BorderBottom(1).BorderColor("CBD5E1").PaddingBottom(4)
            .Text(title).FontSize(11).Bold().FontColor("0F172A");

    private static void Cell(TableDescriptor table, string value) =>
        table.Cell().BorderBottom(0.5f).BorderColor("E2E8F0").Padding(5).Text(value);

    private static IReadOnlyCollection<DashboardReportChartItem> AggregateStatuses(
        IReadOnlyCollection<DashboardReportChartItem> statuses)
    {
        var totals = statuses.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Value),
                StringComparer.OrdinalIgnoreCase);
        return StatusOrder.Select(name => new DashboardReportChartItem(name,
            totals.TryGetValue(name, out var value) ? value : 0)).ToList();
    }

    private static string ColorForStatus(string status) =>
        StatusColors.TryGetValue(status, out var color) ? color : "#64748B";

    private static string DonutSvg(IReadOnlyCollection<DashboardReportChartItem> values)
    {
        var representedTotal = values.Sum(item => item.Value);
        var total = Math.Max(1, representedTotal);
        const double circumference = 2 * Math.PI * 64;
        var offset = 0d;
        var rings = new StringBuilder();
        foreach (var item in values.Where(item => item.Value > 0))
        {
            var fraction = item.Value / (double)total;
            var length = fraction * circumference;
            var remainder = circumference - length;
            rings.Append(CultureInfo.InvariantCulture,
                $"<circle cx='100' cy='100' r='64' fill='none' stroke='{ColorForStatus(item.Name)}' stroke-width='30' stroke-dasharray='{length:0.###} {remainder:0.###}' stroke-dashoffset='{-offset:0.###}'/>");
            offset += length;
        }
        return $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'><circle cx='100' cy='100' r='64' fill='none' stroke='#E2E8F0' stroke-width='30'/><g transform='rotate(-90 100 100)'>{rings}</g><text x='100' y='97' text-anchor='middle' font-family='Arial' font-size='22' font-weight='700' fill='#0F172A'>{representedTotal}</text><text x='100' y='117' text-anchor='middle' font-family='Arial' font-size='11' fill='#64748B'>tickets</text></svg>";
    }

    private static string LineSvg(IReadOnlyCollection<DashboardReportTrendItem> source)
    {
        var data = source.ToList();
        if (data.Count == 0) return EmptySvg("No ticket activity in this period.");
        const double left = 48, top = 44, width = 642, height = 106;
        var max = Math.Max(1, data.Max(item => Math.Max(item.Created, item.Resolved)));
        var displayMaximum = max + 1;
        double X(int index) => left + index * width / Math.Max(1, data.Count - 1);
        double Y(int value) => top + height - value * height / displayMaximum;
        string Points(Func<DashboardReportTrendItem, int> selector) => string.Join(" ", data.Select((item, index) =>
            $"{X(index):0.##},{Y(selector(item)):0.##}"));
        var labelStep = Math.Max(1, (int)Math.Ceiling(data.Count / 8d));
        var labels = string.Join("", data.Select((item, index) => (item, index))
            .Where(value => value.index % labelStep == 0)
            .Select(value =>
            $"<text data-axis='date' x='{X(value.index):0.##}' y='181' text-anchor='middle' font-size='8.5' fill='#64748B'>{WebUtility.HtmlEncode(value.item.Label)}</text>"));
        var createdPoints = string.Join("", data.Select((item, index) => item.Created == 0 ? "" :
            $"<circle data-series='created-point' cx='{X(index):0.##}' cy='{Y(item.Created):0.##}' r='3.5' fill='#2563EB'/><text data-series='created-value' x='{X(index):0.##}' y='{Math.Max(36, Y(item.Created) - 8):0.##}' text-anchor='middle' font-size='8' font-weight='700' fill='#1E40AF' stroke='#FFFFFF' stroke-width='2.5' paint-order='stroke'>{item.Created}</text>"));
        var resolvedPoints = string.Join("", data.Select((item, index) => item.Resolved == 0 ? "" :
            $"<circle data-series='resolved-point' cx='{X(index):0.##}' cy='{Y(item.Resolved):0.##}' r='3.5' fill='#15803D'/><text data-series='resolved-value' x='{X(index):0.##}' y='{Math.Min(166, Y(item.Resolved) + 13):0.##}' text-anchor='middle' font-size='8' font-weight='700' fill='#166534' stroke='#FFFFFF' stroke-width='2.5' paint-order='stroke'>{item.Resolved}</text>"));
        return $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 740 210'><g data-region='legend'><circle cx='514' cy='17' r='4' fill='#2563EB'/><text x='524' y='20' font-size='9' fill='#334155'>Created Tickets</text><circle cx='625' cy='17' r='4' fill='#15803D'/><text x='635' y='20' font-size='9' fill='#334155'>Resolved Tickets</text></g><line x1='{left}' y1='{top + height}' x2='{left + width}' y2='{top + height}' stroke='#CBD5E1'/><line x1='{left}' y1='{top}' x2='{left}' y2='{top + height}' stroke='#CBD5E1'/><polyline points='{Points(item => item.Created)}' fill='none' stroke='#2563EB' stroke-width='3'/><polyline points='{Points(item => item.Resolved)}' fill='none' stroke='#15803D' stroke-width='3'/>{createdPoints}{resolvedPoints}{labels}</svg>";
    }

    private static string CategorySvg(IReadOnlyCollection<DashboardReportChartItem> source)
    {
        var data = source.ToList();
        if (data.Count == 0) return EmptySvg("No tickets were created in this period.");
        var height = data.Count * 28 + 20;
        var max = Math.Max(1, data.Max(item => item.Value));
        var rows = string.Join("", data.Select((item, index) =>
        {
            var y = index * 28 + 8;
            var bar = item.Value * 430d / max;
            return $"<text x='0' y='{y + 12}' font-size='10' fill='#334155'>{WebUtility.HtmlEncode(item.Name)}</text><rect x='190' y='{y}' width='{bar:0.##}' height='16' rx='3' fill='#2563EB'/><text x='{Math.Min(670, 198 + bar):0.##}' y='{y + 12}' font-size='10' font-weight='700' fill='#334155'>{item.Value}</text>";
        }));
        return $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 700 {height}'>{rows}</svg>";
    }

    private static string EmptySvg(string message) =>
        $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 700 150'><rect width='700' height='150' fill='#F8FAFC'/><text x='350' y='78' text-anchor='middle' font-family='Arial' font-size='12' fill='#64748B'>{WebUtility.HtmlEncode(message)}</text></svg>";

    private static float CategoryPdfHeight(
        IReadOnlyCollection<DashboardReportChartItem> categories) =>
        categories.Count == 0 ? 110 : (categories.Count * 28 + 20) * 0.74f;

    private static IReadOnlySet<string> ReportSectionNames(string role) =>
        role == RoleNames.Admin
            ? new HashSet<string>(["CREATED VS RESOLVED", "TICKETS BY CATEGORY"])
            : new HashSet<string>(["PRIORITY OVERVIEW"]);

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        return TimeZoneInfo.Utc;
    }
}
