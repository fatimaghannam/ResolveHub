using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class TicketService(ApplicationDbContext dbContext)
    : ITicketService
{
    public async Task<TicketDashboardSummaryDto> GetDashboardAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var tickets = dbContext.Tickets
            .AsNoTracking()
            .Where(ticket =>
                ticket.CreatedByUserAccountID == userId &&
                !ticket.IsDeleted);

        var counts = await tickets
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Open = group.Count(ticket =>
                    ticket.TicketStatus.Name == TicketStatusNames.Open),
                InProgress = group.Count(ticket =>
                    ticket.TicketStatus.Name == TicketStatusNames.InProgress),
                Resolved = group.Count(ticket =>
                    ticket.TicketStatus.Name == TicketStatusNames.Resolved)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var recent = await ProjectListItems(
                tickets.OrderByDescending(ticket => ticket.CreatedDate))
            .Take(5)
            .ToListAsync(cancellationToken);

        return new TicketDashboardSummaryDto(
            counts?.Total ?? 0,
            counts?.Open ?? 0,
            counts?.InProgress ?? 0,
            counts?.Resolved ?? 0,
            recent);
    }

    public async Task<PagedResultDto<TicketListItemDto>> GetTicketsAsync(
        int userId,
        TicketFilterDto filter,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Tickets
            .AsNoTracking()
            .Where(ticket =>
                ticket.CreatedByUserAccountID == userId &&
                !ticket.IsDeleted);

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.ToLower();
            query = query.Where(ticket =>
                ticket.Title.ToLower().Contains(normalizedSearch) ||
                ticket.TicketReferenceNumber.ToLower().Contains(normalizedSearch));
        }

        if (filter.StatusId.HasValue)
            query = query.Where(ticket => ticket.TicketStatusID == filter.StatusId);
        if (filter.CategoryId.HasValue)
            query = query.Where(ticket => ticket.TicketCategoryID == filter.CategoryId);
        if (filter.PriorityId.HasValue)
            query = query.Where(ticket => ticket.TicketPriorityID == filter.PriorityId);

        if (filter.FromUtc.HasValue)
        {
            var fromUtc = filter.FromUtc.Value.UtcDateTime;
            query = query.Where(ticket => ticket.CreatedDate >= fromUtc);
        }

        if (filter.ToUtcExclusive.HasValue)
        {
            var toUtcExclusive = filter.ToUtcExclusive.Value.UtcDateTime;
            query = query.Where(ticket => ticket.CreatedDate < toUtcExclusive);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        query = ApplySorting(query, filter.SortBy, filter.SortDirection);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var items = await ProjectListItems(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<TicketListItemDto>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public Task<TicketDetailsDto?> GetTicketAsync(
        int userId,
        int ticketId,
        CancellationToken cancellationToken)
    {
        return ProjectDetails(
                dbContext.Tickets.AsNoTracking()
                    .Where(ticket =>
                        ticket.ID == ticketId &&
                        ticket.CreatedByUserAccountID == userId &&
                        !ticket.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TicketServiceResult<TicketDetailsDto>> CreateTicketAsync(
        int userId,
        CreateTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateRequestAsync(
            request.Title,
            request.Description,
            request.TicketCategoryId,
            request.TicketPriorityId,
            cancellationToken);
        if (validation is not null)
            return new(TicketOperationStatus.Invalid, Message: validation);

        var openStatusId = await dbContext.TicketStatuses
            .Where(status =>
                status.IsActive &&
                status.Name == TicketStatusNames.Open)
            .Select(status => status.ID)
            .SingleOrDefaultAsync(cancellationToken);

        if (openStatusId == 0)
            throw new InvalidOperationException("The Open ticket status is not configured.");

        IDbContextTransaction? ownedTransaction = null;
        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            ownedTransaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        await using (ownedTransaction)
        {
            try
            {
                var now = DateTime.UtcNow;
                var ticket = new Ticket
                {
                    // The temporary value is never committed or returned. The identity
                    // value below is the concurrency-safe public numeric sequence.
                    TicketReferenceNumber = $"PENDING-{Guid.NewGuid():N}"[..32],
                    CreatedByUserAccountID = userId,
                    TicketCategoryID = request.TicketCategoryId,
                    TicketPriorityID = request.TicketPriorityId,
                    TicketStatusID = openStatusId,
                    Title = request.Title.Trim(),
                    Description = request.Description.Trim(),
                    CreatedDate = now,
                    UpdatedDate = now,
                    IsDeleted = false
                };

                dbContext.Tickets.Add(ticket);
                await dbContext.SaveChangesAsync(cancellationToken);
                ticket.TicketReferenceNumber = $"RH-{now.Year}-{ticket.ID:D4}";
                dbContext.TicketHistory.Add(new TicketHistory
                {
                    TicketID = ticket.ID,
                    PerformedByUserAccountID = userId,
                    ActionType = TicketHistoryActionNames.TicketCreated,
                    NewValue = ticket.TicketReferenceNumber,
                    Description = "Ticket created.",
                    CreatedDate = now
                });
                await dbContext.SaveChangesAsync(cancellationToken);

                var details = await GetTicketAsync(
                        userId, ticket.ID, cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The created ticket could not be loaded.");

                if (ownedTransaction is not null)
                    await ownedTransaction.CommitAsync(cancellationToken);

                return new(TicketOperationStatus.Success, details);
            }
            catch
            {
                if (ownedTransaction is not null)
                    await ownedTransaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }

    public async Task<TicketServiceResult<TicketDetailsDto>> UpdateTicketAsync(
        int userId,
        int ticketId,
        UpdateTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.ID == ticketId &&
                item.CreatedByUserAccountID == userId &&
                !item.IsDeleted,
                cancellationToken);

        if (ticket is null)
            return new(TicketOperationStatus.NotFound);
        if (!CanModify(ticket))
            return new(
                TicketOperationStatus.Conflict,
                Message: "This ticket can no longer be edited because work has already started.");

        var validation = await ValidateRequestAsync(
            request.Title,
            request.Description,
            request.TicketCategoryId,
            request.TicketPriorityId,
            cancellationToken);
        if (validation is not null)
            return new(TicketOperationStatus.Invalid, Message: validation);

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.TicketCategoryID = request.TicketCategoryId;
        ticket.TicketPriorityID = request.TicketPriorityId;
        ticket.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var details = await GetTicketAsync(userId, ticket.ID, cancellationToken)
            ?? throw new InvalidOperationException("The updated ticket could not be loaded.");
        return new(TicketOperationStatus.Success, details);
    }

    public async Task<TicketServiceResult<bool>> CancelTicketAsync(
        int userId,
        int ticketId,
        CancelTicketRequestDto request,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.ID == ticketId &&
                item.CreatedByUserAccountID == userId &&
                !item.IsDeleted,
                cancellationToken);

        if (ticket is null)
            return new(TicketOperationStatus.NotFound);
        if (!CanModify(ticket))
            return new(
                TicketOperationStatus.Conflict,
                Message: "This ticket can no longer be deleted because it has already been assigned or work has started.");

        var reason = request.Reason?.Trim();
        if (reason?.Length > 500)
            return new(TicketOperationStatus.Invalid, Message: "The cancellation reason cannot exceed 500 characters.");

        var now = DateTime.UtcNow;
        var previousStatus = ticket.TicketStatus.Name;
        ticket.TicketStatusID = await dbContext.TicketStatuses
            .Where(status =>
                status.IsActive &&
                status.Name == TicketStatusNames.Cancelled)
            .Select(status => status.ID)
            .SingleAsync(cancellationToken);
        ticket.IsDeleted = true;
        ticket.CancelledDate = now;
        ticket.CancelledReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        ticket.UpdatedDate = now;
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = ticket.ID,
            PerformedByUserAccountID = userId,
            ActionType = TicketHistoryActionNames.TicketCancelled,
            OldValue = previousStatus,
            NewValue = TicketStatusNames.Cancelled,
            Description = string.IsNullOrWhiteSpace(reason)
                ? "Ticket cancelled by its creator."
                : $"Ticket cancelled by its creator. Reason: {reason}",
            CreatedDate = now
        });
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = userId,
            ActionType = TicketHistoryActionNames.TicketCancelled,
            EntityType = "Ticket",
            EntityID = ticket.TicketReferenceNumber,
            Description = "Ticket cancelled by its creator.",
            OldValue = previousStatus,
            NewValue = TicketStatusNames.Cancelled,
            CreatedDate = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(TicketOperationStatus.Success, true);
    }

    public async Task<IReadOnlyCollection<TicketLookupDto>> GetCategoriesAsync(
        CancellationToken cancellationToken) =>
        await dbContext.TicketCategories.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => new TicketLookupDto(item.ID, item.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TicketLookupDto>> GetPrioritiesAsync(
        CancellationToken cancellationToken) =>
        await dbContext.TicketPriorities.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => new TicketLookupDto(item.ID, item.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TicketLookupDto>> GetStatusesAsync(
        CancellationToken cancellationToken) =>
        await dbContext.TicketStatuses.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => new TicketLookupDto(item.ID, item.Name))
            .ToListAsync(cancellationToken);

    private async Task<string?> ValidateRequestAsync(
        string title,
        string description,
        int categoryId,
        int priorityId,
        CancellationToken cancellationToken)
    {
        var trimmedTitle = title.Trim();
        var trimmedDescription = description.Trim();
        if (trimmedTitle.Length is < 5 or > 200)
            return "Title must be between 5 and 200 characters.";
        if (trimmedDescription.Length is < 10 or > 5000)
            return "Description must be between 10 and 5000 characters.";

        var categoryExists = await dbContext.TicketCategories
            .AnyAsync(item => item.ID == categoryId && item.IsActive, cancellationToken);
        if (!categoryExists)
            return "Select a valid active category.";
        var priorityExists = await dbContext.TicketPriorities
            .AnyAsync(item => item.ID == priorityId && item.IsActive, cancellationToken);
        if (!priorityExists)
            return "Select a valid active priority.";

        return null;
    }

    private static bool CanModify(Ticket ticket) =>
        !ticket.IsDeleted &&
        ticket.AssignedToUserAccountID is null &&
        ticket.TicketStatus.Name == TicketStatusNames.Open;

    private static IQueryable<Ticket> ApplySorting(
        IQueryable<Ticket> query,
        string? sortBy,
        string? direction)
    {
        var descending = !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => descending
                ? query.OrderByDescending(ticket => ticket.Title)
                : query.OrderBy(ticket => ticket.Title),
            "status" => descending
                ? query.OrderByDescending(ticket => ticket.TicketStatus.Name)
                : query.OrderBy(ticket => ticket.TicketStatus.Name),
            "priority" => descending
                ? query.OrderByDescending(ticket => ticket.TicketPriority.SortOrder)
                : query.OrderBy(ticket => ticket.TicketPriority.SortOrder),
            _ => descending
                ? query.OrderByDescending(ticket => ticket.CreatedDate)
                : query.OrderBy(ticket => ticket.CreatedDate)
        };
    }

    private static IQueryable<TicketListItemDto> ProjectListItems(
        IQueryable<Ticket> query) =>
        query.Select(ticket => new TicketListItemDto(
            ticket.ID,
            ticket.TicketReferenceNumber,
            ticket.Title,
            ticket.TicketCategory.Name,
            ticket.TicketPriority.Name,
            ticket.TicketStatus.Name,
            ticket.AssignedToUserAccount == null
                ? null
                : ticket.AssignedToUserAccount.FirstName + " " +
                  ticket.AssignedToUserAccount.LastName,
            ticket.CreatedDate,
            ticket.TicketStatus.Name == TicketStatusNames.Open &&
                ticket.AssignedToUserAccountID == null,
            ticket.TicketStatus.Name == TicketStatusNames.Open &&
                ticket.AssignedToUserAccountID == null));

    private static IQueryable<TicketDetailsDto> ProjectDetails(
        IQueryable<Ticket> query) =>
        query.Select(ticket => new TicketDetailsDto(
            ticket.ID,
            ticket.TicketReferenceNumber,
            ticket.Title,
            ticket.Description,
            ticket.TicketCategoryID,
            ticket.TicketCategory.Name,
            ticket.TicketPriorityID,
            ticket.TicketPriority.Name,
            ticket.TicketStatusID,
            ticket.TicketStatus.Name,
            ticket.CreatedByUserAccount.FirstName + " " +
                ticket.CreatedByUserAccount.LastName,
            ticket.AssignedToUserAccount == null
                ? null
                : ticket.AssignedToUserAccount.FirstName + " " +
                  ticket.AssignedToUserAccount.LastName,
            ticket.CreatedDate,
            ticket.UpdatedDate,
            ticket.AssignedDate,
            ticket.ResolvedDate,
            ticket.ClosedDate,
            ticket.CancelledDate,
            ticket.CancelledReason,
            ticket.ResolutionSummary,
            ticket.Attachments
                .Where(attachment => !attachment.IsDeleted)
                .OrderByDescending(attachment => attachment.UploadedDate)
                .Select(attachment => new TicketAttachmentDto(
                    attachment.ID,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.FileSizeBytes,
                    attachment.UploadedDate,
                    ticket.TicketStatus.Name == TicketStatusNames.Open &&
                        ticket.AssignedToUserAccountID == null))
                .ToList(),
            ticket.Comments
                .Where(comment => !comment.IsDeleted)
                .OrderBy(comment => comment.CreatedDate)
                .Select(comment => new TicketCommentDto(
                    comment.ID,
                    comment.AuthorUserAccount.FirstName + " " +
                        comment.AuthorUserAccount.LastName,
                    comment.AuthorUserAccount.UserAccountRoles
                        .Select(assignment => assignment.Role.Name!)
                        .FirstOrDefault() ?? string.Empty,
                    comment.Content,
                    comment.CreatedDate,
                    comment.UpdatedDate,
                    comment.IsEdited,
                    comment.Visibility.ToString()))
                .ToList(),
            ticket.History
                .Where(history => !history.IsInternal)
                .OrderBy(history => history.CreatedDate)
                .Select(history => new TicketHistoryDto(
                    history.ID,
                    history.ActionType,
                    history.PerformedByUserAccount.FirstName + " " +
                        history.PerformedByUserAccount.LastName,
                    history.OldValue,
                    history.NewValue,
                    history.Description,
                    history.CreatedDate))
                .ToList(),
            ticket.TicketStatus.Name == TicketStatusNames.Open &&
                ticket.AssignedToUserAccountID == null,
            ticket.TicketStatus.Name == TicketStatusNames.Open &&
                ticket.AssignedToUserAccountID == null));
}
