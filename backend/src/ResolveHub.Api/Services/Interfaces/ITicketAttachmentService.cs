using Microsoft.AspNetCore.Http;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public sealed record AttachmentDownload(
    Stream Stream, string ContentType, string FileName);

public interface ITicketAttachmentService
{
    Task<TicketServiceResult<TicketAttachmentDto>> UploadAsync(
        int userId, int ticketId, IFormFile file, CancellationToken cancellationToken);
    Task<AttachmentDownload?> DownloadAsync(
        int userId, int ticketId, int attachmentId, CancellationToken cancellationToken);
    Task<TicketServiceResult<bool>> DeleteAsync(
        int userId, int ticketId, int attachmentId, CancellationToken cancellationToken);
}
