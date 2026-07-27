using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/tickets/{ticketId:int}/attachments")]
[Authorize]
public sealed class TicketAttachmentsController(
    ITicketAttachmentService attachmentService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
    [RequestSizeLimit(10 * 1024 * 1024 + 64 * 1024)]
    public async Task<ActionResult<TicketAttachmentDto>> Upload(
        int ticketId, IFormFile file, CancellationToken cancellationToken)
    {
        var result = await attachmentService.UploadAsync(
            GetUserId(), ticketId, file, cancellationToken);
        return result.Status switch
        {
            TicketOperationStatus.Success => Created(
                $"/api/tickets/{ticketId}/attachments/{result.Value!.Id}/download",
                result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpGet("{attachmentId:int}/download")]
    [Authorize(Roles = RoleNames.Employee)]
    public async Task<IActionResult> Download(
        int ticketId, int attachmentId, CancellationToken cancellationToken)
    {
        var result = await attachmentService.DownloadAsync(
            GetUserId(), ticketId, attachmentId, cancellationToken);
        return result is null
            ? NotFound()
            : File(result.Stream, result.ContentType, result.FileName);
    }

    [HttpDelete("{attachmentId:int}")]
    [Authorize(Roles = RoleNames.Employee)]
    public async Task<IActionResult> Delete(
        int ticketId, int attachmentId, CancellationToken cancellationToken)
    {
        var result = await attachmentService.DeleteAsync(
            GetUserId(), ticketId, attachmentId, cancellationToken);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    private int GetUserId() =>
        int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}
