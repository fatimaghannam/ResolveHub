using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets/{ticketReference}")]
public sealed class TicketActivityController(ITicketActivityService service) : ControllerBase
{
    [HttpGet("activity")]
    public async Task<ActionResult<IReadOnlyCollection<TicketActivityDto>>> Activity(
        string ticketReference, [FromQuery] bool descending = true, CancellationToken token = default)
    {
        var result = await service.GetTimelineAsync(UserId(), ticketReference, descending, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            _ => NotFound()
        };
    }

    [HttpGet("activity-summary")]
    public async Task<ActionResult<TicketActivitySummaryDto>> Summary(
        string ticketReference, CancellationToken token)
    {
        var result = await service.GetSummaryAsync(UserId(), ticketReference, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            _ => NotFound()
        };
    }

    private int UserId() => int.TryParse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id :
        throw new InvalidOperationException("The authenticated user identifier is invalid.");
}
