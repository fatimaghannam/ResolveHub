using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Reports;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class DashboardReportTests
{
    private const string Password = "ValidPassword1!";

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Manager)]
    public async Task ManagementRole_CanGenerateValidDashboardPdf(string role)
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var user = await factory.CreateUserAsync(
            $"report-{role.Replace(" ", "-").ToLowerInvariant()}@resolvehub.test",
            Password, role);
        using var client = await LoginAsync(factory, user.Email!);

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IDashboardReportService>()
                .CreateAsync(new DashboardReportRequest
                {
                    From = new DateOnly(2026, 8, 1), To = new DateOnly(2026, 8, 31)
                }, user.Email!, role, default);

        var response = await client.GetAsync(
            "/api/reports/dashboard?from=2026-08-01&to=2026-08-31");
        var content = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(content, 0, 4));
        Assert.Contains("ResolveHub_Dashboard_Report_2026-08-01_to_2026-08-31.pdf",
            response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName);
    }

    [Theory]
    [InlineData(RoleNames.Employee)]
    [InlineData(RoleNames.ITSupportAgent)]
    public async Task NonManagementRole_CannotGenerateDashboardReport(string role)
    {
        await using var factory = new ResolveHubApiFactory();
        var user = await factory.CreateUserAsync(
            $"report-denied-{role.Replace(" ", "-").ToLowerInvariant()}@resolvehub.test",
            Password, role);
        using var client = await LoginAsync(factory, user.Email!);

        var response = await client.GetAsync(
            "/api/reports/dashboard?from=2026-08-01&to=2026-08-31");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReportData_UsesInclusivePeriodBoundariesAndHistoricalAgentActivity()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var admin = await factory.CreateUserAsync(
            "period-report-admin@resolvehub.test", Password, RoleNames.Admin);
        var employee = await factory.CreateUserAsync(
            "period-report-requester@resolvehub.test", Password, RoleNames.Employee);
        var activeAgent = await factory.CreateUserAsync(
            "period-report-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var idleAgent = await factory.CreateUserAsync(
            "period-report-idle-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var before = await CreateTicketAsync(factory, employeeClient, "Before period");
        var start = await CreateTicketAsync(factory, employeeClient, "Start boundary");
        var end = await CreateTicketAsync(factory, employeeClient, "End boundary");
        var after = await CreateTicketAsync(factory, employeeClient, "After period");
        var fromUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var endLateUtc = new DateTime(2026, 8, 24, 23, 59, 59, DateTimeKind.Utc);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tickets = await context.Tickets.Where(item =>
                item.ID == before.Id || item.ID == start.Id || item.ID == end.Id ||
                item.ID == after.Id).ToDictionaryAsync(item => item.ID);
            tickets[before.Id].CreatedDate = fromUtc.AddTicks(-1);
            tickets[start.Id].CreatedDate = fromUtc;
            tickets[end.Id].CreatedDate = endLateUtc;
            tickets[after.Id].CreatedDate = endLateUtc.AddSeconds(1);
            tickets[before.Id].ResolvedDate = endLateUtc;
            context.ActivityLogs.AddRange(
                new ActivityLog
                {
                    PerformedByUserAccountID = admin.Id,
                    ActionType = TicketHistoryActionNames.TicketAssigned,
                    EntityType = "Ticket",
                    EntityID = start.TicketReferenceNumber,
                    Description = "Assigned during period.",
                    NewValue = activeAgent.Id.ToString(),
                    CreatedDate = endLateUtc
                },
                new ActivityLog
                {
                    PerformedByUserAccountID = admin.Id,
                    ActionType = TicketHistoryActionNames.TicketAssigned,
                    EntityType = "Ticket",
                    EntityID = before.TicketReferenceNumber,
                    Description = "Assigned before period.",
                    NewValue = activeAgent.Id.ToString(),
                    CreatedDate = fromUtc.AddTicks(-1)
                });
            context.TicketWorkSessions.Add(new TicketWorkSession
            {
                TicketID = start.Id,
                ITAgentUserAccountID = activeAgent.Id,
                StartedAt = fromUtc.AddHours(-1),
                EndedAt = fromUtc.AddHours(1),
                DurationMinutes = 120,
                CreatedDate = fromUtc.AddHours(-1)
            });
            context.TicketHistory.AddRange(
                new TicketHistory
                {
                    TicketID = before.Id,
                    PerformedByUserAccountID = activeAgent.Id,
                    ActionType = TicketHistoryActionNames.TicketResolved,
                    CreatedDate = endLateUtc
                },
                new TicketHistory
                {
                    TicketID = before.Id,
                    PerformedByUserAccountID = activeAgent.Id,
                    ActionType = TicketHistoryActionNames.TicketClosed,
                    CreatedDate = endLateUtc
                });
            await context.SaveChangesAsync();
        }

        foreach (var role in new[] { RoleNames.Admin, RoleNames.Manager })
        {
            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDashboardReportService>();
            var method = service.GetType().GetMethod(
                "BuildDataAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var task = (Task<DashboardReportData>)method.Invoke(service,
                [new DashboardReportRequest
                {
                    From = new DateOnly(2026, 8, 1),
                    To = new DateOnly(2026, 8, 24),
                    TimeZone = "UTC"
                }, admin.Email!, role, CancellationToken.None])!;
            var report = await task;
            var workload = Assert.Single(report.Workloads,
                item => item.Assigned == 1 && item.Worked == 1 &&
                    item.Resolved == 1 && item.Closed == 1);
            var idle = Assert.Single(report.Workloads,
                item => item.Assigned == 0 && item.Worked == 0 &&
                    item.Resolved == 0 && item.Closed == 0);

            Assert.Equal(2, report.Metrics.Single(item => item.Name == "Total Tickets").Value);
            Assert.Equal(1, report.Metrics.Single(item => item.Name == "Resolved in Period").Value);
            Assert.Equal(2, report.CreatedDuringPeriod);
            Assert.Equal(1, report.ResolvedDuringPeriod);
            Assert.Equal(2, report.Statuses.Sum(item => item.Value));
            Assert.Equal(2, report.Categories.Sum(item => item.Value));
            Assert.Equal(2, report.Priorities.Sum(item => item.Value));
            Assert.Equal(2, report.Trend.Sum(item => item.Created));
            Assert.Equal(1, report.Trend.Sum(item => item.Resolved));
            Assert.Equal(1, workload.Assigned);
            Assert.Equal(1, workload.Worked);
            Assert.Equal(1, workload.Resolved);
            Assert.Equal(1, workload.Closed);
            Assert.Equal(0, idle.Assigned);
            Assert.Equal(0, idle.Worked);
            Assert.Equal(0, idle.Resolved);
            Assert.Equal(0, idle.Closed);

            var oneDayTask = (Task<DashboardReportData>)method.Invoke(service,
                [new DashboardReportRequest
                {
                    From = new DateOnly(2026, 8, 24),
                    To = new DateOnly(2026, 8, 24),
                    TimeZone = "UTC"
                }, admin.Email!, role, CancellationToken.None])!;
            var oneDayReport = await oneDayTask;
            Assert.Equal(1, oneDayReport.Metrics
                .Single(item => item.Name == "Total Tickets").Value);
            Assert.Equal(1, oneDayReport.Metrics
                .Single(item => item.Name == "Resolved in Period").Value);
            Assert.Equal(1, oneDayReport.Workloads.Sum(item => item.Assigned));
        }
    }

    [Theory]
    [InlineData("2026-08-31", "2026-08-01")]
    [InlineData("2025-01-01", "2026-08-01")]
    public async Task InvalidDateRange_IsRejected(string from, string to)
    {
        await using var factory = new ResolveHubApiFactory();
        var admin = await factory.CreateUserAsync(
            $"report-invalid-{Guid.NewGuid():N}@resolvehub.test", Password, RoleNames.Admin);
        using var client = await LoginAsync(factory, admin.Email!);

        var response = await client.GetAsync(
            $"/api/reports/dashboard?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Donut_UsesOneAggregatedSlicePerRepresentedStatus()
    {
        DashboardReportChartItem[] values =
        [
            new(TicketStatusNames.Open, 1), new(TicketStatusNames.Open, 1),
            new(TicketStatusNames.Assigned, 4), new(TicketStatusNames.InProgress, 1),
            new(TicketStatusNames.Pending, 2), new(TicketStatusNames.Resolved, 2),
            new(TicketStatusNames.Closed, 3), new(TicketStatusNames.Cancelled, 1),
            new(TicketStatusNames.Duplicate, 1)
        ];

        var aggregated = InvokePrivate<IReadOnlyCollection<DashboardReportChartItem>>(
            "AggregateStatuses", (object)values);
        var svg = InvokePrivate<string>("DonutSvg", (object)aggregated);
        var slices = svg.Split("stroke-width='30'").Length - 2;

        Assert.Equal(8, aggregated.Count);
        Assert.Equal(16, aggregated.Sum(item => item.Value));
        Assert.Equal(8, slices);
        Assert.Contains(">16</text>", svg);
        Assert.DoesNotContain("pathLength", svg);
        foreach (var color in new[] { "#2563EB", "#7C3AED", "#D97706", "#0891B2",
                     "#15803D", "#64748B", "#DC2626", "#5B21B6" })
            Assert.Contains(color, svg);
    }

    [Fact]
    public void Trend_UsesConciseLabels_AndLimitsVisibleAxisDensity()
    {
        var trend = Enumerable.Range(0, 31)
            .Select(index => new DashboardReportTrendItem(
                new DateOnly(2026, 8, 1).AddDays(index).ToString("MMM d"), index % 4, index % 3))
            .ToArray();

        var svg = InvokePrivate<string>("LineSvg", (object)trend);
        var visibleLabels = Occurrences(svg, "data-axis='date'");

        Assert.InRange(visibleLabels, 2, 8);
        Assert.DoesNotContain("–", svg);
        Assert.Contains("Aug 1", svg);
        Assert.Contains("Aug 29", svg);
        Assert.DoesNotContain("Aug 31", svg);
    }

    [Fact]
    public void Trend_ShowsWholeNumberValuesForBothSeries()
    {
        DashboardReportTrendItem[] trend =
            [new("Aug 1", 2, 2), new("Aug 2", 1, 0), new("Aug 3", 0, 1)];

        var svg = InvokePrivate<string>("LineSvg", (object)trend);

        Assert.Equal(2, Occurrences(svg, "data-series='created-value'"));
        Assert.Equal(2, Occurrences(svg, "data-series='resolved-value'"));
        Assert.Contains(">2</text>", svg);
        Assert.Contains("data-region='legend'", svg);
        Assert.Contains("paint-order='stroke'", svg);
        Assert.Contains("data-label-position='above'", svg);
        Assert.Contains("data-label-position='below'", svg);
    }

    [Fact]
    public void Trend_UsesDynamicIntegerYAxisAndHorizontalGridLines()
    {
        DashboardReportTrendItem[] trend =
            [new("Aug 1", 1, 1), new("Aug 2", 3, 2), new("Aug 3", 0, 1)];

        var svg = InvokePrivate<string>("LineSvg", (object)trend);

        Assert.Equal(5, Occurrences(svg, "data-axis='count'"));
        Assert.Equal(5, Occurrences(svg, "data-grid='horizontal'"));
        foreach (var value in Enumerable.Range(0, 5))
            Assert.Contains($">{value}</text>", svg);
        Assert.DoesNotContain(">0.5</text>", svg);
        Assert.DoesNotContain(">1.5</text>", svg);
        Assert.Contains("data-axis-line='x'", svg);
        Assert.Contains("data-axis-line='y'", svg);
        Assert.Contains("data-series='created-line'", svg);
        Assert.Contains("data-series='resolved-line'", svg);
    }

    [Fact]
    public void Trend_WithNoActivity_UsesEmptyState()
    {
        DashboardReportTrendItem[] trend =
            [new("Aug 1", 0, 0), new("Aug 2", 0, 0)];

        var svg = InvokePrivate<string>("LineSvg", (object)trend);

        Assert.Contains("No ticket activity in this period.", svg);
        Assert.DoesNotContain("data-series='created-line'", svg);
        Assert.DoesNotContain("data-grid='horizontal'", svg);
    }

    [Fact]
    public void Trend_CurrentSampleTicks_EndAtAug22WithoutCrowdingAug23()
    {
        var trend = Enumerable.Range(0, 23)
            .Select(index => new DashboardReportTrendItem(
                new DateOnly(2026, 8, 1).AddDays(index).ToString("MMM d"),
                index == 10 ? 5 : index % 2, index % 3 == 0 ? 1 : 0))
            .ToArray();

        var svg = InvokePrivate<string>("LineSvg", (object)trend);

        Assert.Equal(8, Occurrences(svg, "data-axis='date'"));
        Assert.Contains("Aug 22", svg);
        Assert.DoesNotContain("Aug 23", svg);
        Assert.Contains(">5</text>", svg);
    }

    [Fact]
    public void ReportComposition_MatchesRoleDashboards()
    {
        var admin = InvokePrivate<IReadOnlySet<string>>(
            "ReportSectionNames", RoleNames.Admin);
        var manager = InvokePrivate<IReadOnlySet<string>>(
            "ReportSectionNames", RoleNames.Manager);

        Assert.Contains("CREATED VS RESOLVED", admin);
        Assert.Contains("TICKETS BY CATEGORY", admin);
        Assert.DoesNotContain("PRIORITY OVERVIEW", admin);
        Assert.DoesNotContain("CREATED VS RESOLVED", manager);
        Assert.DoesNotContain("TICKETS BY CATEGORY", manager);
        Assert.Contains("PRIORITY OVERVIEW", manager);
    }

    [Theory]
    [InlineData("2026-08-01", "2026-08-31", 31)]
    [InlineData("2026-08-01", "2026-08-30", 30)]
    [InlineData("2026-06-01", "2026-08-29", 13)]
    [InlineData("2026-01-01", "2026-04-30", 4)]
    public void Trend_AggregationPreservesPresetAndCustomPeriods(
        string fromValue, string toValue, int expectedBuckets)
    {
        var from = DateOnly.Parse(fromValue);
        var to = DateOnly.Parse(toValue);
        var rows = new List<(DateTime Created, DateTime? Resolved)>();

        var trend = InvokePrivate<IReadOnlyCollection<DashboardReportTrendItem>>(
            "BuildTrend", from, to, rows, TimeZoneInfo.Utc);

        Assert.Equal(expectedBuckets, trend.Count);
        Assert.All(trend, item => Assert.DoesNotContain("–", item.Label));
    }

    [Fact]
    public void LargeReport_PaginatesWithoutLayoutFailure()
    {
        var categories = Enumerable.Range(1, 50)
            .Select(index => new DashboardReportChartItem($"Long Support Category {index}", index))
            .ToArray();
        var workloads = Enumerable.Range(1, 100)
            .Select(index => new DashboardReportWorkloadItem(
                $"Agent {index}", index % 6, 1, 1, 1)).ToArray();
        var report = new DashboardReportData(
            new DateOnly(2026, 5, 1), new DateOnly(2026, 8, 23), "Test Manager",
            RoleNames.Manager, DateTimeOffset.UtcNow,
            [new("Total Tickets", 16), new("Open Tickets", 2)],
            [new(TicketStatusNames.Open, 2), new(TicketStatusNames.Assigned, 4)],
            Enumerable.Range(0, 17).Select(index =>
                new DashboardReportTrendItem(new DateOnly(2026, 5, 1).AddDays(index * 7).ToString("MMM d"), index, index / 2)).ToArray(),
            categories, [new("Critical", 4), new("High", 5)], workloads, 16, 8);

        var pdf = InvokePrivate<byte[]>("CreatePdf", report);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static T InvokePrivate<T>(string name, params object[] arguments) =>
        (T)(typeof(ResolveHub.Api.Services.Implementations.DashboardReportService)
            .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, arguments) ?? throw new InvalidOperationException());

    private static int Occurrences(string value, string pattern) =>
        value.Split(pattern).Length - 1;

    private static async Task<TicketDetailsDto> CreateTicketAsync(
        ResolveHubApiFactory factory, HttpClient client, string title)
    {
        var lookups = await factory.GetTicketLookupIdsAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title,
            description = $"Report-period test ticket: {title}.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDetailsDto>())!;
    }

    private static async Task<HttpClient> LoginAsync(
        ResolveHubApiFactory factory, string email)
    {
        var client = factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }
}
