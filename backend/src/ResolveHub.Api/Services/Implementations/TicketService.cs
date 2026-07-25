using Microsoft.EntityFrameworkCore;
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
            query = query.Where(ticket =>
                ticket.Title.Contains(search) ||
                ticket.TicketReferenceNumber.Contains(search));
        }

        if (filter.StatusId.HasValue)
            query = query.Where(ticket => ticket.TicketStatusID == filter.StatusId);
        if (filter.CategoryId.HasValue)
            query = query.Where(ticket => ticket.TicketCategoryID == filter.CategoryId);
        if (filter.PriorityId.HasValue)
            query = query.Where(ticket => ticket.TicketPriorityID == filter.PriorityId);

        if (filter.FromDate.HasValue)
        {
            var from = filter.FromDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(ticket => ticket.CreatedDate >= from);
        }

        if (filter.ToDate.HasValue)
        {
            var toExclusive = filter.ToDate.Value
                .AddDays(1)
                .ToDateTime(TimeOnly.MinValue);
            query = query.Where(ticket => ticket.CreatedDate < toExclusive);
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
            request.AssetId,
            userId,
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

        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            TicketReferenceNumber =
                $"RH-{now.Year}-{Guid.NewGuid():N}"[..17].ToUpperInvariant(),
            CreatedByUserAccountID = userId,
            TicketCategoryID = request.TicketCategoryId,
            TicketPriorityID = request.TicketPriorityId,
            TicketStatusID = openStatusId,
            AssetID = request.AssetId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CreatedDate = now,
            UpdatedDate = now,
            IsDeleted = false
        };

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        var details = await GetTicketAsync(userId, ticket.ID, cancellationToken)
            ?? throw new InvalidOperationException("The created ticket could not be loaded.");
        return new(TicketOperationStatus.Success, details);
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
            request.AssetId,
            userId,
            cancellationToken);
        if (validation is not null)
            return new(TicketOperationStatus.Invalid, Message: validation);

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.TicketCategoryID = request.TicketCategoryId;
        ticket.TicketPriorityID = request.TicketPriorityId;
        ticket.AssetID = request.AssetId;
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
        ticket.IsDeleted = true;
        ticket.CancelledDate = now;
        ticket.CancelledReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        ticket.UpdatedDate = now;
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
        int? assetId,
        int userId,
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

        if (assetId.HasValue)
        {
            var userDepartmentId = await dbContext.Users
                .Where(user => user.Id == userId)
                .Select(user => user.DepartmentID)
                .SingleAsync(cancellationToken);
            var assetAllowed = await dbContext.Assets.AnyAsync(
                asset => asset.ID == assetId &&
                    asset.IsActive &&
                    (asset.AssignedToUserAccountID == userId ||
                     (userDepartmentId != null &&
                      asset.AssignedToUserAccountID == null &&
                      asset.DepartmentID == userDepartmentId)),
                cancellationToken);
            if (!assetAllowed)
                return "Select an active asset available to your account.";
        }

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
            ticket.Asset == null
                ? null
                : new AssetLookupDto(
                    ticket.Asset.ID,
                    ticket.Asset.AssetTag,
                    ticket.Asset.AssetName,
                    ticket.Asset.AssetType,
                    ticket.Asset.SerialNumber,
                    ticket.Asset.Location),
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
            ticket.TicketStatus.Name == TicketStatusNames.Open &&
                ticket.AssignedToUserAccountID == null,
            ticket.TicketStatus.Name == TicketStatusNames.Open &&
                ticket.AssignedToUserAccountID == null));
}
