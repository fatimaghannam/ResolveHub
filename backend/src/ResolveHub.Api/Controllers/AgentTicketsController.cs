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
[Route("api/agent/tickets")]
[Authorize(Roles = RoleNames.ITSupportAgent)]
public sealed class AgentTicketsController(
    IAgentTicketService service,
    ITicketAttachmentService attachmentService,
    ITicketCommentService commentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<AgentTicketListItemDto>), 200)]
    public async Task<ActionResult<PagedResultDto<AgentTicketListItemDto>>> All(
        [FromQuery] AgentTicketFilterDto filter, CancellationToken token) =>
        Ok(await service.GetTicketsAsync(GetUserId(), filter, token));

    [HttpGet("open")]
    public async Task<ActionResult<PagedResultDto<AgentTicketListItemDto>>> Open(
        [FromQuery] AgentTicketFilterDto filter, CancellationToken token) =>
        Ok(await service.GetOpenTicketsAsync(GetUserId(), filter, token));

    [HttpGet("history")]
    public async Task<ActionResult<PagedResultDto<AgentTicketListItemDto>>> TicketHistory(
        [FromQuery] AgentTicketFilterDto filter, CancellationToken token) =>
        Ok(await service.GetHistoryTicketsAsync(GetUserId(), filter, token));

    [HttpGet("{ticketReference}")]
    public async Task<ActionResult<AgentTicketDetailsDto>> Get(
        string ticketReference, CancellationToken token)
    {
        var ticket = await service.GetTicketAsync(GetUserId(), ticketReference, token);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost("{ticketReference}/assignment-requests")]
    public IActionResult RequestAssignment(string ticketReference) =>
        StatusCode(403, new { message = "IT Support Agents cannot create assignment approval requests." });

    [HttpPatch("{ticketReference}/status")]
    public async Task<ActionResult<AgentTicketDetailsDto>> UpdateStatus(
        string ticketReference, UpdateAgentTicketStatusRequestDto request,
        CancellationToken token) =>
        Result(await service.UpdateStatusAsync(
            GetUserId(), ticketReference, request, token));

    [HttpPost("{ticketReference}/resolve")]
    public async Task<ActionResult<AgentTicketDetailsDto>> Resolve(
        string ticketReference, ResolveTicketRequestDto request,
        CancellationToken token) =>
        Result(await service.ResolveAsync(GetUserId(), ticketReference, request, token));

    [HttpPost("{ticketReference}/pending")]
    public async Task<ActionResult<AgentTicketWorkflowResultDto>> MarkPending(
        string ticketReference, MarkTicketPendingRequestDto request,
        CancellationToken token) => WorkflowResult(await service.MarkPendingAsync(
            GetUserId(), ticketReference, request, token));

    [HttpPost("{ticketReference}/resume-work")]
    public async Task<ActionResult<AgentTicketWorkflowResultDto>> ResumeWork(
        string ticketReference, CancellationToken token) => WorkflowResult(
            await service.ResumeWorkAsync(GetUserId(), ticketReference, token));

    [HttpPost("{ticketReference}/close")]
    public async Task<ActionResult<AgentTicketDetailsDto>> Close(
        string ticketReference, CloseTicketRequestDto request,
        CancellationToken token) =>
        Result(await service.CloseAsync(GetUserId(), ticketReference, request, token));

    [HttpGet("{ticketReference}/comments")]
    public async Task<ActionResult<TicketCommentPageDto>> Comments(
        string ticketReference, string? visibility = null, int page = 1,
        int pageSize = 15, CancellationToken token = default)
    {
        var comments = await commentService.GetAsync(GetUserId(),
            TicketCommentAudience.Agent, null, ticketReference, visibility,
            page, pageSize, token);
        return comments is null ? NotFound() : Ok(comments);
    }

    [HttpPost("{ticketReference}/comments")]
    [Consumes("application/json")]
    public async Task<ActionResult<TicketCommentDto>> AddComment(
        string ticketReference, AddTicketCommentRequestDto request,
        CancellationToken token)
    {
        var result = await commentService.AddAsync(GetUserId(),
            TicketCommentAudience.Agent, null, ticketReference, request, null, token);
        return CreatedResult(ticketReference, "comments", result);
    }

    [HttpPost("{ticketReference}/comments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TicketCommentDto>> AddCommentWithAttachments(
        string ticketReference, [FromForm] CreateTicketCommentFormRequest request,
        CancellationToken token) => CommentResult(
            await commentService.AddWithAttachmentsAsync(GetUserId(),
                TicketCommentAudience.Agent, null, ticketReference, request, token));

    [HttpPost("{ticketReference}/comments/{commentId:int}/replies")]
    public async Task<ActionResult<TicketCommentDto>> Reply(
        string ticketReference, int commentId, AddTicketCommentRequestDto request,
        CancellationToken token) => CommentResult(await commentService.AddAsync(
            GetUserId(), TicketCommentAudience.Agent, null, ticketReference,
            request, commentId, token));

    [HttpPut("{ticketReference}/comments/{commentId:int}")]
    public async Task<ActionResult<TicketCommentDto>> EditComment(
        string ticketReference, int commentId, EditTicketCommentRequestDto request,
        CancellationToken token) => CommentResult(await commentService.EditAsync(
            GetUserId(), TicketCommentAudience.Agent, null, ticketReference,
            commentId, request, token));

    [HttpDelete("{ticketReference}/comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(
        string ticketReference, int commentId, CancellationToken token) =>
        BooleanResult(await commentService.DeleteAsync(GetUserId(),
            TicketCommentAudience.Agent, null, ticketReference, commentId, token));

    [HttpPost("{ticketReference}/comments/{commentId:int}/attachments")]
    public async Task<ActionResult<CommentAttachmentDto>> UploadCommentAttachment(
        string ticketReference, int commentId, IFormFile file, CancellationToken token) =>
        CommentAttachmentResult(await commentService.UploadAttachmentAsync(GetUserId(),
            TicketCommentAudience.Agent, null, ticketReference, commentId, file, token));

    [HttpGet("{ticketReference}/comments/{commentId:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadCommentAttachment(string ticketReference,
        int commentId, int attachmentId, CancellationToken token)
    {
        var file = await commentService.DownloadAttachmentAsync(GetUserId(),
            TicketCommentAudience.Agent, null, ticketReference, commentId, attachmentId, token);
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

    [HttpGet("{ticketReference}/history")]
    public async Task<ActionResult<IReadOnlyCollection<TicketHistoryDto>>> History(
        string ticketReference, CancellationToken token)
    {
        var items = await service.GetHistoryAsync(GetUserId(), ticketReference, token);
        return items is null ? NotFound() : Ok(items);
    }

    [HttpGet("{ticketReference}/attachments/{attachmentId:int}/download")]
    public async Task<IActionResult> DownloadAttachment(
        string ticketReference, int attachmentId, CancellationToken token)
    {
        var file = await attachmentService.DownloadForAssignedAgentAsync(
            GetUserId(), ticketReference, attachmentId, token);
        return file is null ? NotFound() :
            File(file.Stream, file.ContentType, file.FileName);
    }

    private ActionResult<AgentTicketDetailsDto> Result(
        TicketServiceResult<AgentTicketDetailsDto> result) =>
        result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };

    private ActionResult<AgentTicketWorkflowResultDto> WorkflowResult(
        TicketServiceResult<AgentTicketWorkflowResultDto> result) =>
        result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };

    private ActionResult<IReadOnlyCollection<TicketCommentDto>> Collection(
        IReadOnlyCollection<TicketCommentDto>? items) =>
        items is null ? NotFound() : Ok(items);

    private ActionResult<TicketCommentDto> CreatedResult(
        string ticketReference, string segment,
        TicketServiceResult<TicketCommentDto> result) =>
        result.Status switch
        {
            TicketOperationStatus.Success => Created(
                $"/api/agent/tickets/{ticketReference}/{segment}", result.Value),
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
        result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };

    private int GetUserId()
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out var id) ? id :
            throw new InvalidOperationException("The authenticated user identifier is invalid.");
    }
}
