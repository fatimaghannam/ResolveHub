using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/employee/dashboard")]
[Authorize(Roles = RoleNames.Employee)]
public sealed class EmployeeDashboardController(ITicketService ticketService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(TicketDashboardSummaryDto), 200)]
    public async Task<ActionResult<TicketDashboardSummaryDto>> GetDashboard(
        CancellationToken cancellationToken)
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(value, out var userId))
            throw new InvalidOperationException(
                "The authenticated user identifier is invalid.");

        return Ok(await ticketService.GetDashboardAsync(
            userId, cancellationToken));
    }
}
