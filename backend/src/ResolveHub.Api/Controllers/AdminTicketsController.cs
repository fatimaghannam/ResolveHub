using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminTicketsController(IAdminTicketService service)
    : ControllerBase
{
    [HttpGet("ticket-assignments")]
    public async Task<ActionResult<AdminAssignmentOverviewDto>> GetAssignments(
        CancellationToken token) =>
        Ok(await service.GetAssignmentsAsync(token));

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardSummaryDto>> GetDashboard(
        CancellationToken token) =>
        Ok(await service.GetDashboardAsync(token));

    [HttpPost("tickets/{ticketReference}/assign")]
    public async Task<IActionResult> Assign(
        string ticketReference,
        [FromBody] AssignTicketRequestDto request,
        CancellationToken token)
    {
        var result = await service.AssignAsync(
            GetUserId(), ticketReference, request.AgentUserId, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict =>
                Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    private int GetUserId()
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException(
                "The authenticated user identifier is invalid.");
    }
}
