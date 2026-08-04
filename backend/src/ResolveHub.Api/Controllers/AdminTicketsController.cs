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
    IAssignmentApprovalService assignmentApprovalService,
    ITicketCommentService commentService)
    : ControllerBase
{
    [HttpGet("ticket-assignments")]
    public async Task<ActionResult<AdminAssignmentOverviewDto>> GetAssignments(
        [FromQuery] AdminTicketFilterDto filter, CancellationToken token) =>
        Ok(await service.GetAssignmentsAsync(filter, token));

    [HttpGet("assignment-requests")]
    public async Task<ActionResult<IReadOnlyCollection<TicketAssignmentRequestDto>>>
        AssignmentRequests(CancellationToken token) =>
        Ok(await assignmentApprovalService.GetPendingAdminRequestsAsync(token));

    [HttpPost("assignment-requests/{requestId:int}/{decision}")]
    public async Task<IActionResult> ReviewAssignmentRequest(
        int requestId, string decision, [FromBody] ReviewAssignmentRequestDto? request,
        CancellationToken token)
    {
        if (decision is not ("approve" or "reject")) return BadRequest();
        var result = await assignmentApprovalService.ReviewAsync(GetUserId(), requestId,
            decision == "approve", request?.Reason, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardSummaryDto>> GetDashboard(
        CancellationToken token) =>
        Ok(await service.GetDashboardAsync(token));

    [HttpGet("users/agents")]
    public async Task<ActionResult<IReadOnlyCollection<AdminAgentWorkloadDto>>> Agents(
        CancellationToken token) =>
        Ok(await service.GetAgentsAsync(token));

    [HttpGet("tickets")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
    [Consumes("application/json")]
    public async Task<ActionResult<TicketCommentDto>> AddComment(
        string ticketReference, AddTicketCommentRequestDto request,
        CancellationToken token)
    {
        var result = await commentService.AddAsync(GetUserId(),
            TicketCommentAudience.Administrator, null, ticketReference,
            request, null, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => Created(
                $"/api/admin/tickets/{ticketReference}/comments", result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
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
        int reviewId, string decision,
        [FromBody] ReviewDuplicateRequestDto? request,
        CancellationToken token)
    {
        if (decision is not ("approve" or "reject")) return BadRequest();
        var result = await service.ReviewDuplicateAsync(
            GetUserId(), reviewId, decision == "approve",
            request?.InternalNote, token);
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
        string ticketReference, [FromBody] MarkDuplicateRequestDto request,
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

    [HttpPost("tickets/{ticketReference}/comments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TicketCommentDto>> AddCommentWithAttachments(
        string ticketReference, [FromForm] CreateTicketCommentFormRequest request,
        CancellationToken token) => CommentResult(
            await commentService.AddWithAttachmentsAsync(GetUserId(),
                TicketCommentAudience.Administrator, null, ticketReference,
                request, token));

    [HttpGet("tickets/{ticketReference}/comments")]
    public async Task<ActionResult<TicketCommentPageDto>> Comments(
        string ticketReference, string? visibility = null, int page = 1,
        int pageSize = 15, CancellationToken token = default)
    {
        var comments = await commentService.GetAsync(GetUserId(),
            TicketCommentAudience.Administrator, null, ticketReference, visibility,
            page, pageSize, token);
        return comments is null ? NotFound() : Ok(comments);
    }

    [HttpPost("tickets/{ticketReference}/comments/{commentId:int}/replies")]
    public async Task<ActionResult<TicketCommentDto>> Reply(
        string ticketReference, int commentId, AddTicketCommentRequestDto request,
        CancellationToken token) => CommentResult(await commentService.AddAsync(
            GetUserId(), TicketCommentAudience.Administrator, null, ticketReference,
            request, commentId, token));

    [HttpPut("tickets/{ticketReference}/comments/{commentId:int}")]
    public async Task<ActionResult<TicketCommentDto>> EditComment(
        string ticketReference, int commentId, EditTicketCommentRequestDto request,
        CancellationToken token) => CommentResult(await commentService.EditAsync(
            GetUserId(), TicketCommentAudience.Administrator, null, ticketReference,
            commentId, request, token));

    [HttpDelete("tickets/{ticketReference}/comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(
        string ticketReference, int commentId, CancellationToken token) =>
        BooleanResult(await commentService.DeleteAsync(GetUserId(),
            TicketCommentAudience.Administrator, null, ticketReference,
            commentId, token));

    [HttpPost("tickets/{ticketReference}/comments/{commentId:int}/attachments")]
    public async Task<ActionResult<CommentAttachmentDto>> UploadCommentAttachment(
        string ticketReference, int commentId, IFormFile file, CancellationToken token) =>
        CommentAttachmentResult(await commentService.UploadAttachmentAsync(GetUserId(),
            TicketCommentAudience.Administrator, null, ticketReference, commentId, file, token));

    [HttpGet("tickets/{ticketReference}/comments/{commentId:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadCommentAttachment(string ticketReference,
        int commentId, int attachmentId, CancellationToken token)
    {
        var file = await commentService.DownloadAttachmentAsync(GetUserId(),
            TicketCommentAudience.Administrator, null, ticketReference, commentId,
            attachmentId, token);
        return file is null ? NotFound() : File(file.Stream, file.ContentType, file.FileName);
    }

    private ActionResult<CommentAttachmentDto> CommentAttachmentResult(
        TicketServiceResult<CommentAttachmentDto> result) => result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };

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

    private ActionResult<TicketCommentDto> CommentResult(
        TicketServiceResult<TicketCommentDto> result) => result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };

    private IActionResult BooleanResult(TicketServiceResult<bool> result) =>
        result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
}
