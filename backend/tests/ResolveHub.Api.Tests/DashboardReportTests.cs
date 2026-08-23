using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Reports;
using ResolveHub.Api.Services.Interfaces;
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
        Assert.DoesNotContain(">0</text>", svg);
        Assert.Contains("data-region='legend'", svg);
        Assert.Contains("paint-order='stroke'", svg);
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
            .Select(index => new DashboardReportWorkloadItem($"Agent {index}", index % 6, 5,
                Math.Max(0, 5 - index % 6), "Available", 1, 1, 1)).ToArray();
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
