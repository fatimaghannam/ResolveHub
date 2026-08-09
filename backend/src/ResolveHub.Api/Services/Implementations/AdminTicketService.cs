using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Infrastructure;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AdminTicketService(
    ApplicationDbContext dbContext,
    ILogger<AdminTicketService> logger,
    INotificationService notificationService)
    : IAdminTicketService
{
    public async Task<AdminAssignmentOverviewDto> GetAssignmentsAsync(
        AdminTicketFilterDto filter, CancellationToken token) =>
        new(
            await GetUnassignedTickets(filter).ToListAsync(token),
            await GetAgentWorkloadsAsync(token));

    public async Task<AdminDashboardSummaryDto> GetDashboardAsync(
        CancellationToken token)
    {
        var monthStart = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);
        var tickets = dbContext.Tickets.AsNoTracking()
            .Where(ticket => !ticket.IsDeleted);
        var counts = await tickets.GroupBy(_ => 1).Select(group => new
        {
            Total = group.Count(),
            Open = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.Open),
            InProgress = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.InProgress),
            Unassigned = group.Count(ticket =>
                ticket.AssignedToUserAccountID == null &&
                !ticket.TicketStatus.IsFinalStatus),
            Resolved = group.Count(ticket =>
                ticket.ResolvedDate >= monthStart && ticket.ResolvedDate < nextMonth)
        }).SingleOrDefaultAsync(token);

        var statusRows = await tickets
            .GroupBy(ticket => new
            {
                ticket.TicketStatusID,
                ticket.TicketStatus.Name,
                ticket.TicketStatus.SortOrder
            })
            .Select(group => new
            {
                group.Key.Name,
                Count = group.Count(),
                group.Key.SortOrder
            })
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(token);
        var statusCounts = statusRows
            .Select(item => new AdminChartItemDto(item.Name, item.Count))
            .ToList();

        var categoryRows = await tickets
            .GroupBy(ticket => new
            {
                ticket.TicketCategoryID,
                ticket.TicketCategory.Name,
                ticket.TicketCategory.SortOrder
            })
            .Select(group => new
            {
                group.Key.Name,
                Count = group.Count(),
                group.Key.SortOrder
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(token);
        var categoryCounts = categoryRows
            .Select(item => new AdminChartItemDto(item.Name, item.Count))
            .ToList();
        var trendStart = monthStart.AddMonths(-5);
        var monthlyRows = await tickets
            .Where(ticket => ticket.CreatedDate >= trendStart ||
                ticket.ResolvedDate >= trendStart)
            .Select(ticket => new { ticket.CreatedDate, ticket.ResolvedDate })
            .ToListAsync(token);
        var monthlyTrend = Enumerable.Range(0, 6).Select(offset =>
        {
            var start = trendStart.AddMonths(offset);
            var end = start.AddMonths(1);
            return new AdminMonthlyTrendDto(
                start.ToString("MMM"),
                monthlyRows.Count(item => item.CreatedDate >= start && item.CreatedDate < end),
                monthlyRows.Count(item => item.ResolvedDate >= start && item.ResolvedDate < end));
        }).ToList();

        return new(
            await dbContext.Users.CountAsync(user => user.IsActive, token),
            counts?.Total ?? 0,
            counts?.Open ?? 0,
            counts?.InProgress ?? 0,
            counts?.Unassigned ?? 0,
            counts?.Resolved ?? 0,
            statusCounts,
            monthlyTrend,
            categoryCounts,
            await GetUnassignedTickets(new AdminTicketFilterDto())
                .Take(5).ToListAsync(token),
            await GetAgentWorkloadsAsync(token));
    }

    public Task<IReadOnlyCollection<AdminAgentWorkloadDto>> GetAgentsAsync(
        CancellationToken token) => GetAgentWorkloadsAsync(token);

    public async Task<PagedResultDto<AdminTicketListItemDto>> GetTicketsAsync(
        AdminTicketFilterDto filter, CancellationToken token)
    {
        var query = dbContext.Tickets.AsNoTracking()
            .Where(ticket => !ticket.IsDeleted);
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var normalizedSearch = search.ToLower();
            query = query.Where(ticket =>
                ticket.TicketReferenceNumber.ToLower().Contains(normalizedSearch) ||
                ticket.Title.ToLower().Contains(normalizedSearch) ||
                ticket.CreatedByUserAccount.FirstName.ToLower().Contains(normalizedSearch) ||
                ticket.CreatedByUserAccount.LastName.ToLower().Contains(normalizedSearch) ||
                (ticket.CreatedByUserAccount.FirstName + " " +
                 ticket.CreatedByUserAccount.LastName).ToLower().Contains(normalizedSearch) ||
                (ticket.AssignedToUserAccount != null &&
                 (ticket.AssignedToUserAccount.FirstName.ToLower().Contains(normalizedSearch) ||
                  ticket.AssignedToUserAccount.LastName.ToLower().Contains(normalizedSearch) ||
                  (ticket.AssignedToUserAccount.FirstName + " " +
                   ticket.AssignedToUserAccount.LastName).ToLower()
                      .Contains(normalizedSearch))));
        }
        if (filter.StatusId.HasValue)
            query = query.Where(ticket => ticket.TicketStatusID == filter.StatusId);
        if (filter.CategoryId.HasValue)
            query = query.Where(ticket => ticket.TicketCategoryID == filter.CategoryId);
        if (filter.PriorityId.HasValue)
            query = query.Where(ticket => ticket.TicketPriorityID == filter.PriorityId);
        if (filter.AgentUserId.HasValue)
            query = query.Where(ticket => ticket.AssignedToUserAccountID == filter.AgentUserId);
        if (filter.RequesterId.HasValue)
            query = query.Where(ticket => ticket.CreatedByUserAccountID == filter.RequesterId);
        if (filter.UnassignedOnly == true)
            query = query.Where(ticket => ticket.AssignedToUserAccountID == null);
        if (filter.AssignedOnly == true)
            query = query.Where(ticket => ticket.AssignedToUserAccountID != null);
        if (filter.ActiveWorkloadOnly == true)
            query = query.Where(ticket =>
                TicketWorkloadRules.ActiveStatuses.Contains(ticket.TicketStatus.Name));
        query = query.ApplyUtcDateRange(filter.FromUtc,
            filter.ToUtcExclusive, ticket => ticket.CreatedDate);
        if (!filter.FromUtc.HasValue && filter.FromDate.HasValue)
            query = query.Where(ticket => ticket.CreatedDate >= filter.FromDate.Value.Date);
        if (!filter.ToUtcExclusive.HasValue && filter.ToDate.HasValue)
        {
            var end = filter.ToDate.Value.Date.AddDays(1);
            query = query.Where(ticket => ticket.CreatedDate < end);
        }

        var total = await query.CountAsync(token);
        query = ApplySorting(query, filter.SortBy, filter.SortDirection);
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(ticket => new AdminTicketListItemDto(
                ticket.ID, ticket.TicketReferenceNumber, ticket.Title,
                ticket.CreatedByUserAccountID,
                ticket.CreatedByUserAccount.FirstName + " " + ticket.CreatedByUserAccount.LastName,
                ticket.TicketCategoryID, ticket.TicketCategory.Name,
                ticket.TicketPriorityID, ticket.TicketPriority.Name,
                ticket.TicketStatusID, ticket.TicketStatus.Name,
                ticket.AssignedToUserAccountID,
                ticket.AssignedToUserAccount == null ? null :
                    ticket.AssignedToUserAccount.FirstName + " " + ticket.AssignedToUserAccount.LastName,
                ticket.CreatedDate, ticket.UpdatedDate,
                ticket.OriginalTicket == null ? null :
                    ticket.OriginalTicket.TicketReferenceNumber))
            .ToListAsync(token);
        return new(items, filter.Page, filter.PageSize, total,
            Math.Max(1, (int)Math.Ceiling(total / (double)filter.PageSize)));
    }

    private static IQueryable<Ticket> ApplySorting(
        IQueryable<Ticket> query, string? sortBy, string? direction)
    {
        var descending =
            !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => descending
                ? query.OrderByDescending(ticket => ticket.Title)
                    .ThenByDescending(ticket => ticket.ID)
                : query.OrderBy(ticket => ticket.Title).ThenBy(ticket => ticket.ID),
            "status" => descending
                ? query.OrderByDescending(ticket => ticket.TicketStatus.SortOrder)
                    .ThenByDescending(ticket => ticket.ID)
                : query.OrderBy(ticket => ticket.TicketStatus.SortOrder)
                    .ThenBy(ticket => ticket.ID),
            "priority" => descending
                ? query.OrderByDescending(ticket => ticket.TicketPriority.SortOrder)
                    .ThenByDescending(ticket => ticket.ID)
                : query.OrderBy(ticket => ticket.TicketPriority.SortOrder)
                    .ThenBy(ticket => ticket.ID),
            _ => descending
                ? query.OrderByDescending(ticket => ticket.CreatedDate)
                    .ThenByDescending(ticket => ticket.ID)
                : query.OrderBy(ticket => ticket.CreatedDate).ThenBy(ticket => ticket.ID)
        };
    }

    public Task<AdminTicketDetailsDto?> GetTicketAsync(
        string ticketReference, CancellationToken token) =>
        dbContext.Tickets.AsNoTracking()
            .Where(ticket => ticket.TicketReferenceNumber == ticketReference && !ticket.IsDeleted)
            .Select(ticket => new AdminTicketDetailsDto(
                ticket.ID, ticket.TicketReferenceNumber, ticket.Title, ticket.Description,
                ticket.CreatedByUserAccountID,
                ticket.CreatedByUserAccount.FirstName + " " + ticket.CreatedByUserAccount.LastName,
                ticket.CreatedByUserAccount.Email!,
                ticket.TicketCategoryID, ticket.TicketCategory.Name,
                ticket.TicketPriorityID, ticket.TicketPriority.Name,
                ticket.TicketStatusID, ticket.TicketStatus.Name,
                ticket.AssignedToUserAccountID,
                ticket.AssignedToUserAccount == null ? null :
                    ticket.AssignedToUserAccount.FirstName + " " + ticket.AssignedToUserAccount.LastName,
                ticket.CreatedDate, ticket.UpdatedDate, ticket.AssignedDate,
                ticket.ResolvedDate, ticket.ClosedDate,
                ticket.Attachments.Where(item => !item.IsDeleted).Select(item =>
                    new TicketAttachmentDto(item.ID, item.FileName, item.ContentType,
                        item.FileSizeBytes, item.UploadedDate, false)).ToList(),
                ticket.Comments.Where(item => !item.IsDeleted &&
                        item.Visibility == CommentVisibility.Public)
                    .OrderBy(item => item.CreatedDate).Select(item =>
                    new TicketCommentDto(item.ID,
                        item.AuthorUserAccount.FirstName + " " + item.AuthorUserAccount.LastName,
                        item.AuthorUserAccount.UserAccountRoles
                            .Select(assignment => assignment.Role.Name!)
                            .FirstOrDefault() ?? string.Empty,
                        item.Content, item.CreatedDate, item.UpdatedDate, item.IsEdited,
                        item.Visibility.ToString())).ToList(),
                ticket.History.OrderByDescending(item => item.CreatedDate).Select(item =>
                    new TicketHistoryDto(item.ID, item.ActionType,
                        item.PerformedByUserAccount.FirstName + " " +
                        item.PerformedByUserAccount.LastName,
                        item.OldValue, item.NewValue, item.Description, item.CreatedDate)).ToList(),
                ticket.OriginalTicketID,
                ticket.OriginalTicket == null ? null :
                    ticket.OriginalTicket.TicketReferenceNumber,
                ticket.OriginalTicket == null ? null : ticket.OriginalTicket.Title,
                ticket.DuplicateReviews
                    .Where(review => review.Status == DuplicateReviewStatusNames.Approved)
                    .OrderByDescending(review => review.ReviewedDate)
                    .Select(review => review.ReviewedDate).FirstOrDefault(),
                ticket.DuplicateReviews
                    .Where(review => review.Status == DuplicateReviewStatusNames.Approved)
                    .OrderByDescending(review => review.ReviewedDate)
                    .Select(review => review.ReviewedByUserAccount == null ? null :
                        review.ReviewedByUserAccount.FirstName + " " +
                        review.ReviewedByUserAccount.LastName).FirstOrDefault(),
                ticket.DuplicateReviews
                    .Where(review => review.Status == DuplicateReviewStatusNames.Pending)
                    .OrderByDescending(review => review.CreatedDate)
                    .Select(review => new DuplicateReviewDto(
                        review.ID, ticket.TicketReferenceNumber,
                        ticket.Title, ticket.TicketStatus.Name,
                        ticket.TicketPriority.Name, ticket.CreatedDate,
                        ticket.CreatedByUserAccount.FirstName + " " +
                            ticket.CreatedByUserAccount.LastName,
                        ticket.TicketCategory.Name,
                        review.SuggestedOriginalTicket.TicketReferenceNumber,
                        review.SuggestedOriginalTicket.Title,
                        review.SuggestedOriginalTicket.TicketStatus.Name,
                        review.SuggestedOriginalTicket.TicketPriority.Name,
                        review.SuggestedOriginalTicket.CreatedDate,
                        review.SuggestedOriginalTicket.CreatedByUserAccount.FirstName + " " +
                            review.SuggestedOriginalTicket.CreatedByUserAccount.LastName,
                        review.SuggestedOriginalTicket.TicketCategory.Name,
                        review.ReportedByUserAccount.FirstName + " " +
                            review.ReportedByUserAccount.LastName,
                        review.ReportedByUserAccount.UserAccountRoles.Any(role =>
                            role.Role.Name == RoleNames.Admin),
                        review.Reason, review.Status, review.CreatedDate))
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(token);

    public async Task<TicketServiceResult<bool>> AssignAsync(
        int administratorId,
        string ticketReference,
        int? agentUserId,
        CancellationToken token,
        int? preservedAgentRequestId = null)
    {
        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null)
        {
            transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                token);
        }

        async Task<TicketServiceResult<bool>> FailureAsync(
            TicketOperationStatus status,
            string? message = null)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(token);
            return new(status, Message: message);
        }

        try
        {
            var ticket = await dbContext.Tickets
                .Include(item => item.TicketStatus)
                .SingleOrDefaultAsync(item =>
                    item.TicketReferenceNumber == ticketReference &&
                    !item.IsDeleted,
                    token);
            if (ticket is null)
                return await FailureAsync(TicketOperationStatus.NotFound);
            if (DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name))
                return await FailureAsync(TicketOperationStatus.Conflict,
                    DuplicateTicketRules.ReadOnlyMessage);
            if (ticket.TicketStatus.IsFinalStatus)
                return await FailureAsync(
                    TicketOperationStatus.Conflict,
                    "Completed tickets cannot be assigned or reassigned.");
            if (ticket.AssignedToUserAccountID == agentUserId)
            {
                return await FailureAsync(
                    TicketOperationStatus.Conflict,
                    agentUserId.HasValue
                        ? "This ticket is already assigned to that agent."
                        : "This ticket is already unassigned.");
            }

            if (agentUserId.HasValue)
            {
                var agentIsEligible = await (
                    from user in dbContext.Users
                    join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
                    join role in dbContext.Roles on userRole.RoleId equals role.Id
                    where user.Id == agentUserId.Value &&
                          user.IsActive &&
                          role.Name == RoleNames.ITSupportAgent
                    select user.Id).AnyAsync(token);
                if (!agentIsEligible)
                {
                    return await FailureAsync(
                        TicketOperationStatus.Invalid,
                        "Select an active IT Support Agent.");
                }

                var activeTickets = await dbContext.Tickets.CountAsync(
                    item =>
                        !item.IsDeleted &&
                        item.AssignedToUserAccountID == agentUserId.Value &&
                        TicketWorkloadRules.ActiveStatuses.Contains(
                            item.TicketStatus.Name),
                    token);
                if (activeTickets >=
                    TicketWorkloadRules.MaxActiveTicketsPerAgent)
                {
                    return await FailureAsync(
                        TicketOperationStatus.Conflict,
                        $"This IT Agent has reached the maximum workload of {TicketWorkloadRules.MaxActiveTicketsPerAgent} active tickets.");
                }
            }

            var targetStatusName = agentUserId.HasValue
                ? TicketStatusNames.Assigned : TicketStatusNames.Open;
            var targetStatusId = await dbContext.TicketStatuses
                .Where(status =>
                    status.IsActive &&
                    status.Name == targetStatusName)
                .Select(status => status.ID)
                .SingleAsync(token);
            var now = DateTime.UtcNow;
            var previousAgentId = ticket.AssignedToUserAccountID;
            var assignmentNames = await dbContext.Users.AsNoTracking()
                .Where(user => user.Id == previousAgentId || user.Id == agentUserId)
                .ToDictionaryAsync(user => user.Id,
                    user => user.FirstName + " " + user.LastName, token);
            var previousAgentName = previousAgentId.HasValue &&
                assignmentNames.TryGetValue(previousAgentId.Value, out var previousName)
                    ? previousName : null;
            var assignedAgentName = agentUserId.HasValue &&
                assignmentNames.TryGetValue(agentUserId.Value, out var assignedName)
                    ? assignedName : null;
            if (previousAgentId.HasValue)
            {
                var openSession = await dbContext.TicketWorkSessions.SingleOrDefaultAsync(
                    item => item.TicketID == ticket.ID && item.EndedAt == null, token);
                if (openSession is not null)
                {
                    openSession.EndedAt = now;
                    openSession.DurationMinutes = Math.Max(0,
                        (int)Math.Floor((now - openSession.StartedAt).TotalMinutes));
                    openSession.EndedReason = "Reassigned";
                }
            }
            ticket.AssignedToUserAccountID = agentUserId;
            if (ticket.TicketStatus.Name is
                TicketStatusNames.Open or TicketStatusNames.Assigned)
            {
                ticket.TicketStatusID = targetStatusId;
            }
            ticket.AssignedDate = agentUserId.HasValue ? now : null;
            ticket.UpdatedDate = now;
            dbContext.TicketHistory.Add(new TicketHistory
            {
                TicketID = ticket.ID,
                PerformedByUserAccountID = administratorId,
                ActionType = agentUserId.HasValue
                    ? previousAgentId.HasValue
                        ? TicketHistoryActionNames.TicketReassigned
                        : TicketHistoryActionNames.TicketAssigned
                    : "Ticket Unassigned",
                OldValue = previousAgentName,
                NewValue = assignedAgentName,
                Description =
                    previousAgentId.HasValue && agentUserId.HasValue
                        ? $"Ticket reassigned to {assignedAgentName}."
                        : agentUserId.HasValue
                            ? $"Ticket assigned to {assignedAgentName}."
                            : "Ticket assignment removed.",
                CreatedDate = now
            });
            dbContext.ActivityLogs.Add(new ActivityLog
            {
                PerformedByUserAccountID = administratorId,
                ActionType = previousAgentId.HasValue
                    ? "Ticket Reassigned"
                    : "Ticket Assigned",
                EntityType = "Ticket",
                EntityID = ticket.TicketReferenceNumber,
                Description = previousAgentId.HasValue
                    ? "Ticket assignment changed to another IT Support Agent."
                    : "Ticket assigned to an IT Support Agent.",
                OldValue = previousAgentId?.ToString(),
                NewValue = agentUserId?.ToString(),
                CreatedDate = now
            });

            if (agentUserId.HasValue)
            {
                var type = previousAgentId.HasValue
                    ? NotificationTypeNames.TicketReassigned
                    : NotificationTypeNames.TicketAssigned;
                notificationService.Add(agentUserId.Value, type,
                    previousAgentId.HasValue ? "Ticket Reassigned" : "Ticket Assigned",
                    $"{ticket.TicketReferenceNumber} has been assigned to you.",
                    ticket.TicketReferenceNumber, now, administratorId);
                notificationService.Add(ticket.CreatedByUserAccountID, type,
                    previousAgentId.HasValue ? "Ticket reassigned" : "Ticket assigned",
                    $"{ticket.TicketReferenceNumber} has been assigned to {assignedAgentName}.",
                    ticket.TicketReferenceNumber, now, administratorId);
            }
            if (previousAgentId.HasValue && previousAgentId != agentUserId)
                notificationService.Add(previousAgentId.Value,
                    NotificationTypeNames.TicketReassigned, "Ticket reassigned",
                    $"{ticket.TicketReferenceNumber} is no longer assigned to you.",
                    ticket.TicketReferenceNumber, now, administratorId);

            if (agentUserId.HasValue)
            {
                var obsoleteAgentRequests = await dbContext.TicketAssignmentRequests
                    .Where(item => item.TicketID == ticket.ID &&
                        item.RequestedAgentUserAccountID == null &&
                        item.Status == AssignmentRequestStatusNames.Pending &&
                        item.ID != preservedAgentRequestId)
                    .ToListAsync(token);
                dbContext.TicketAssignmentRequests.RemoveRange(obsoleteAgentRequests);
            }

            await dbContext.SaveChangesAsync(token);
            if (transaction is not null)
                await transaction.CommitAsync(token);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(token);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
        return new(TicketOperationStatus.Success, true);
    }

    public async Task<IReadOnlyCollection<DuplicateReviewDto>>
        GetPendingDuplicateReviewsAsync(CancellationToken token) =>
        await dbContext.DuplicateReviews.AsNoTracking()
            .Where(review => review.Status == DuplicateReviewStatusNames.Pending)
            .OrderBy(review => review.CreatedDate)
            .Select(review => new DuplicateReviewDto(
                review.ID, review.Ticket.TicketReferenceNumber,
                review.Ticket.Title, review.Ticket.TicketStatus.Name,
                review.Ticket.TicketPriority.Name, review.Ticket.CreatedDate,
                review.Ticket.CreatedByUserAccount.FirstName + " " +
                    review.Ticket.CreatedByUserAccount.LastName,
                review.Ticket.TicketCategory.Name,
                review.SuggestedOriginalTicket.TicketReferenceNumber,
                review.SuggestedOriginalTicket.Title,
                review.SuggestedOriginalTicket.TicketStatus.Name,
                review.SuggestedOriginalTicket.TicketPriority.Name,
                review.SuggestedOriginalTicket.CreatedDate,
                review.SuggestedOriginalTicket.CreatedByUserAccount.FirstName + " " +
                    review.SuggestedOriginalTicket.CreatedByUserAccount.LastName,
                review.SuggestedOriginalTicket.TicketCategory.Name,
                review.ReportedByUserAccount.FirstName + " " +
                    review.ReportedByUserAccount.LastName,
                review.ReportedByUserAccount.UserAccountRoles.Any(role =>
                    role.Role.Name == RoleNames.Admin),
                review.Reason, review.Status, review.CreatedDate))
            .ToListAsync(token);

    public async Task<TicketServiceResult<bool>> ReviewDuplicateAsync(
        int administratorId, int reviewId, bool approve,
        string? internalNote, CancellationToken token)
    {
        var review = await dbContext.DuplicateReviews
            .Include(item => item.Ticket).ThenInclude(ticket => ticket.TicketStatus)
            .Include(item => item.SuggestedOriginalTicket)
            .SingleOrDefaultAsync(item => item.ID == reviewId, token);
        if (review is null) return new(TicketOperationStatus.NotFound);
        if (review.Status != DuplicateReviewStatusNames.Pending)
            return new(TicketOperationStatus.Conflict,
                Message: "This duplicate review has already been completed.");

        if (DuplicateTicketRules.IsDuplicate(review.Ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket has already been marked as Duplicate.");

        var now = DateTime.UtcNow;
        var administratorName = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == administratorId)
            .Select(user => user.FirstName + " " + user.LastName)
            .SingleAsync(token);
        var reviewNote = string.IsNullOrWhiteSpace(internalNote)
            ? null : internalNote.Trim();
        if (approve)
        {
            await ApplyDuplicateApprovalAsync(
                review, administratorId, administratorName, now, token);
        }
        if (reviewNote is not null)
        {
            dbContext.TicketHistory.Add(new TicketHistory
            {
                TicketID = review.TicketID,
                PerformedByUserAccountID = administratorId,
                ActionType = "Duplicate Review Note",
                Description = reviewNote,
                IsInternal = true,
                CreatedDate = now
            });
        }
        else if (!approve)
        {
            review.Status = DuplicateReviewStatusNames.Rejected;
            review.ReviewedByUserAccountID = administratorId;
            review.ReviewedDate = now;
            dbContext.TicketHistory.Add(new TicketHistory
            {
                TicketID = review.TicketID,
                PerformedByUserAccountID = administratorId,
                ActionType = TicketHistoryActionNames.DuplicateReviewRejected,
                OldValue = DuplicateReviewStatusNames.Pending,
                NewValue = review.Status,
                Description = $"{administratorName} rejected the duplicate review for {review.Ticket.TicketReferenceNumber}, reported as a possible duplicate of {review.SuggestedOriginalTicket.TicketReferenceNumber}.",
                CreatedDate = now
            });
            dbContext.ActivityLogs.Add(new ActivityLog
            {
                PerformedByUserAccountID = administratorId,
                ActionType = TicketHistoryActionNames.DuplicateReviewRejected,
                EntityType = "Ticket",
                EntityID = review.Ticket.TicketReferenceNumber,
                Description = $"Administrator {administratorName} rejected the duplicate review for {review.Ticket.TicketReferenceNumber}, suggested original {review.SuggestedOriginalTicket.TicketReferenceNumber}.",
                OldValue = DuplicateReviewStatusNames.Pending,
                NewValue = review.Status,
                CreatedDate = now
            });
        }
        dbContext.UserNotifications.Add(new UserNotification
        {
            UserAccountID = review.ReportedByUserAccountID,
            Type = approve ? NotificationTypeNames.DuplicateReportApproved : NotificationTypeNames.DuplicateReportRejected,
            Title = approve ? "Duplicate Review Approved" : "Duplicate Report Rejected",
            Message = approve
                ? $"{review.Ticket.TicketReferenceNumber} was marked as a duplicate of {review.SuggestedOriginalTicket.TicketReferenceNumber}."
                : $"The duplicate report for {review.Ticket.TicketReferenceNumber} was rejected.",
            TicketReferenceNumber = review.Ticket.TicketReferenceNumber,
            CreatedDate = now
        });
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success, true);
    }

    public async Task<TicketServiceResult<bool>> MarkDuplicateAsync(
        int administratorId, string ticketReference,
        MarkDuplicateRequestDto request, CancellationToken token)
    {
        if (!request.Confirmed)
            return new(TicketOperationStatus.Invalid,
                Message: "Confirm that this ticket should be marked as Duplicate.");
        var reportedReference = ticketReference.Trim();
        var originalReference = request.OriginalTicketReference.Trim();
        logger.LogInformation(
            "Direct duplicate lookup received reported reference {ReportedTicketReference} and original reference {OriginalTicketReference}; both are queried against Ticket.TicketReferenceNumber.",
            reportedReference, originalReference);
        if (string.Equals(reportedReference, originalReference,
                StringComparison.OrdinalIgnoreCase))
            return new(TicketOperationStatus.Invalid,
                Message: "A ticket cannot be reported as a duplicate of itself.");

        var normalizedReportedReference = reportedReference.ToUpperInvariant();
        var duplicate = await dbContext.Tickets
            .Include(ticket => ticket.TicketStatus)
            .SingleOrDefaultAsync(ticket => !ticket.IsDeleted &&
                ticket.TicketReferenceNumber.ToUpper() ==
                    normalizedReportedReference, token);
        if (duplicate is null)
        {
            logger.LogWarning(
                "Direct duplicate lookup could not find reported ticket {ReportedTicketReference} in Ticket.TicketReferenceNumber.",
                reportedReference);
            return new(TicketOperationStatus.NotFound,
                Message: "The reported ticket could not be found.");
        }

        var normalizedOriginalReference = originalReference.ToUpperInvariant();
        var original = await dbContext.Tickets.SingleOrDefaultAsync(ticket =>
            !ticket.IsDeleted && ticket.TicketReferenceNumber.ToUpper() ==
                normalizedOriginalReference, token);
        if (original is null)
        {
            logger.LogWarning(
                "Direct duplicate lookup found reported ticket {ReportedTicketReference}, but could not find original ticket {OriginalTicketReference} in Ticket.TicketReferenceNumber.",
                duplicate.TicketReferenceNumber, originalReference);
            return new(TicketOperationStatus.NotFound,
                Message: "The original ticket could not be found.");
        }
        logger.LogInformation(
            "Direct duplicate lookup resolved reported ticket {ReportedTicketReference} (ID {ReportedTicketId}) and original ticket {OriginalTicketReference} (ID {OriginalTicketId}).",
            duplicate.TicketReferenceNumber, duplicate.ID,
            original.TicketReferenceNumber, original.ID);
        if (DuplicateTicketRules.IsDuplicate(duplicate.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket has already been marked as Duplicate.");

        var pendingReviews = await dbContext.DuplicateReviews
            .Include(review => review.ReportedByUserAccount)
                .ThenInclude(user => user.UserAccountRoles)
                .ThenInclude(role => role.Role)
            .Where(review => review.TicketID == duplicate.ID &&
                review.Status == DuplicateReviewStatusNames.Pending)
            .ToListAsync(token);
        if (pendingReviews.Any(review =>
                !review.ReportedByUserAccount.UserAccountRoles.Any(role =>
                    role.Role.Name == RoleNames.Admin)))
            return new(TicketOperationStatus.Conflict,
                Message: "A Manager duplicate review is already pending for this ticket.");
        if (pendingReviews.Count > 0)
            dbContext.DuplicateReviews.RemoveRange(pendingReviews);

        var administratorName = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == administratorId)
            .Select(user => user.FirstName + " " + user.LastName)
            .SingleAsync(token);
        var now = DateTime.UtcNow;
        await ApplyDuplicateStateAsync(
            duplicate, original.ID, now, token);
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = duplicate.ID,
            PerformedByUserAccountID = administratorId,
            ActionType = TicketHistoryActionNames.DuplicateMarked,
            NewValue = original.TicketReferenceNumber,
            Description = $"{administratorName} marked {duplicate.TicketReferenceNumber} as a duplicate of {original.TicketReferenceNumber}.",
            CreatedDate = now
        });
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? null : request.Reason.Trim();
        if (reason is not null)
        {
            dbContext.TicketHistory.Add(new TicketHistory
            {
                TicketID = duplicate.ID,
                PerformedByUserAccountID = administratorId,
                ActionType = "Duplicate Marking Note",
                Description = reason,
                IsInternal = true,
                CreatedDate = now
            });
        }
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = administratorId,
            ActionType = TicketHistoryActionNames.DuplicateMarked,
            EntityType = "Ticket",
            EntityID = duplicate.TicketReferenceNumber,
            Description = $"Administrator {administratorName} marked {duplicate.TicketReferenceNumber} as a duplicate of {original.TicketReferenceNumber}.",
            NewValue = original.TicketReferenceNumber,
            CreatedDate = now
        });
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success, true);
    }

    private async Task ApplyDuplicateApprovalAsync(
        DuplicateReview review, int administratorId, string administratorName,
        DateTime now, CancellationToken token)
    {
        review.Status = DuplicateReviewStatusNames.Approved;
        review.ReviewedByUserAccountID = administratorId;
        review.ReviewedDate = now;
        await ApplyDuplicateStateAsync(
            review.Ticket, review.SuggestedOriginalTicketID, now, token);

        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = review.TicketID,
            PerformedByUserAccountID = administratorId,
            ActionType = TicketHistoryActionNames.DuplicateReviewApproved,
            OldValue = DuplicateReviewStatusNames.Pending,
            NewValue = DuplicateReviewStatusNames.Approved,
            Description = $"{administratorName} approved the duplicate review. {review.Ticket.TicketReferenceNumber} was marked as a duplicate of {review.SuggestedOriginalTicket.TicketReferenceNumber}.",
            CreatedDate = now
        });
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = administratorId,
            ActionType = TicketHistoryActionNames.DuplicateReviewApproved,
            EntityType = "Ticket",
            EntityID = review.Ticket.TicketReferenceNumber,
            Description = $"Administrator {administratorName} approved {review.Ticket.TicketReferenceNumber} as a duplicate of {review.SuggestedOriginalTicket.TicketReferenceNumber}.",
            OldValue = DuplicateReviewStatusNames.Pending,
            NewValue = DuplicateReviewStatusNames.Approved,
            CreatedDate = now
        });
    }

    private async Task ApplyDuplicateStateAsync(
        Ticket ticket, int originalTicketId, DateTime now,
        CancellationToken token)
    {
        ticket.TicketStatusID = await dbContext.TicketStatuses
            .Where(status => status.Name == TicketStatusNames.Duplicate)
            .Select(status => status.ID).SingleAsync(token);
        ticket.OriginalTicketID = originalTicketId;
        ticket.UpdatedDate = now;
    }

    public async Task<IReadOnlyCollection<UserNotificationDto>> GetNotificationsAsync(
        int userId, CancellationToken token) =>
        await dbContext.UserNotifications.AsNoTracking()
            .Where(item => item.UserAccountID == userId)
            .OrderByDescending(item => item.CreatedDate)
            .Select(item => new UserNotificationDto(
                item.ID, item.Type, item.Title, item.Message,
                item.TicketReferenceNumber, item.IsRead, item.CreatedDate))
            .ToListAsync(token);

    public async Task<bool> MarkNotificationReadAsync(
        int userId, int notificationId, CancellationToken token)
    {
        var notification = await dbContext.UserNotifications.SingleOrDefaultAsync(
            item => item.ID == notificationId && item.UserAccountID == userId, token);
        if (notification is null) return false;
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await dbContext.SaveChangesAsync(token);
        }
        return true;
    }

    public async Task MarkAllNotificationsReadAsync(
        int userId, CancellationToken token)
    {
        var notifications = await dbContext.UserNotifications
            .Where(item => item.UserAccountID == userId && !item.IsRead)
            .ToListAsync(token);
        foreach (var notification in notifications) notification.IsRead = true;
        if (notifications.Count > 0) await dbContext.SaveChangesAsync(token);
    }

    private IQueryable<AdminUnassignedTicketDto> GetUnassignedTickets(
        AdminTicketFilterDto filter)
    {
        var query = dbContext.Tickets.AsNoTracking()
            .Where(ticket =>
                !ticket.IsDeleted &&
                ticket.AssignedToUserAccountID == null &&
                !ticket.TicketStatus.IsFinalStatus);
        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.ToLower();
            query = query.Where(ticket =>
                ticket.TicketReferenceNumber.ToLower().Contains(normalizedSearch) ||
                ticket.Title.ToLower().Contains(normalizedSearch) ||
                (ticket.CreatedByUserAccount.FirstName + " " +
                 ticket.CreatedByUserAccount.LastName).ToLower()
                    .Contains(normalizedSearch));
        }
        if (filter.CategoryId.HasValue)
            query = query.Where(ticket =>
                ticket.TicketCategoryID == filter.CategoryId);
        if (filter.PriorityId.HasValue)
            query = query.Where(ticket =>
                ticket.TicketPriorityID == filter.PriorityId);
        query = query.ApplyUtcDateRange(filter.FromUtc,
            filter.ToUtcExclusive, ticket => ticket.CreatedDate);
        return query
            .OrderByDescending(ticket => ticket.TicketPriority.SortOrder)
            .ThenBy(ticket => ticket.CreatedDate)
            .Select(ticket => new AdminUnassignedTicketDto(
                ticket.ID,
                ticket.TicketReferenceNumber,
                ticket.Title,
                ticket.CreatedByUserAccount.FirstName + " " +
                    ticket.CreatedByUserAccount.LastName,
                ticket.TicketCategory.Name,
                ticket.TicketPriority.Name,
                ticket.CreatedDate));
    }

    private async Task<IReadOnlyCollection<AdminAgentWorkloadDto>>
        GetAgentWorkloadsAsync(CancellationToken token)
    {
        var agents = await (
            from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where role.Name == RoleNames.ITSupportAgent && user.IsActive
            select new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                Name = user.FirstName + " " + user.LastName,
                user.Email
            })
            .Distinct()
            .OrderBy(agent => agent.FirstName)
            .ThenBy(agent => agent.LastName)
            .ThenBy(agent => agent.Id)
            .ToListAsync(token);
        var agentIds = agents.Select(agent => agent.Id).ToArray();
        var counts = await dbContext.Tickets.AsNoTracking()
            .Where(ticket =>
                !ticket.IsDeleted &&
                ticket.AssignedToUserAccountID.HasValue &&
                agentIds.Contains(ticket.AssignedToUserAccountID.Value))
            .GroupBy(ticket => ticket.AssignedToUserAccountID!.Value)
            .Select(group => new
            {
                AgentId = group.Key,
                Active = group.Count(ticket =>
                    TicketWorkloadRules.ActiveStatuses.Contains(
                        ticket.TicketStatus.Name)),
                Assigned = group.Count(ticket =>
                    ticket.TicketStatus.Name == TicketStatusNames.Assigned),
                InProgress = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.InProgress),
                Pending = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.Pending)
            }).ToDictionaryAsync(item => item.AgentId, token);

        return agents.Select(agent =>
        {
            counts.TryGetValue(agent.Id, out var workload);
            var active = workload?.Active ?? 0;
            return new AdminAgentWorkloadDto(
                agent.Id,
                agent.FirstName,
                agent.LastName,
                agent.Name,
                agent.Email!,
                active,
                workload?.Assigned ?? 0,
                workload?.InProgress ?? 0,
                workload?.Pending ?? 0,
                TicketWorkloadRules.MaxActiveTicketsPerAgent,
                TicketWorkloadRules.GetRemainingCapacity(active),
                TicketWorkloadRules.GetCapacityState(active),
                TicketWorkloadRules.IsAtCapacity(active));
        }).ToList();
    }
}
