using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/agent/dashboard")]
[Authorize(Roles = RoleNames.ITAgent)]
public sealed class AgentDashboardController(IAgentTicketService service)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AgentDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentDashboardDto>> Get(CancellationToken token) =>
        Ok(await service.GetDashboardAsync(GetUserId(), token));

    private int GetUserId()
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out var id) ? id :
            throw new InvalidOperationException("The authenticated user identifier is invalid.");
    }
}
