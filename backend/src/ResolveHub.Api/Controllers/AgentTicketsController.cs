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
[Authorize(Roles = RoleNames.ITAgent)]
public sealed class AgentTicketsController(
    IAgentTicketService service,
    ITicketAttachmentService attachmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<AgentTicketListItemDto>), 200)]
    public async Task<ActionResult<PagedResultDto<AgentTicketListItemDto>>> All(
        [FromQuery] AgentTicketFilterDto filter, CancellationToken token) =>
        Ok(await service.GetTicketsAsync(GetUserId(), filter, token));

    [HttpGet("{ticketReference}")]
    public async Task<ActionResult<AgentTicketDetailsDto>> Get(
        string ticketReference, CancellationToken token)
    {
        var ticket = await service.GetTicketAsync(GetUserId(), ticketReference, token);
        return ticket is null ? NotFound() : Ok(ticket);
    }

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

    [HttpGet("{ticketReference}/comments")]
    public async Task<ActionResult<IReadOnlyCollection<TicketCommentDto>>> Comments(
        string ticketReference, CancellationToken token) =>
        Collection(await service.GetCommentsAsync(
            GetUserId(), ticketReference, false, token));

    [HttpPost("{ticketReference}/comments")]
    public async Task<ActionResult<TicketCommentDto>> AddComment(
        string ticketReference, AddTicketCommentRequestDto request,
        CancellationToken token)
    {
        var result = await service.AddCommentAsync(
            GetUserId(), ticketReference, request, false, token);
        return CreatedResult(ticketReference, "comments", result);
    }

    [HttpGet("{ticketReference}/internal-notes")]
    public async Task<ActionResult<IReadOnlyCollection<TicketCommentDto>>> InternalNotes(
        string ticketReference, CancellationToken token) =>
        Collection(await service.GetCommentsAsync(
            GetUserId(), ticketReference, true, token));

    [HttpPost("{ticketReference}/internal-notes")]
    public async Task<ActionResult<TicketCommentDto>> AddInternalNote(
        string ticketReference, AddTicketCommentRequestDto request,
        CancellationToken token)
    {
        var result = await service.AddCommentAsync(
            GetUserId(), ticketReference, request, true, token);
        return CreatedResult(ticketReference, "internal-notes", result);
    }

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
