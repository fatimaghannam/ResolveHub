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
[Route("api/tickets")]
[Authorize]
public sealed class TicketsController(
    ITicketService ticketService,
    ITicketCommentService commentService)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    [ProducesResponseType(typeof(PagedResultDto<TicketListItemDto>), 200)]
    public async Task<ActionResult<PagedResultDto<TicketListItemDto>>> GetTickets(
        [FromQuery] TicketFilterDto filter,
        CancellationToken cancellationToken)
    {
        return Ok(await ticketService.GetTicketsAsync(
            GetUserId(), filter, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    [ProducesResponseType(typeof(TicketDetailsDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<TicketDetailsDto>> GetTicket(
        int id,
        CancellationToken cancellationToken)
    {
        var ticket = await ticketService.GetTicketAsync(
            GetUserId(), id, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    [ProducesResponseType(typeof(TicketDetailsDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<TicketDetailsDto>> CreateTicket(
        [FromBody] CreateTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.CreateTicketAsync(
            GetUserId(), request, cancellationToken);
        if (result.Status == TicketOperationStatus.Invalid)
            return BadRequest(new { message = result.Message });

        return CreatedAtAction(
            nameof(GetTicket),
            new { id = result.Value!.Id },
            result.Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    [ProducesResponseType(typeof(TicketDetailsDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<TicketDetailsDto>> UpdateTicket(
        int id,
        [FromBody] UpdateTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.UpdateTicketAsync(
            GetUserId(), id, request, cancellationToken);
        return result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict =>
                ModificationDenied(result.Message),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CancelTicket(
        int id,
        [FromBody] CancelTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await ticketService.CancelTicketAsync(
            GetUserId(), id, request, cancellationToken);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict =>
                ModificationDenied(result.Message),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpGet("{id:int}/comments")]
    [Authorize(Roles = RoleNames.Employee)]
    [ProducesResponseType(typeof(TicketCommentPageDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<TicketCommentPageDto>> GetComments(
        int id, string? visibility = null, int page = 1, int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        var comments = await commentService.GetAsync(GetUserId(),
            TicketCommentAudience.Employee, id, null, visibility, page, pageSize,
            cancellationToken);
        return comments is null ? NotFound() : Ok(comments);
    }

    [HttpPost("{id:int}/comments")]
    [Consumes("application/json")]
    [Authorize(Roles = RoleNames.Employee)]
    public async Task<ActionResult<TicketCommentDto>> AddComment(
        int id, AddTicketCommentRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await commentService.AddAsync(GetUserId(),
            TicketCommentAudience.Employee, id, null, request, null,
            cancellationToken);
        return result.Status switch
        {
            TicketOperationStatus.Success => Created(
                $"/api/tickets/{id}/comments", result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Forbidden => StatusCode(403, new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpPost("{id:int}/comments")]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = RoleNames.Employee)]
    public async Task<ActionResult<TicketCommentDto>> AddCommentWithAttachments(
        int id, [FromForm] CreateTicketCommentFormRequest request,
        CancellationToken token) => CommentResult(
            await commentService.AddWithAttachmentsAsync(GetUserId(),
                TicketCommentAudience.Employee, id, null, request, token));

    [HttpPost("{id:int}/comments/{commentId:int}/replies")]
    [Authorize(Roles = RoleNames.Employee)]
    public async Task<ActionResult<TicketCommentDto>> Reply(
        int id, int commentId, AddTicketCommentRequestDto request,
        CancellationToken token) => CommentResult(await commentService.AddAsync(
            GetUserId(), TicketCommentAudience.Employee, id, null, request,
            commentId, token));

    [HttpPut("{id:int}/comments/{commentId:int}")]
    [Authorize(Roles = RoleNames.Employee)]
    public async Task<ActionResult<TicketCommentDto>> EditComment(
        int id, int commentId, EditTicketCommentRequestDto request,
        CancellationToken token) => CommentResult(await commentService.EditAsync(
            GetUserId(), TicketCommentAudience.Employee, id, null, commentId,
            request, token));

    [HttpDelete("{id:int}/comments/{commentId:int}")]
    [Authorize(Roles = RoleNames.Employee)]
    public async Task<IActionResult> DeleteComment(
        int id, int commentId, CancellationToken token) => BooleanResult(
            await commentService.DeleteAsync(GetUserId(),
                TicketCommentAudience.Employee, id, null, commentId, token));

    [HttpPost("{id:int}/comments/{commentId:int}/attachments")]
    [Authorize(Roles = RoleNames.Employee)]
    public async Task<ActionResult<CommentAttachmentDto>> UploadCommentAttachment(
        int id, int commentId, IFormFile file, CancellationToken token) =>
        AttachmentResult(await commentService.UploadAttachmentAsync(GetUserId(),
            TicketCommentAudience.Employee, id, null, commentId, file, token));

    [HttpGet("{id:int}/comments/{commentId:int}/attachments/{attachmentId:int}")]
    [Authorize(Roles = RoleNames.Employee)]
    public async Task<IActionResult> DownloadCommentAttachment(
        int id, int commentId, int attachmentId, CancellationToken token)
    {
        var file = await commentService.DownloadAttachmentAsync(GetUserId(),
            TicketCommentAudience.Employee, id, null, commentId, attachmentId, token);
        return file is null ? NotFound() : File(file.Stream, file.ContentType, file.FileName);
    }

    private ActionResult<CommentAttachmentDto> AttachmentResult(
        TicketServiceResult<CommentAttachmentDto> result) => result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
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
        return int.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException(
                "The authenticated user identifier is invalid.");
    }

    private ActionResult ModificationDenied(string? message) =>
        User.IsInRole(RoleNames.Employee)
            ? Conflict(new { message })
            : StatusCode(StatusCodes.Status403Forbidden, new { message });
}
