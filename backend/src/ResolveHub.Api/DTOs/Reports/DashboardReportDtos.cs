using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Reports;

public sealed class DashboardReportRequest
{
    [Required] public DateOnly? From { get; init; }
    [Required] public DateOnly? To { get; init; }
    [StringLength(100)] public string? TimeZone { get; init; }
}

public sealed record DashboardReportMetric(string Name, int Value);
public sealed record DashboardReportChartItem(string Name, int Value);
public sealed record DashboardReportTrendItem(string Label, int Created, int Resolved);
public sealed record DashboardReportWorkloadItem(
    string Name, int Assigned, int Worked, int Resolved, int Closed);

public sealed record DashboardReportData(
    DateOnly From, DateOnly To, string GeneratedBy, string Role,
    DateTimeOffset GeneratedAt,
    IReadOnlyCollection<DashboardReportMetric> Metrics,
    IReadOnlyCollection<DashboardReportChartItem> Statuses,
    IReadOnlyCollection<DashboardReportTrendItem> Trend,
    IReadOnlyCollection<DashboardReportChartItem> Categories,
    IReadOnlyCollection<DashboardReportChartItem> Priorities,
    IReadOnlyCollection<DashboardReportWorkloadItem> Workloads,
    int CreatedDuringPeriod,
    int ResolvedDuringPeriod);

public sealed record DashboardReportFile(byte[] Content, string FileName);
