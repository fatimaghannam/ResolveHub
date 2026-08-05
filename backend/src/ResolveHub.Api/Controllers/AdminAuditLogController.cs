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
        if (filter.FromUtc.HasValue != filter.ToUtcExclusive.HasValue)
            return BadRequest(new { message = "Both UTC date boundaries are required." });
        if (filter.FromUtc is not null && filter.ToUtcExclusive <= filter.FromUtc)
            return BadRequest(new { message = "The end boundary must be later than the start boundary." });

        return Ok(await service.GetAsync(filter, token));
    }
}
