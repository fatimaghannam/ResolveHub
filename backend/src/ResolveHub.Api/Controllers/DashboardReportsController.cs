using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Reports;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/reports/dashboard")]
[Authorize(Roles = RoleNames.Admin + "," + RoleNames.Manager)]
public sealed class DashboardReportsController(
    IDashboardReportService service,
    ILogger<DashboardReportsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DashboardReportRequest request, CancellationToken token)
    {
        if (!request.From.HasValue || !request.To.HasValue)
            return ValidationProblem("A start date and end date are required.");
        if (request.From > request.To)
            return ValidationProblem("The start date cannot be after the end date.");
        if (request.To.Value.DayNumber - request.From.Value.DayNumber > 366)
            return ValidationProblem("The reporting period cannot exceed 366 days.");

        try
        {
            var role = User.IsInRole(RoleNames.Admin) ? RoleNames.Admin : RoleNames.Manager;
            var report = await service.CreateAsync(request,
                User.Identity?.Name ?? role, role, token);
            return File(report.Content, "application/pdf", report.FileName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Dashboard report generation failed.");
            return StatusCode(500, new
            {
                message = "The dashboard report could not be generated. Please try again."
            });
        }
    }
}
