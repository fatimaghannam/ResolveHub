using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/manager")]
[Authorize(Roles = RoleNames.Manager)]
public sealed class ManagerTicketsController(IManagerTicketService service) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<ManagerDashboardDto>> Dashboard(CancellationToken token) =>
        Ok(await service.GetDashboardAsync(token));

    [HttpGet("tickets")]
    public async Task<ActionResult<PagedResultDto<AdminTicketListItemDto>>> Tickets(
        [FromQuery] AdminTicketFilterDto filter, CancellationToken token) =>
        Ok(await service.GetTicketsAsync(filter, token));

    [HttpGet("tickets/{ticketReference}")]
    public async Task<ActionResult<AdminTicketDetailsDto>> Ticket(
        string ticketReference, CancellationToken token)
    {
        var ticket = await service.GetTicketAsync(ticketReference, token);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<AdminAssignmentOverviewDto>> Assignments(
        [FromQuery] AdminTicketFilterDto filter, CancellationToken token) =>
        Ok(await service.GetAssignmentsAsync(filter, token));

    [HttpPost("tickets/{ticketReference}/assign")]
    public async Task<IActionResult> Assign(
        string ticketReference, AssignTicketRequestDto request, CancellationToken token)
    {
        var result = await service.AssignAsync(
            UserId, ticketReference, request.AgentUserId, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpGet("workload")]
    public async Task<ActionResult<IReadOnlyCollection<ManagerAgentWorkloadDto>>> Workload(
        CancellationToken token) =>
        Ok(await service.GetWorkloadAsync(token));

    [HttpGet("activity")]
    public async Task<ActionResult<ManagerActivityResultDto>> Activity(
        CancellationToken token) =>
        Ok(await service.GetActivityAsync(token));

    private int UserId
    {
        get
        {
            var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return int.TryParse(value, out var userId)
                ? userId
                : throw new InvalidOperationException(
                    "The authenticated user identifier is invalid.");
        }
    }
}
