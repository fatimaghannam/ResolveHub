using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminTicketsController(
    IAdminTicketService service,
    IManagerTicketService managerTicketService)
    : ControllerBase
{
    [HttpGet("ticket-assignments")]
    public async Task<ActionResult<AdminAssignmentOverviewDto>> GetAssignments(
        [FromQuery] AdminTicketFilterDto filter, CancellationToken token) =>
        Ok(await service.GetAssignmentsAsync(filter, token));

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardSummaryDto>> GetDashboard(
        CancellationToken token) =>
        Ok(await service.GetDashboardAsync(token));

    [HttpGet("users/agents")]
    public async Task<ActionResult<IReadOnlyCollection<AdminAgentWorkloadDto>>> Agents(
        CancellationToken token) =>
        Ok(await service.GetAgentsAsync(token));

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

    [HttpPost("tickets/{ticketReference}/comments")]
    public async Task<ActionResult<TicketCommentDto>> AddComment(
        string ticketReference, AddTicketCommentRequestDto request,
        CancellationToken token)
    {
        var result = await managerTicketService.AddCommentAsync(
            GetUserId(), ticketReference, request, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => Created(
                $"/api/admin/tickets/{ticketReference}/comments", result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict =>
                Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

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

    [HttpPut("tickets/{ticketReference}/assignment")]
    public async Task<IActionResult> UpdateAssignment(
        string ticketReference, [FromBody] UpdateTicketAssignmentDto request,
        CancellationToken token)
    {
        var result = await service.AssignAsync(
            GetUserId(), ticketReference, request.AgentUserId, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpGet("duplicate-reviews")]
    public async Task<ActionResult<IReadOnlyCollection<DuplicateReviewDto>>>
        DuplicateReviews(CancellationToken token) =>
        Ok(await service.GetPendingDuplicateReviewsAsync(token));

    [HttpPost("duplicate-reviews/{reviewId:int}/{decision}")]
    public async Task<IActionResult> ReviewDuplicate(
        int reviewId, string decision, CancellationToken token)
    {
        if (decision is not ("approve" or "reject")) return BadRequest();
        var result = await service.ReviewDuplicateAsync(
            GetUserId(), reviewId, decision == "approve", token);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound =>
                NotFound(new { message = result.Message }),
            TicketOperationStatus.Conflict =>
                Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpPost("tickets/{ticketReference}/mark-duplicate")]
    public async Task<IActionResult> MarkDuplicate(
        string ticketReference, MarkDuplicateRequestDto request,
        CancellationToken token)
    {
        var result = await service.MarkDuplicateAsync(
            GetUserId(), ticketReference, request, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound =>
                NotFound(new { message = result.Message }),
            TicketOperationStatus.Conflict =>
                Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<IReadOnlyCollection<UserNotificationDto>>> Notifications(
        CancellationToken token) =>
        Ok(await service.GetNotificationsAsync(GetUserId(), token));

    [HttpPatch("notifications/{notificationId:int}/read")]
    public async Task<IActionResult> MarkNotificationRead(
        int notificationId, CancellationToken token) =>
        await service.MarkNotificationReadAsync(GetUserId(), notificationId, token)
            ? NoContent() : NotFound();

    [HttpPatch("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken token)
    {
        await service.MarkAllNotificationsReadAsync(GetUserId(), token);
        return NoContent();
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
