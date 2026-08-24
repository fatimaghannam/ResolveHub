using System.Data;
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

public sealed class TicketCommentService(
    ApplicationDbContext dbContext,
    IOptions<FileStorageSettings>? fileOptions = null,
    IWebHostEnvironment? environment = null)
    : ITicketCommentService
{
    private static readonly Dictionary<string, string[]> AllowedAttachmentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = ["image/png"], [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"], [".gif"] = ["image/gif"],
            [".webp"] = ["image/webp"],
            [".pdf"] = ["application/pdf"],
            [".doc"] = ["application/msword"],
            [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
            [".xls"] = ["application/vnd.ms-excel"],
            [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
            [".txt"] = ["text/plain"],
            [".zip"] = ["application/zip", "application/x-zip-compressed"]
        };

    public async Task<TicketServiceResult<TicketCommentDto>> AddWithAttachmentsAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, CreateTicketCommentFormRequest request,
        CancellationToken token)
    {
        var settings = fileOptions?.Value ?? new FileStorageSettings();
        if (request.Attachments.Count > Math.Min(5, settings.MaxFilesPerTicket))
            return Invalid("A maximum of 5 attachments is allowed per comment.");
        if (request.Attachments
            .GroupBy(file => new { Name = Path.GetFileName(file.FileName).ToUpperInvariant(), file.Length })
            .Any(group => group.Count() > 1))
            return Invalid("The same attachment cannot be added more than once.");
        foreach (var file in request.Attachments)
        {
            var validation = ValidateAttachment(file, settings);
            if (validation is not null) return Invalid(validation);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async strategyToken =>
        {
        dbContext.ChangeTracker.Clear();
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        var storedPaths = new List<string>();
        try
        {
            if (dbContext.Database.IsRelational() &&
                dbContext.Database.CurrentTransaction is null)
                transaction = await dbContext.Database.BeginTransactionAsync(strategyToken);
            var result = await AddAsync(userId, audience, ticketId, ticketReference,
                new AddTicketCommentRequestDto
                {
                    Message = request.Content,
                    Visibility = request.Visibility
                }, request.ParentCommentId, strategyToken);
            if (result.Status != TicketOperationStatus.Success)
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(CancellationToken.None);
                return result;
            }

            foreach (var file in request.Attachments)
            {
                var upload = await UploadAttachmentAsync(userId, audience, ticketId,
                    ticketReference, result.Value!.Id, file, strategyToken);
                if (upload.Status != TicketOperationStatus.Success)
                {
                    if (transaction is not null)
                        await transaction.RollbackAsync(CancellationToken.None);
                    DeleteStoredFiles(storedPaths, settings);
                    return new(upload.Status, Message: upload.Message);
                }
                var path = dbContext.ChangeTracker.Entries<TicketCommentAttachment>()
                    .Where(entry => entry.Entity.ID == upload.Value!.Id)
                    .Select(entry => entry.Entity.FilePath).Single();
                storedPaths.Add(path);
            }

            var ticket = await FindReadableTicketAsync(userId, audience, ticketId,
                ticketReference, strategyToken);
            var createdComment = (await ProjectAsync(ticket!, userId, strategyToken))
                .Single(comment => comment.Id == result.Value!.Id);
            if (transaction is not null)
                await transaction.CommitAsync(strategyToken);
            return new(TicketOperationStatus.Success, createdComment);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            DeleteStoredFiles(storedPaths, settings);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
        }, token);
    }
    public async Task<TicketCommentPageDto?> GetAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, string? visibility, int page, int pageSize,
        CancellationToken token)
    {
        var ticket = await FindReadableTicketAsync(
            userId, audience, ticketId, ticketReference, token);
        if (ticket is null) return null;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var canViewPrivate = userId == ticket.CreatedByUserAccountID ||
                             userId == ticket.AssignedToUserAccountID;
        var visible = dbContext.TicketComments.AsNoTracking()
            .Where(comment => comment.TicketID == ticket.ID &&
                (comment.Visibility == CommentVisibility.Public || canViewPrivate));
        var publicCount = await visible.CountAsync(comment =>
            comment.Visibility == CommentVisibility.Public, token);
        var privateCount = canViewPrivate
            ? await visible.CountAsync(comment =>
                comment.Visibility == CommentVisibility.Private, token)
            : 0;
        if (string.Equals(visibility, nameof(CommentVisibility.Public),
                StringComparison.OrdinalIgnoreCase))
            visible = visible.Where(comment => comment.Visibility == CommentVisibility.Public);
        else if (string.Equals(visibility, nameof(CommentVisibility.Private),
                     StringComparison.OrdinalIgnoreCase) && canViewPrivate)
            visible = visible.Where(comment => comment.Visibility == CommentVisibility.Private);
        else if (!string.IsNullOrWhiteSpace(visibility) &&
                 !string.Equals(visibility, "All", StringComparison.OrdinalIgnoreCase))
            visible = visible.Where(_ => false);

        var roots = visible.Where(comment => comment.ParentCommentID == null);
        var totalThreads = await roots.CountAsync(token);
        var rootIds = await roots.OrderBy(comment => comment.CreatedDate)
            .Select(comment => comment.ID)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
        var items = await ProjectQuery(ticket, userId, rootIds).ToListAsync(token);
        var totalVisibleComments = await visible.CountAsync(token);
        return new(items, page, pageSize, totalThreads, totalVisibleComments,
            publicCount, privateCount, page * pageSize < totalThreads);
    }

    public async Task<TicketServiceResult<TicketCommentDto>> AddAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, AddTicketCommentRequestDto request,
        int? parentCommentId, CancellationToken token)
    {
        var content = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > TicketCommentRules.MaximumMessageLength)
            return Invalid("Comment content is required and cannot exceed 5000 characters.");

        var ticket = await FindWritableTicketAsync(
            userId, audience, ticketId, ticketReference, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (TicketCommentRules.IsReadOnly(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict, Message:
                DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name)
                    ? DuplicateTicketRules.ReadOnlyMessage
                    : "Comments cannot be added to a closed or cancelled ticket.");

        TicketComment? parent = null;
        CommentVisibility visibility;
        if (parentCommentId is not null)
        {
            parent = await dbContext.TicketComments.SingleOrDefaultAsync(comment =>
                comment.ID == parentCommentId && comment.TicketID == ticket.ID, token);
            if (parent is null) return new(TicketOperationStatus.NotFound,
                Message: "The parent comment could not be found.");
            if (parent.ParentCommentID is not null)
                return Invalid("Replies can only be added to top-level comments.");
            visibility = parent.Visibility;
        }
        else if (!TicketCommentRules.TryParseVisibility(
                     request.Visibility, out visibility))
            return Invalid("Visibility must be Public or Private.");

        if (visibility == CommentVisibility.Private &&
            userId != ticket.CreatedByUserAccountID &&
            userId != ticket.AssignedToUserAccountID)
            return new(TicketOperationStatus.Forbidden,
                Message: "Only the ticket creator and assigned IT Agent can add Private comments.");

        var now = DateTime.UtcNow;
        var comment = new TicketComment
        {
            TicketID = ticket.ID,
            AuthorUserAccountID = userId,
            ParentCommentID = parent?.ID,
            Content = content,
            Visibility = visibility,
            CreatedDate = now
        };
        dbContext.TicketComments.Add(comment);
        ticket.UpdatedDate = now;
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = ticket.ID,
            PerformedByUserAccountID = userId,
            ActionType = parent is null ? TicketHistoryActionNames.CommentAdded : "Comment Replied",
            NewValue = visibility.ToString(),
            Description = parent is null
                ? TicketCommentRules.HistoryDescription(visibility)
                : $"A {visibility} reply was added.",
            IsInternal = visibility == CommentVisibility.Private,
            CreatedDate = now
        });
        await AddNotificationsAsync(ticket, userId, visibility, now, token);
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success,
            (await ProjectAsync(ticket, userId, token)).Single(item => item.Id == comment.ID));
    }

    public async Task<TicketServiceResult<TicketCommentDto>> EditAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, int commentId,
        EditTicketCommentRequestDto request, CancellationToken token)
    {
        var content = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > TicketCommentRules.MaximumMessageLength)
            return Invalid("Comment content is required and cannot exceed 5000 characters.");
        var ticket = await FindReadableTicketAsync(
            userId, audience, ticketId, ticketReference, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        var comment = await dbContext.TicketComments.SingleOrDefaultAsync(item =>
            item.ID == commentId && item.TicketID == ticket.ID, token);
        if (comment is null) return new(TicketOperationStatus.NotFound);
        if (comment.AuthorUserAccountID != userId)
            return new(TicketOperationStatus.Forbidden,
                Message: "You can edit only your own comments.");
        if (comment.IsDeleted)
            return new(TicketOperationStatus.Conflict,
                Message: "Deleted comments cannot be edited.");
        if (TicketCommentRules.IsReadOnly(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: "Comments on this ticket are read-only.");
        comment.Content = content;
        comment.IsEdited = true;
        comment.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success,
            (await ProjectAsync(ticket, userId, token)).Single(item => item.Id == comment.ID));
    }

    public async Task<TicketServiceResult<bool>> DeleteAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, int commentId, CancellationToken token)
    {
        var ticket = await FindReadableTicketAsync(
            userId, audience, ticketId, ticketReference, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        var comment = await dbContext.TicketComments.SingleOrDefaultAsync(item =>
            item.ID == commentId && item.TicketID == ticket.ID, token);
        if (comment is null) return new(TicketOperationStatus.NotFound);
        if (comment.AuthorUserAccountID != userId)
            return new(TicketOperationStatus.Forbidden,
                Message: "You can delete only your own comments.");
        if (TicketCommentRules.IsReadOnly(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: "Comments on this ticket are read-only.");
        TicketServiceResult<bool>? result = null;
        async Task ApplyDeletionAsync()
        {
            await dbContext.Entry(comment).ReloadAsync(token);
            if (comment.IsDeleted)
            {
                result = new(TicketOperationStatus.Conflict,
                    Message: "This comment has already been deleted.");
                return;
            }
            var hasReplies = await dbContext.TicketComments.AnyAsync(reply =>
                reply.ParentCommentID == comment.ID && !reply.IsDeleted, token);
            if (hasReplies)
            {
                result = new(TicketOperationStatus.Conflict,
                    Message: "This comment cannot be deleted because it has replies.");
                return;
            }
            comment.IsDeleted = true;
            comment.DeletedDate = DateTime.UtcNow;
            comment.UpdatedDate = comment.DeletedDate;
            await dbContext.SaveChangesAsync(token);
            result = new(TicketOperationStatus.Success, true);
        }

        if (dbContext.Database.IsRelational())
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, token);
                await ApplyDeletionAsync();
                if (result?.Status == TicketOperationStatus.Success)
                    await transaction.CommitAsync(token);
            });
        }
        else
        {
            await ApplyDeletionAsync();
        }
        return result ?? new(TicketOperationStatus.Conflict,
            Message: "The comment could not be deleted.");
    }

    public async Task<TicketServiceResult<CommentAttachmentDto>> UploadAttachmentAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, int commentId, IFormFile file,
        CancellationToken token)
    {
        var ticket = await FindReadableTicketAsync(userId, audience, ticketId,
            ticketReference, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        var comment = await dbContext.TicketComments.SingleOrDefaultAsync(item =>
            item.ID == commentId && item.TicketID == ticket.ID, token);
        if (comment is null) return new(TicketOperationStatus.NotFound);
        if (comment.AuthorUserAccountID != userId)
            return new(TicketOperationStatus.Forbidden,
                Message: "You can attach files only to your own comments.");
        if (comment.IsDeleted)
            return new(TicketOperationStatus.Conflict,
                Message: "Files cannot be attached to a deleted comment.");
        var settings = fileOptions?.Value ?? new FileStorageSettings();
        if (await dbContext.TicketCommentAttachments.CountAsync(item =>
                item.TicketCommentID == commentId, token) >= settings.MaxFilesPerTicket)
            return new(TicketOperationStatus.Invalid,
                Message: $"A comment may have at most {settings.MaxFilesPerTicket} attachments.");
        var validation = ValidateAttachment(file, settings);
        if (validation is not null)
            return new(TicketOperationStatus.Invalid, Message: validation);
        var originalName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalName);
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativePath = Path.Combine("comments", ticket.ID.ToString(),
            commentId.ToString(), storedName);
        var root = Path.GetFullPath(Path.Combine(environment?.ContentRootPath ?? Path.GetTempPath(),
            settings.UploadRoot));
        var physicalPath = Path.GetFullPath(Path.Combine(root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        try
        {
            await using (var stream = new FileStream(physicalPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                await file.CopyToAsync(stream, token);
            var attachment = new TicketCommentAttachment
            {
                TicketCommentID = commentId,
                UploadedByUserAccountID = userId,
                FileName = originalName,
                StoredFileName = storedName,
                FilePath = relativePath,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                UploadedDate = DateTime.UtcNow
            };
            dbContext.TicketCommentAttachments.Add(attachment);
            await dbContext.SaveChangesAsync(token);
            return new(TicketOperationStatus.Success,
                new CommentAttachmentDto(attachment.ID, attachment.FileName,
                    attachment.ContentType, attachment.FileSizeBytes,
                    attachment.UploadedDate));
        }
        catch
        {
            if (File.Exists(physicalPath)) File.Delete(physicalPath);
            throw;
        }
    }

    private static string? ValidateAttachment(IFormFile file, FileStorageSettings settings)
    {
        if (file.Length <= 0) return "The selected file is empty.";
        if (file.Length > settings.MaxFileSizeBytes)
            return "The file exceeds the maximum allowed size.";
        var originalName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalName);
        if (!AllowedAttachmentTypes.TryGetValue(extension, out var contentTypes) ||
            !contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return "This file type is not supported.";
        if (!HasExpectedSignature(file, extension))
            return "The file content does not match its file type.";
        return null;
    }

    private static bool HasExpectedSignature(IFormFile file, string extension)
    {
        var header = new byte[12];
        using var stream = file.OpenReadStream();
        var read = stream.Read(header, 0, header.Length);
        bool Starts(params byte[] signature) =>
            read >= signature.Length && header.AsSpan(0, signature.Length).SequenceEqual(signature);
        return extension.ToLowerInvariant() switch
        {
            ".png" => Starts(0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a),
            ".jpg" or ".jpeg" => Starts(0xff, 0xd8, 0xff),
            ".gif" => Starts(0x47, 0x49, 0x46, 0x38),
            ".webp" => read >= 12 && Starts(0x52, 0x49, 0x46, 0x46) &&
                       header.AsSpan(8, 4).SequenceEqual(new byte[] { 0x57, 0x45, 0x42, 0x50 }),
            ".pdf" => Starts(0x25, 0x50, 0x44, 0x46),
            ".zip" or ".docx" or ".xlsx" => Starts(0x50, 0x4b),
            ".doc" or ".xls" => Starts(0xd0, 0xcf, 0x11, 0xe0),
            ".txt" => !header.AsSpan(0, read).Contains((byte)0),
            _ => false
        };
    }

    private void DeleteStoredFiles(IEnumerable<string> paths, FileStorageSettings settings)
    {
        var root = Path.GetFullPath(Path.Combine(environment?.ContentRootPath ?? Path.GetTempPath(),
            settings.UploadRoot));
        foreach (var relativePath in paths)
        {
            var path = Path.GetFullPath(Path.Combine(root, relativePath));
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                File.Delete(path);
        }
    }

    public async Task<AttachmentDownload?> DownloadAttachmentAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, int commentId, int attachmentId,
        CancellationToken token)
    {
        var ticket = await FindReadableTicketAsync(userId, audience, ticketId,
            ticketReference, token);
        if (ticket is null) return null;
        var canViewPrivate = userId == ticket.CreatedByUserAccountID ||
                             userId == ticket.AssignedToUserAccountID;
        var item = await dbContext.TicketCommentAttachments.AsNoTracking()
            .Where(attachment => attachment.ID == attachmentId &&
                attachment.TicketCommentID == commentId &&
                attachment.TicketComment.TicketID == ticket.ID &&
                (attachment.TicketComment.Visibility == CommentVisibility.Public ||
                 canViewPrivate))
            .Select(attachment => new { attachment.FilePath,
                attachment.ContentType, attachment.FileName })
            .SingleOrDefaultAsync(token);
        if (item is null) return null;
        var settings = fileOptions?.Value ?? new FileStorageSettings();
        var root = Path.GetFullPath(Path.Combine(environment?.ContentRootPath ?? Path.GetTempPath(),
            settings.UploadRoot));
        var path = Path.GetFullPath(Path.Combine(root, item.FilePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path)) return null;
        return new AttachmentDownload(new FileStream(path, FileMode.Open,
            FileAccess.Read, FileShare.Read), item.ContentType, item.FileName);
    }

    private async Task<Ticket?> FindReadableTicketAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, CancellationToken token)
    {
        var query = dbContext.Tickets.Include(ticket => ticket.TicketStatus)
            .Where(ticket => !ticket.IsDeleted &&
                (ticketId == null || ticket.ID == ticketId) &&
                (ticketReference == null || ticket.TicketReferenceNumber == ticketReference));
        query = audience switch
        {
            TicketCommentAudience.Employee => query.Where(ticket =>
                ticket.CreatedByUserAccountID == userId),
            TicketCommentAudience.Agent => query.Where(ticket =>
                ticket.AssignedToUserAccountID == userId ||
                (ticket.TicketStatus.Name == TicketStatusNames.Cancelled &&
                 ticket.CancellationRequests.Any(request =>
                     request.RequestedByAgentUserAccountID == userId &&
                     request.Status == CancellationRequestStatusNames.Approved)) ||
                (ticket.AssignedToUserAccountID == null &&
                 ticket.TicketStatus.Name == TicketStatusNames.Open)),
            _ => query
        };
        return await query.SingleOrDefaultAsync(token);
    }

    private async Task<Ticket?> FindWritableTicketAsync(
        int userId, TicketCommentAudience audience, int? ticketId,
        string? ticketReference, CancellationToken token)
    {
        var ticket = await FindReadableTicketAsync(
            userId, audience, ticketId, ticketReference, token);
        return audience == TicketCommentAudience.Agent &&
               ticket?.AssignedToUserAccountID != userId ? null : ticket;
    }

    private async Task<IReadOnlyCollection<TicketCommentDto>> ProjectAsync(
        Ticket ticket, int viewerId, CancellationToken token)
        => await ProjectQuery(ticket, viewerId).ToListAsync(token);

    private IQueryable<TicketCommentDto> ProjectQuery(
        Ticket ticket, int viewerId, List<int>? rootIds = null)
    {
        var canViewPrivate = viewerId == ticket.CreatedByUserAccountID ||
                             viewerId == ticket.AssignedToUserAccountID;
        var query = dbContext.TicketComments.AsNoTracking()
            .Where(comment => comment.TicketID == ticket.ID &&
                (comment.Visibility == CommentVisibility.Public || canViewPrivate));
        if (rootIds is not null)
            query = query.Where(comment => rootIds.Contains(comment.ID) ||
                (comment.ParentCommentID != null &&
                 rootIds.Contains(comment.ParentCommentID.Value)));
        return query
            .OrderBy(comment => comment.CreatedDate)
            .Select(comment => new TicketCommentDto(
                comment.ID,
                comment.AuthorUserAccount.FirstName + " " +
                    comment.AuthorUserAccount.LastName,
                comment.AuthorUserAccount.UserAccountRoles
                    .Select(role => role.Role.Name!).FirstOrDefault() ?? string.Empty,
                comment.IsDeleted ? "This comment was deleted." : comment.Content,
                comment.CreatedDate, comment.UpdatedDate, comment.IsEdited,
                comment.Visibility.ToString(), comment.ParentCommentID,
                comment.IsDeleted, comment.DeletedDate,
                comment.AuthorUserAccountID == viewerId && !comment.IsDeleted,
                comment.AuthorUserAccountID == viewerId && !comment.IsDeleted &&
                    !dbContext.TicketComments.Any(reply =>
                        reply.ParentCommentID == comment.ID && !reply.IsDeleted),
                comment.AuthorUserAccountID == ticket.CreatedByUserAccountID,
                comment.AuthorUserAccountID == ticket.AssignedToUserAccountID,
                dbContext.TicketComments.Count(reply =>
                    reply.ParentCommentID == comment.ID && !reply.IsDeleted),
                false,
                comment.Attachments.OrderBy(item => item.UploadedDate)
                    .Select(item => new CommentAttachmentDto(item.ID, item.FileName,
                        item.ContentType, item.FileSizeBytes, item.UploadedDate))
                    .ToList()));
    }

    private async Task AddNotificationsAsync(
        Ticket ticket, int authorId, CommentVisibility visibility,
        DateTime now, CancellationToken token)
    {
        var recipients = new HashSet<int> { ticket.CreatedByUserAccountID };
        if (ticket.AssignedToUserAccountID is int assignedId)
            recipients.Add(assignedId);
        if (visibility == CommentVisibility.Public)
        {
            var activeAdministratorIds = await dbContext.UserRoles
                .Where(userRole => userRole.Role.Name == RoleNames.Admin &&
                    userRole.UserAccount.IsActive)
                .Select(userRole => userRole.UserId)
                .ToListAsync(token);
            recipients.UnionWith(activeAdministratorIds);
        }
        recipients.Remove(authorId);

        var authorName = await dbContext.Users.Where(user => user.Id == authorId)
            .Select(user => user.FirstName + " " + user.LastName).SingleAsync(token);
        foreach (var recipient in recipients)
            dbContext.UserNotifications.Add(new UserNotification
            {
                UserAccountID = recipient,
                Type = visibility == CommentVisibility.Public
                    ? NotificationTypeNames.PublicCommentAdded
                    : NotificationTypeNames.PrivateCommentAdded,
                Title = visibility == CommentVisibility.Public
                    ? "New Public Comment" : "New Private Comment",
                Message = $"{authorName} added a {visibility.ToString().ToLowerInvariant()} comment to {ticket.TicketReferenceNumber}.",
                TicketReferenceNumber = ticket.TicketReferenceNumber,
                CreatedDate = now
            });
    }

    private static TicketServiceResult<TicketCommentDto> Invalid(string message) =>
        new(TicketOperationStatus.Invalid, Message: message);
}
