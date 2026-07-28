using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AgentTicketService(ApplicationDbContext dbContext)
    : IAgentTicketService
{
    private static readonly string[] ActiveStatuses =
        [TicketStatusNames.Assigned, TicketStatusNames.InProgress, TicketStatusNames.Pending];
    private static readonly string[] FinishedStatuses =
        [TicketStatusNames.Resolved, TicketStatusNames.Closed, TicketStatusNames.Cancelled];

    public async Task<AgentDashboardDto> GetDashboardAsync(
        int agentId, CancellationToken token)
    {
        var tickets = OwnedTickets(agentId);
        var monthStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);
        var counts = await tickets.GroupBy(_ => 1).Select(group => new
        {
            Active = group.Count(ticket => ActiveStatuses.Contains(ticket.TicketStatus.Name)),
            InProgress = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.InProgress),
            Pending = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.Pending),
            High = group.Count(ticket => ticket.TicketPriority.Name == "High" &&
                !FinishedStatuses.Contains(ticket.TicketStatus.Name)),
            Critical = group.Count(ticket => ticket.TicketPriority.Name == "Critical" &&
                !FinishedStatuses.Contains(ticket.TicketStatus.Name)),
            Resolved = group.Count(ticket => ticket.ResolvedDate >= monthStart &&
                ticket.ResolvedDate < nextMonth)
        }).SingleOrDefaultAsync(token);

        var attention = await ProjectList(tickets
                .Where(ticket =>
                    (ticket.TicketPriority.Name == "High" ||
                     ticket.TicketPriority.Name == "Critical") &&
                    !FinishedStatuses.Contains(ticket.TicketStatus.Name))
                .OrderByDescending(ticket => ticket.TicketPriority.Name == "Critical")
                .ThenBy(ticket => ticket.CreatedDate))
            .Take(3).ToListAsync(token);

        var recent = await ProjectList(tickets
                .OrderByDescending(ticket => ticket.AssignedDate ?? ticket.CreatedDate)
                .ThenByDescending(ticket => ticket.CreatedDate))
            .Take(5).ToListAsync(token);

        return new(
            counts?.Active ?? 0, counts?.InProgress ?? 0, counts?.Pending ?? 0,
            counts?.High ?? 0, counts?.Critical ?? 0, counts?.Resolved ?? 0,
            attention, recent);
    }

    public async Task<PagedResultDto<AgentTicketListItemDto>> GetTicketsAsync(
        int agentId, AgentTicketFilterDto filter, CancellationToken token)
    {
        var query = OwnedTickets(agentId);
        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.ToLower();
            query = query.Where(ticket =>
                ticket.TicketReferenceNumber.ToLower().Contains(normalizedSearch) ||
                ticket.Title.ToLower().Contains(normalizedSearch) ||
                ticket.CreatedByUserAccount.FirstName.ToLower().Contains(normalizedSearch) ||
                ticket.CreatedByUserAccount.LastName.ToLower().Contains(normalizedSearch) ||
                (ticket.CreatedByUserAccount.FirstName + " " +
                 ticket.CreatedByUserAccount.LastName).ToLower().Contains(normalizedSearch));
        }
        if (filter.StatusId.HasValue)
            query = query.Where(ticket => ticket.TicketStatusID == filter.StatusId);
        if (filter.CategoryId.HasValue)
            query = query.Where(ticket => ticket.TicketCategoryID == filter.CategoryId);
        if (filter.PriorityId.HasValue)
            query = query.Where(ticket => ticket.TicketPriorityID == filter.PriorityId);
        if (filter.FromUtc.HasValue)
        {
            var start = filter.FromUtc.Value.UtcDateTime;
            query = query.Where(ticket => ticket.CreatedDate >= start);
        }
        else if (filter.FromDate.HasValue)
        {
            var start = DateTime.SpecifyKind(filter.FromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(ticket => ticket.CreatedDate >= start);
        }
        if (filter.ToUtcExclusive.HasValue)
        {
            var endExclusive = filter.ToUtcExclusive.Value.UtcDateTime;
            query = query.Where(ticket => ticket.CreatedDate < endExclusive);
        }
        else if (filter.ToDate.HasValue)
        {
            var endExclusive = DateTime.SpecifyKind(
                filter.ToDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(ticket => ticket.CreatedDate < endExclusive);
        }

        var totalItems = await query.CountAsync(token);
        query = ApplySorting(query, filter.SortBy, filter.SortDirection);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var items = await ProjectList(query)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
        return new(items, page, pageSize, totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public async Task<AgentTicketDetailsDto?> GetTicketAsync(
        int agentId, string ticketReference, CancellationToken token)
    {
        var details = await ProjectDetails(OwnedTickets(agentId)
                .Where(ticket => ticket.TicketReferenceNumber == ticketReference))
            .SingleOrDefaultAsync(token);
        if (details is null) return null;
        var transitions = await GetAllowedTransitionsAsync(details.StatusName, token);
        return details with
        {
            AllowedStatusTransitions = transitions,
            CanChangeStatus = transitions.Count > 0,
            CanResolve = details.StatusName is TicketStatusNames.InProgress or TicketStatusNames.Pending
        };
    }

    public async Task<TicketServiceResult<AgentTicketDetailsDto>> UpdateStatusAsync(
        int agentId, string ticketReference,
        UpdateAgentTicketStatusRequestDto request, CancellationToken token)
    {
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.TicketReferenceNumber == ticketReference &&
                item.AssignedToUserAccountID == agentId && !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        var target = await dbContext.TicketStatuses.SingleOrDefaultAsync(
            status => status.ID == request.StatusId && status.IsActive, token);
        if (target is null)
            return new(TicketOperationStatus.Invalid, Message: "Select a valid active status.");
        if (!IsTransitionAllowed(ticket.TicketStatus.Name, target.Name))
            return new(TicketOperationStatus.Conflict,
                Message: $"A ticket cannot move from {ticket.TicketStatus.Name} to {target.Name}.");

        var oldStatus = ticket.TicketStatus.Name;
        var now = DateTime.UtcNow;
        ticket.TicketStatusID = target.ID;
        ticket.UpdatedDate = now;
        ticket.AssignedDate ??= now;
        if (target.Name == TicketStatusNames.Resolved)
        {
            ticket.ResolvedDate = now;
            ticket.ResolvedByUserAccountID = agentId;
        }
        else if (oldStatus == TicketStatusNames.Resolved)
        {
            ticket.ResolvedDate = null;
            ticket.ResolvedByUserAccountID = null;
            ticket.ResolutionSummary = null;
        }
        AddHistory(ticket.ID, agentId,
            oldStatus == TicketStatusNames.Resolved
                ? TicketHistoryActionNames.TicketReopened
                : TicketHistoryActionNames.StatusChanged,
            oldStatus, target.Name, NullIfWhiteSpace(request.Reason), false, now);

        var result = await SaveWorkflowAsync(token);
        if (result is not null) return result;
        return new(TicketOperationStatus.Success,
            await GetTicketAsync(agentId, ticketReference, token));
    }

    public async Task<TicketServiceResult<AgentTicketDetailsDto>> ResolveAsync(
        int agentId, string ticketReference,
        ResolveTicketRequestDto request, CancellationToken token)
    {
        var summary = request.ResolutionSummary?.Trim();
        if (summary is null || summary.Length is < 10 or > 5000)
            return new(TicketOperationStatus.Invalid,
                Message: "Resolution summary must be between 10 and 5000 characters.");
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.TicketReferenceNumber == ticketReference &&
                item.AssignedToUserAccountID == agentId && !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (ticket.TicketStatus.Name is not
            (TicketStatusNames.InProgress or TicketStatusNames.Pending))
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket cannot be resolved from its current status.");
        var resolvedStatusId = await dbContext.TicketStatuses
            .Where(status => status.Name == TicketStatusNames.Resolved && status.IsActive)
            .Select(status => status.ID).SingleAsync(token);
        var now = DateTime.UtcNow;
        var oldStatus = ticket.TicketStatus.Name;
        ticket.TicketStatusID = resolvedStatusId;
        ticket.ResolutionSummary = summary;
        ticket.ResolvedByUserAccountID = agentId;
        ticket.ResolvedDate = now;
        ticket.UpdatedDate = now;
        AddHistory(ticket.ID, agentId, TicketHistoryActionNames.TicketResolved,
            oldStatus, TicketStatusNames.Resolved, "Ticket resolved with a resolution summary.",
            false, now);
        var result = await SaveWorkflowAsync(token);
        if (result is not null) return result;
        return new(TicketOperationStatus.Success,
            await GetTicketAsync(agentId, ticketReference, token));
    }

    public async Task<IReadOnlyCollection<TicketCommentDto>?> GetCommentsAsync(
        int agentId, string ticketReference, bool isInternal, CancellationToken token)
    {
        var ticketId = await OwnedTickets(agentId)
            .Where(ticket => ticket.TicketReferenceNumber == ticketReference)
            .Select(ticket => (int?)ticket.ID).SingleOrDefaultAsync(token);
        return ticketId is null ? null : await ProjectComments(ticketId.Value, isInternal)
            .ToListAsync(token);
    }

    public async Task<TicketServiceResult<TicketCommentDto>> AddCommentAsync(
        int agentId, string ticketReference, AddTicketCommentRequestDto request,
        bool isInternal, CancellationToken token)
    {
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 5000)
            return new(TicketOperationStatus.Invalid,
                Message: "Comment content is required and cannot exceed 5000 characters.");
        var ticket = await dbContext.Tickets.SingleOrDefaultAsync(item =>
            item.TicketReferenceNumber == ticketReference &&
            item.AssignedToUserAccountID == agentId && !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (ticket.ClosedDate.HasValue || ticket.CancelledDate.HasValue)
            return new(TicketOperationStatus.Conflict,
                Message: "Comments cannot be added to a closed or cancelled ticket.");
        var now = DateTime.UtcNow;
        var comment = new TicketComment
        {
            TicketID = ticket.ID, AuthorUserAccountID = agentId,
            Content = content, IsInternal = isInternal, CreatedDate = now
        };
        dbContext.TicketComments.Add(comment);
        ticket.UpdatedDate = now;
        AddHistory(ticket.ID, agentId,
            isInternal ? TicketHistoryActionNames.InternalNoteAdded :
                TicketHistoryActionNames.CommentAdded,
            null, null, isInternal ? "An internal note was added." : "A comment was added.",
            isInternal, now);
        await dbContext.SaveChangesAsync(token);
        var authorName = await dbContext.Users.Where(user => user.Id == agentId)
            .Select(user => user.FirstName + " " + user.LastName).SingleAsync(token);
        return new(TicketOperationStatus.Success,
            new(comment.ID, authorName, comment.Content, comment.CreatedDate, null, false));
    }

    public async Task<IReadOnlyCollection<TicketHistoryDto>?> GetHistoryAsync(
        int agentId, string ticketReference, CancellationToken token)
    {
        var ticketId = await OwnedTickets(agentId)
            .Where(ticket => ticket.TicketReferenceNumber == ticketReference)
            .Select(ticket => (int?)ticket.ID).SingleOrDefaultAsync(token);
        return ticketId is null ? null : await ProjectHistory(ticketId.Value, true)
            .ToListAsync(token);
    }

    public async Task<IReadOnlyCollection<TicketCommentDto>?> GetEmployeeCommentsAsync(
        int employeeId, int ticketId, CancellationToken token)
    {
        var ownsTicket = await dbContext.Tickets.AnyAsync(ticket =>
            ticket.ID == ticketId && ticket.CreatedByUserAccountID == employeeId &&
            !ticket.IsDeleted, token);
        return !ownsTicket ? null : await ProjectComments(ticketId, false).ToListAsync(token);
    }

    private IQueryable<Ticket> OwnedTickets(int agentId) =>
        dbContext.Tickets.AsNoTracking().Where(ticket =>
            ticket.AssignedToUserAccountID == agentId && !ticket.IsDeleted);

    private async Task<IReadOnlyCollection<AllowedStatusTransitionDto>>
        GetAllowedTransitionsAsync(string currentStatus, CancellationToken token)
    {
        var names = currentStatus switch
        {
            TicketStatusNames.Assigned => new[] { TicketStatusNames.InProgress },
            TicketStatusNames.InProgress => new[] { TicketStatusNames.Pending, TicketStatusNames.Resolved },
            TicketStatusNames.Pending => new[] { TicketStatusNames.InProgress, TicketStatusNames.Resolved },
            TicketStatusNames.Resolved => new[] { TicketStatusNames.InProgress },
            _ => []
        };
        return await dbContext.TicketStatuses.AsNoTracking()
            .Where(status => status.IsActive && names.Contains(status.Name))
            .OrderBy(status => status.SortOrder)
            .Select(status => new AllowedStatusTransitionDto(status.ID, status.Name))
            .ToListAsync(token);
    }

    private static bool IsTransitionAllowed(string from, string to) =>
        (from, to) switch
        {
            (TicketStatusNames.Assigned, TicketStatusNames.InProgress) => true,
            (TicketStatusNames.InProgress, TicketStatusNames.Pending) => true,
            (TicketStatusNames.InProgress, TicketStatusNames.Resolved) => true,
            (TicketStatusNames.Pending, TicketStatusNames.InProgress) => true,
            (TicketStatusNames.Pending, TicketStatusNames.Resolved) => true,
            (TicketStatusNames.Resolved, TicketStatusNames.InProgress) => true,
            _ => false
        };

    private async Task<TicketServiceResult<AgentTicketDetailsDto>?> SaveWorkflowAsync(
        CancellationToken token)
    {
        try
        {
            await dbContext.SaveChangesAsync(token);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(TicketOperationStatus.Conflict,
                Message: "The ticket changed while this request was being processed. Reload and try again.");
        }
    }

    private void AddHistory(int ticketId, int actorId, string action,
        string? oldValue, string? newValue, string? description,
        bool isInternal, DateTime createdDate) =>
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = ticketId, PerformedByUserAccountID = actorId,
            ActionType = action, OldValue = oldValue, NewValue = newValue,
            Description = description, IsInternal = isInternal, CreatedDate = createdDate
        });

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IQueryable<Ticket> ApplySorting(
        IQueryable<Ticket> query, string? sortBy, string? direction)
    {
        var desc = !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.ToLowerInvariant() switch
        {
            "createddate" => desc ? query.OrderByDescending(x => x.CreatedDate) : query.OrderBy(x => x.CreatedDate),
            "title" => desc ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            "status" => desc ? query.OrderByDescending(x => x.TicketStatus.SortOrder) : query.OrderBy(x => x.TicketStatus.SortOrder),
            "priority" => desc ? query.OrderByDescending(x => x.TicketPriority.SortOrder) : query.OrderBy(x => x.TicketPriority.SortOrder),
            _ => desc
                ? query.OrderByDescending(x => x.AssignedDate ?? x.CreatedDate).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.AssignedDate ?? x.CreatedDate).ThenBy(x => x.CreatedDate)
        };
    }

    private static IQueryable<AgentTicketListItemDto> ProjectList(IQueryable<Ticket> query) =>
        query.Select(ticket => new AgentTicketListItemDto(
            ticket.ID, ticket.TicketReferenceNumber, ticket.Title,
            ticket.CreatedByUserAccount.FirstName + " " + ticket.CreatedByUserAccount.LastName,
            ticket.CreatedByUserAccount.Department == null ? null :
                ticket.CreatedByUserAccount.Department.Name,
            ticket.TicketCategoryID, ticket.TicketCategory.Name,
            ticket.TicketPriorityID, ticket.TicketPriority.Name,
            ticket.TicketStatusID, ticket.TicketStatus.Name,
            ticket.AssignedToUserAccount!.FirstName + " " +
                ticket.AssignedToUserAccount.LastName,
            ticket.CreatedDate, ticket.UpdatedDate, ticket.AssignedDate, ticket.ResolvedDate));

    private static IQueryable<AgentTicketDetailsDto> ProjectDetails(IQueryable<Ticket> query) =>
        query.Select(ticket => new AgentTicketDetailsDto(
            ticket.ID, ticket.TicketReferenceNumber, ticket.Title, ticket.Description,
            ticket.CreatedByUserAccount.FirstName + " " + ticket.CreatedByUserAccount.LastName,
            ticket.CreatedByUserAccount.Email!,
            ticket.CreatedByUserAccount.Department == null ? null :
                ticket.CreatedByUserAccount.Department.Name,
            ticket.TicketCategoryID, ticket.TicketCategory.Name,
            ticket.TicketPriorityID, ticket.TicketPriority.Name,
            ticket.TicketStatusID, ticket.TicketStatus.Name,
            ticket.CreatedDate, ticket.UpdatedDate, ticket.AssignedDate,
            ticket.ResolvedDate, ticket.ClosedDate,
            ticket.AssignedToUserAccount!.FirstName + " " +
                ticket.AssignedToUserAccount.LastName,
            ticket.Attachments.Where(item => !item.IsDeleted)
                .OrderByDescending(item => item.UploadedDate)
                .Select(item => new TicketAttachmentDto(
                    item.ID, item.FileName, item.ContentType, item.FileSizeBytes,
                    item.UploadedDate, false)).ToList(),
            ticket.Comments.Where(item => !item.IsInternal && !item.IsDeleted)
                .OrderBy(item => item.CreatedDate)
                .Select(item => new TicketCommentDto(
                    item.ID, item.AuthorUserAccount.FirstName + " " +
                        item.AuthorUserAccount.LastName,
                    item.Content, item.CreatedDate, item.UpdatedDate, item.IsEdited)).ToList(),
            ticket.Comments.Where(item => item.IsInternal && !item.IsDeleted)
                .OrderBy(item => item.CreatedDate)
                .Select(item => new TicketCommentDto(
                    item.ID, item.AuthorUserAccount.FirstName + " " +
                        item.AuthorUserAccount.LastName,
                    item.Content, item.CreatedDate, item.UpdatedDate, item.IsEdited)).ToList(),
            ticket.History.OrderBy(item => item.CreatedDate)
                .Select(item => new TicketHistoryDto(
                    item.ID, item.ActionType,
                    item.PerformedByUserAccount.FirstName + " " +
                        item.PerformedByUserAccount.LastName,
                    item.OldValue, item.NewValue, item.Description, item.CreatedDate)).ToList(),
            ticket.ResolutionSummary, Array.Empty<AllowedStatusTransitionDto>(),
            false, false, false, false, true, true, false, false));

    private IQueryable<TicketCommentDto> ProjectComments(int ticketId, bool isInternal) =>
        dbContext.TicketComments.AsNoTracking()
            .Where(item => item.TicketID == ticketId &&
                item.IsInternal == isInternal && !item.IsDeleted)
            .OrderBy(item => item.CreatedDate)
            .Select(item => new TicketCommentDto(
                item.ID, item.AuthorUserAccount.FirstName + " " +
                    item.AuthorUserAccount.LastName,
                item.Content, item.CreatedDate, item.UpdatedDate, item.IsEdited));

    private IQueryable<TicketHistoryDto> ProjectHistory(int ticketId, bool includeInternal) =>
        dbContext.TicketHistory.AsNoTracking()
            .Where(item => item.TicketID == ticketId &&
                (includeInternal || !item.IsInternal))
            .OrderBy(item => item.CreatedDate)
            .Select(item => new TicketHistoryDto(
                item.ID, item.ActionType,
                item.PerformedByUserAccount.FirstName + " " +
                    item.PerformedByUserAccount.LastName,
                item.OldValue, item.NewValue, item.Description, item.CreatedDate));
}
