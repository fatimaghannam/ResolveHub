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
public sealed class ManagerTicketsController(
    IManagerTicketService service,
    ITicketCommentService commentService) : ControllerBase
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
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpGet("assignment-requests")]
    public async Task<ActionResult<IReadOnlyCollection<TicketAssignmentRequestDto>>>
        AssignmentRequests(CancellationToken token) =>
        Ok(await service.GetAssignmentRequestsAsync(token));

    [HttpPost("assignment-requests/{requestId:int}/approve")]
    public async Task<IActionResult> ApproveAssignmentRequest(
        int requestId, CancellationToken token) =>
        ReviewResult(await service.ReviewAssignmentRequestAsync(
            UserId, requestId, true, token));

    [HttpPost("assignment-requests/{requestId:int}/reject")]
    public async Task<IActionResult> RejectAssignmentRequest(
        int requestId, CancellationToken token) =>
        ReviewResult(await service.ReviewAssignmentRequestAsync(
            UserId, requestId, false, token));

    [HttpPost("tickets/{ticketReference}/comments")]
    [Consumes("application/json")]
    public async Task<ActionResult<TicketCommentDto>> AddComment(
        string ticketReference, AddTicketCommentRequestDto request,
        CancellationToken token)
    {
        var result = await commentService.AddAsync(UserId,
            TicketCommentAudience.Manager, null, ticketReference,
            request, null, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => Created(
                $"/api/manager/tickets/{ticketReference}/comments", result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
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
            await commentService.AddWithAttachmentsAsync(UserId,
                TicketCommentAudience.Manager, null, ticketReference, request, token));

    [HttpGet("tickets/{ticketReference}/comments")]
    public async Task<ActionResult<TicketCommentPageDto>> Comments(
        string ticketReference, string? visibility = null, int page = 1,
        int pageSize = 15, CancellationToken token = default)
    {
        var comments = await commentService.GetAsync(UserId,
            TicketCommentAudience.Manager, null, ticketReference, visibility,
            page, pageSize, token);
        return comments is null ? NotFound() : Ok(comments);
    }

    [HttpPost("tickets/{ticketReference}/comments/{commentId:int}/replies")]
    public async Task<ActionResult<TicketCommentDto>> Reply(
        string ticketReference, int commentId, AddTicketCommentRequestDto request,
        CancellationToken token) => CommentResult(await commentService.AddAsync(
            UserId, TicketCommentAudience.Manager, null, ticketReference,
            request, commentId, token));

    [HttpPut("tickets/{ticketReference}/comments/{commentId:int}")]
    public async Task<ActionResult<TicketCommentDto>> EditComment(
        string ticketReference, int commentId, EditTicketCommentRequestDto request,
        CancellationToken token) => CommentResult(await commentService.EditAsync(
            UserId, TicketCommentAudience.Manager, null, ticketReference,
            commentId, request, token));

    [HttpDelete("tickets/{ticketReference}/comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(
        string ticketReference, int commentId, CancellationToken token) =>
        BooleanResult(await commentService.DeleteAsync(UserId,
            TicketCommentAudience.Manager, null, ticketReference, commentId, token));

    [HttpPost("tickets/{ticketReference}/comments/{commentId:int}/attachments")]
    public async Task<ActionResult<CommentAttachmentDto>> UploadCommentAttachment(
        string ticketReference, int commentId, IFormFile file, CancellationToken token) =>
        CommentAttachmentResult(await commentService.UploadAttachmentAsync(UserId,
            TicketCommentAudience.Manager, null, ticketReference, commentId, file, token));

    [HttpGet("tickets/{ticketReference}/comments/{commentId:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadCommentAttachment(string ticketReference,
        int commentId, int attachmentId, CancellationToken token)
    {
        var file = await commentService.DownloadAttachmentAsync(UserId,
            TicketCommentAudience.Manager, null, ticketReference, commentId,
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

    [HttpPost("tickets/{ticketReference}/duplicate-reviews")]
    public async Task<ActionResult<DuplicateReviewDto>> ReportDuplicate(
        string ticketReference, CreateDuplicateReviewRequestDto request,
        CancellationToken token)
    {
        var result = await service.ReportDuplicateAsync(
            UserId, ticketReference, request, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => Created(
                $"/api/manager/tickets/{ticketReference}/duplicate-reviews/{result.Value!.Id}",
                result.Value),
            TicketOperationStatus.NotFound => NotFound(new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<IReadOnlyCollection<UserNotificationDto>>> Notifications(
        CancellationToken token) => Ok(await service.GetNotificationsAsync(UserId, token));

    [HttpPatch("notifications/{notificationId:int}/read")]
    public async Task<IActionResult> MarkNotificationRead(
        int notificationId, CancellationToken token) =>
        await service.MarkNotificationReadAsync(UserId, notificationId, token)
            ? NoContent() : NotFound();

    [HttpPatch("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken token)
    {
        await service.MarkAllNotificationsReadAsync(UserId, token);
        return NoContent();
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

    private IActionResult ReviewResult(TicketServiceResult<bool> result) =>
        result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };

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
        result.Status == TicketOperationStatus.Forbidden
            ? StatusCode(403, new { message = result.Message })
            : ReviewResult(result);
}
