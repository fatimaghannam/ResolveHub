using ResolveHub.Api.DTOs.Tickets;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAdminTicketReportService
{
    byte[] CreatePdf(AdminTicketReportDto report, string generatedBy, DateTimeOffset generatedAt, string? timeZoneId);
    byte[] CreateExcel(AdminTicketReportDto report, string generatedBy, DateTimeOffset generatedAt, string? timeZoneId);
    string CreateFileName(AdminTicketReportDto report, string extension, DateTimeOffset generatedAt, string? timeZoneId);
}
