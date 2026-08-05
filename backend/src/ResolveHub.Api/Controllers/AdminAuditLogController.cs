using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/admin/audit-log")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminAuditLogController(ISystemAuditLogService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SystemAuditPageDto>> Get(
        [FromQuery] SystemAuditFilterDto filter, CancellationToken token)
    {
        if (filter.DateRange == "custom" && filter.FromDate is not null &&
            filter.ToDate is not null && filter.FromDate.Value.Date > filter.ToDate.Value.Date)
            return BadRequest(new { message = "From Date cannot be later than To Date." });

        return Ok(await service.GetAsync(filter, token));
    }
}
