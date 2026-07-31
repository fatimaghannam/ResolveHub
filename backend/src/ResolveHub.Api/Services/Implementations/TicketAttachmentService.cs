using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;
using ResolveHub.Api.Settings;

namespace ResolveHub.Api.Services.Implementations;

public sealed class TicketAttachmentService(
    ApplicationDbContext dbContext,
    IOptions<FileStorageSettings> options,
    IWebHostEnvironment environment) : ITicketAttachmentService
{
    private static readonly Dictionary<string, string[]> AllowedTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = ["image/png"],
            [".jpg"] = ["image/jpeg"], [".jpeg"] = ["image/jpeg"],
            [".pdf"] = ["application/pdf"],
            [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
            [".txt"] = ["text/plain"], [".log"] = ["text/plain", "application/octet-stream"],
            [".zip"] = ["application/zip", "application/x-zip-compressed", "application/octet-stream"]
        };

    public async Task<TicketServiceResult<TicketAttachmentDto>> UploadAsync(
        int userId, int ticketId, IFormFile file, CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item => item.ID == ticketId &&
                item.CreatedByUserAccountID == userId && !item.IsDeleted,
                cancellationToken);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: DuplicateTicketRules.ReadOnlyMessage);
        if (ticket.TicketStatus.Name != TicketStatusNames.Open ||
            ticket.AssignedToUserAccountID != null)
            return new(TicketOperationStatus.Conflict,
                Message: "Attachments can no longer be added because work has started.");

        var settings = options.Value;
        var count = await dbContext.TicketAttachments.CountAsync(
            item => item.TicketID == ticketId && !item.IsDeleted, cancellationToken);
        if (count >= settings.MaxFilesPerTicket)
            return new(TicketOperationStatus.Invalid,
                Message: $"A ticket may have at most {settings.MaxFilesPerTicket} attachments.");
        if (file.Length <= 0 || file.Length > settings.MaxFileSizeBytes)
            return new(TicketOperationStatus.Invalid,
                Message: "The file is empty or exceeds the 10 MB limit.");

        var originalName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalName);
        if (!AllowedTypes.TryGetValue(extension, out var contentTypes) ||
            !contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return new(TicketOperationStatus.Invalid,
                Message: "This file type is not allowed.");

        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativePath = Path.Combine(ticketId.ToString(), storedName);
        var root = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath, settings.UploadRoot));
        var directory = Path.Combine(root, ticketId.ToString());
        Directory.CreateDirectory(directory);
        var physicalPath = Path.Combine(directory, storedName);

        try
        {
            await using (var stream = new FileStream(
                physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
                await file.CopyToAsync(stream, cancellationToken);

            var attachment = new TicketAttachment
            {
                TicketID = ticketId,
                UploadedByUserAccountID = userId,
                FileName = originalName,
                StoredFileName = storedName,
                FilePath = relativePath,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                UploadedDate = DateTime.UtcNow
            };
            dbContext.TicketAttachments.Add(attachment);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new(TicketOperationStatus.Success,
                new TicketAttachmentDto(attachment.ID, attachment.FileName,
                    attachment.ContentType, attachment.FileSizeBytes,
                    attachment.UploadedDate, true));
        }
        catch
        {
            if (File.Exists(physicalPath)) File.Delete(physicalPath);
            throw;
        }
    }

    public async Task<AttachmentDownload?> DownloadAsync(
        int userId, int ticketId, int attachmentId, CancellationToken cancellationToken)
    {
        var item = await dbContext.TicketAttachments.AsNoTracking()
            .Where(attachment => attachment.ID == attachmentId &&
                attachment.TicketID == ticketId && !attachment.IsDeleted &&
                attachment.Ticket.CreatedByUserAccountID == userId &&
                !attachment.Ticket.IsDeleted)
            .Select(attachment => new
            {
                attachment.FilePath, attachment.ContentType, attachment.FileName
            }).SingleOrDefaultAsync(cancellationToken);
        if (item is null) return null;
        var root = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath, options.Value.UploadRoot));
        var path = Path.GetFullPath(Path.Combine(root, item.FilePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path)) return null;
        return new AttachmentDownload(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read),
            item.ContentType, item.FileName);
    }

    public async Task<AttachmentDownload?> DownloadForAssignedAgentAsync(
        int agentId,
        string ticketReference,
        int attachmentId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.TicketAttachments.AsNoTracking()
            .Where(attachment =>
                attachment.ID == attachmentId &&
                !attachment.IsDeleted &&
                !attachment.Ticket.IsDeleted &&
                attachment.Ticket.TicketReferenceNumber == ticketReference &&
                attachment.Ticket.AssignedToUserAccountID == agentId)
            .Select(attachment => new
            {
                attachment.FilePath,
                attachment.ContentType,
                attachment.FileName
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (item is null) return null;

        var root = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath, options.Value.UploadRoot));
        var path = Path.GetFullPath(Path.Combine(root, item.FilePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path))
            return null;

        return new AttachmentDownload(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read),
            item.ContentType,
            item.FileName);
    }

    public async Task<TicketServiceResult<bool>> DeleteAsync(
        int userId, int ticketId, int attachmentId, CancellationToken cancellationToken)
    {
        var item = await dbContext.TicketAttachments
            .Include(attachment => attachment.Ticket).ThenInclude(ticket => ticket.TicketStatus)
            .SingleOrDefaultAsync(attachment => attachment.ID == attachmentId &&
                attachment.TicketID == ticketId && !attachment.IsDeleted &&
                attachment.Ticket.CreatedByUserAccountID == userId &&
                !attachment.Ticket.IsDeleted, cancellationToken);
        if (item is null) return new(TicketOperationStatus.NotFound);
        if (DuplicateTicketRules.IsDuplicate(item.Ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: DuplicateTicketRules.ReadOnlyMessage);
        if (item.Ticket.TicketStatus.Name != TicketStatusNames.Open ||
            item.Ticket.AssignedToUserAccountID != null)
            return new(TicketOperationStatus.Conflict,
                Message: "The attachment can no longer be removed because work has started.");
        item.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(TicketOperationStatus.Success, true);
    }
}
