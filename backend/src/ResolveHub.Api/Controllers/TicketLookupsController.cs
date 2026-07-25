using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Roles = RoleNames.Employee)]
public sealed class TicketLookupsController(ITicketService ticketService)
    : ControllerBase
{
    [HttpGet("ticket-categories")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TicketLookupDto>), 200)]
    public async Task<IActionResult> Categories(CancellationToken token) =>
        Ok(await ticketService.GetCategoriesAsync(token));

    [HttpGet("ticket-priorities")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TicketLookupDto>), 200)]
    public async Task<IActionResult> Priorities(CancellationToken token) =>
        Ok(await ticketService.GetPrioritiesAsync(token));

    [HttpGet("ticket-statuses")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TicketLookupDto>), 200)]
    public async Task<IActionResult> Statuses(CancellationToken token) =>
        Ok(await ticketService.GetStatusesAsync(token));
}
