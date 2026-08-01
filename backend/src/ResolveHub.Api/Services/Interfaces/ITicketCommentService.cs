using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public enum TicketCommentAudience
{
    Employee,
    Agent,
    Manager,
    Administrator
}

public interface ITicketCommentService
{
    Task<TicketCommentPageDto?> GetAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, string? visibility, int page, int pageSize,
        CancellationToken token);
    Task<TicketServiceResult<TicketCommentDto>> AddAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, AddTicketCommentRequestDto request,
        int? parentCommentId, CancellationToken token);
    Task<TicketServiceResult<TicketCommentDto>> AddWithAttachmentsAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, CreateTicketCommentFormRequest request,
        CancellationToken token);
    Task<TicketServiceResult<TicketCommentDto>> EditAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, int commentId,
        EditTicketCommentRequestDto request, CancellationToken token);
    Task<TicketServiceResult<bool>> DeleteAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, int commentId, CancellationToken token);
    Task<TicketServiceResult<CommentAttachmentDto>> UploadAttachmentAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, int commentId, IFormFile file,
        CancellationToken token);
    Task<AttachmentDownload?> DownloadAttachmentAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, int commentId, int attachmentId,
        CancellationToken token);
}
