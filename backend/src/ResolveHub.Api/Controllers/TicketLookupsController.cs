using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class TicketLookupsController(ITicketService ticketService)
    : ControllerBase
{
    [HttpGet("ticket-categories")]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    [ProducesResponseType(typeof(IReadOnlyCollection<TicketLookupDto>), 200)]
    public async Task<IActionResult> Categories(CancellationToken token) =>
        Ok(await ticketService.GetCategoriesAsync(token));

    [HttpGet("ticket-priorities")]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    [ProducesResponseType(typeof(IReadOnlyCollection<TicketLookupDto>), 200)]
    public async Task<IActionResult> Priorities(CancellationToken token) =>
        Ok(await ticketService.GetPrioritiesAsync(token));

    [HttpGet("ticket-statuses")]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    [ProducesResponseType(typeof(IReadOnlyCollection<TicketLookupDto>), 200)]
    public async Task<IActionResult> Statuses(CancellationToken token) =>
        Ok(await ticketService.GetStatusesAsync(token));
}
