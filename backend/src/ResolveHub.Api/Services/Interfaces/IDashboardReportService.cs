using ResolveHub.Api.DTOs.Reports;

namespace ResolveHub.Api.Services.Interfaces;

public interface IDashboardReportService
{
    Task<DashboardReportFile> CreateAsync(
        DashboardReportRequest request, string generatedBy, string role,
        CancellationToken token);
}
