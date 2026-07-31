using System.Data;
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

public sealed class ManagerTicketService(
    ApplicationDbContext dbContext,
    IAdminTicketService adminTicketService) : IManagerTicketService
{
    public Task<PagedResultDto<AdminTicketListItemDto>> GetTicketsAsync(
        AdminTicketFilterDto filter, CancellationToken token) =>
        adminTicketService.GetTicketsAsync(filter, token);

    public Task<AdminTicketDetailsDto?> GetTicketAsync(
        string ticketReference, CancellationToken token) =>
        adminTicketService.GetTicketAsync(ticketReference, token);

    public Task<AdminAssignmentOverviewDto> GetAssignmentsAsync(
        AdminTicketFilterDto filter, CancellationToken token) =>
        adminTicketService.GetAssignmentsAsync(filter, token);

    public Task<TicketServiceResult<bool>> AssignAsync(
        int managerId, string ticketReference, int agentUserId,
        CancellationToken token) =>
        adminTicketService.AssignAsync(managerId, ticketReference, agentUserId, token);

    public Task<IReadOnlyCollection<UserNotificationDto>> GetNotificationsAsync(
        int userId, CancellationToken token) =>
        adminTicketService.GetNotificationsAsync(userId, token);

    public Task<bool> MarkNotificationReadAsync(
        int userId, int notificationId, CancellationToken token) =>
        adminTicketService.MarkNotificationReadAsync(userId, notificationId, token);

    public Task MarkAllNotificationsReadAsync(int userId, CancellationToken token) =>
        adminTicketService.MarkAllNotificationsReadAsync(userId, token);

    public async Task<TicketServiceResult<DuplicateReviewDto>> ReportDuplicateAsync(
        int managerId, string ticketReference,
        CreateDuplicateReviewRequestDto request, CancellationToken token)
    {
        var originalReference = request.SuggestedOriginalTicketReference.Trim();
        if (string.Equals(ticketReference, originalReference,
                StringComparison.OrdinalIgnoreCase))
            return new(TicketOperationStatus.Invalid,
                Message: "A ticket cannot be reported as a duplicate of itself.");

        var tickets = await dbContext.Tickets.Include(ticket => ticket.TicketStatus)
            .Include(ticket => ticket.TicketCategory)
            .Include(ticket => ticket.TicketPriority)
            .Include(ticket => ticket.CreatedByUserAccount)
            .Where(ticket => !ticket.IsDeleted &&
            (ticket.TicketReferenceNumber == ticketReference ||
             ticket.TicketReferenceNumber == originalReference))
            .ToListAsync(token);
        var reported = tickets.SingleOrDefault(ticket =>
            ticket.TicketReferenceNumber == ticketReference);
        var original = tickets.SingleOrDefault(ticket =>
            ticket.TicketReferenceNumber == originalReference);
        if (reported is null || original is null)
            return new(TicketOperationStatus.NotFound,
                Message: "The reported or suggested original ticket could not be found.");
        if (DuplicateTicketRules.IsDuplicate(reported.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket has already been marked as Duplicate.");
        if (await dbContext.DuplicateReviews.AnyAsync(review =>
                review.TicketID == reported.ID &&
                review.Status == DuplicateReviewStatusNames.Pending, token))
            return new(TicketOperationStatus.Conflict,
                Message: "A duplicate review is already pending for this ticket.");

        var reporter = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == managerId)
            .Select(user => new
            {
                Name = user.FirstName + " " + user.LastName,
                IsAdministrator = user.UserAccountRoles.Any(role =>
                    role.Role.Name == RoleNames.Admin)
            })
            .SingleAsync(token);
        var now = DateTime.UtcNow;
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? null : request.Reason.Trim();
        var review = new DuplicateReview
        {
            TicketID = reported.ID,
            SuggestedOriginalTicketID = original.ID,
            ReportedByUserAccountID = managerId,
            Reason = reason,
            Status = DuplicateReviewStatusNames.Pending,
            CreatedDate = now
        };
        dbContext.DuplicateReviews.Add(review);
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = reported.ID,
            PerformedByUserAccountID = managerId,
            ActionType = TicketHistoryActionNames.DuplicateReviewReported,
            NewValue = original.TicketReferenceNumber,
            Description = $"{reporter.Name} reported {reported.TicketReferenceNumber} as a possible duplicate of {original.TicketReferenceNumber}.",
            CreatedDate = now
        });
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = managerId,
            ActionType = TicketHistoryActionNames.DuplicateReviewReported,
            EntityType = "Ticket",
            EntityID = reported.TicketReferenceNumber,
            Description = $"{reporter.Name} reported {reported.TicketReferenceNumber} as a possible duplicate of {original.TicketReferenceNumber}.",
            NewValue = original.TicketReferenceNumber,
            CreatedDate = now
        });
        var administratorIds = await dbContext.UserRoles
            .Where(assignment => assignment.Role.Name == RoleNames.Admin &&
                assignment.UserAccount.IsActive)
            .Select(assignment => assignment.UserId).Distinct().ToListAsync(token);
        foreach (var administratorId in administratorIds)
            dbContext.UserNotifications.Add(new UserNotification
            {
                UserAccountID = administratorId,
                Type = "DuplicateReview",
                Title = "Duplicate Review Pending",
                Message = $"{reporter.Name} reported {reported.TicketReferenceNumber} as a possible duplicate of {original.TicketReferenceNumber}.",
                TicketReferenceNumber = reported.TicketReferenceNumber,
                CreatedDate = now
            });
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success,
            new DuplicateReviewDto(review.ID, reported.TicketReferenceNumber,
                reported.Title, reported.TicketStatus.Name,
                reported.TicketPriority.Name, reported.CreatedDate,
                reported.CreatedByUserAccount.FirstName + " " +
                    reported.CreatedByUserAccount.LastName,
                reported.TicketCategory.Name,
                original.TicketReferenceNumber, original.Title,
                original.TicketStatus.Name,
                original.TicketPriority.Name, original.CreatedDate,
                original.CreatedByUserAccount.FirstName + " " +
                    original.CreatedByUserAccount.LastName,
                original.TicketCategory.Name, reporter.Name,
                reporter.IsAdministrator, reason,
                review.Status, now));
    }

    public async Task<IReadOnlyCollection<TicketAssignmentRequestDto>>
        GetAssignmentRequestsAsync(CancellationToken token) =>
        await dbContext.TicketAssignmentRequests.AsNoTracking()
            .Where(item => item.Status == AssignmentRequestStatusNames.Pending &&
                !item.Ticket.IsDeleted &&
                item.Ticket.AssignedToUserAccountID == null &&
                item.Ticket.TicketStatus.Name == TicketStatusNames.Open)
            .OrderBy(item => item.RequestedDate)
            .Select(item => new TicketAssignmentRequestDto(
                item.ID, item.TicketID, item.Ticket.TicketReferenceNumber,
                item.Ticket.Title, item.RequestedByUserAccountID,
                item.RequestedByUserAccount.FirstName + " " +
                    item.RequestedByUserAccount.LastName,
                item.Status, item.RequestedDate,
                item.ReviewedByUserAccountID,
                item.ReviewedByUserAccount == null ? null :
                    item.ReviewedByUserAccount.FirstName + " " +
                    item.ReviewedByUserAccount.LastName,
                item.ReviewedDate))
            .ToListAsync(token);

    public async Task<TicketServiceResult<bool>> ReviewAssignmentRequestAsync(
        int managerId, int requestId, bool approve, CancellationToken token)
    {
        var request = await dbContext.TicketAssignmentRequests
            .Include(item => item.Ticket)
            .SingleOrDefaultAsync(item => item.ID == requestId, token);
        if (request is null) return new(TicketOperationStatus.NotFound);
        if (request.Status != AssignmentRequestStatusNames.Pending)
            return new(TicketOperationStatus.Conflict,
                Message: "This assignment request has already been reviewed.");
        if (request.Ticket.IsDeleted ||
            request.Ticket.AssignedToUserAccountID.HasValue)
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket is no longer available for assignment.");

        if (approve)
        {
            IDbContextTransaction? transaction = null;
            try
            {
                if (dbContext.Database.IsRelational())
                {
                    transaction = await dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable, token);
                }
                var assignment = await adminTicketService.AssignAsync(
                    managerId, request.Ticket.TicketReferenceNumber,
                    request.RequestedByUserAccountID, token);
                if (assignment.Status != TicketOperationStatus.Success)
                {
                    if (transaction is not null)
                        await transaction.RollbackAsync(token);
                    return assignment;
                }
                await CompleteReviewAsync(
                    request, managerId, true, token);
                if (transaction is not null)
                    await transaction.CommitAsync(token);
                return new(TicketOperationStatus.Success, true);
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
        }

        await CompleteReviewAsync(request, managerId, false, token);
        return new(TicketOperationStatus.Success, true);
    }

    private async Task CompleteReviewAsync(
        TicketAssignmentRequest request, int managerId,
        bool approve, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        if (approve)
        {
            var competingRequests = await dbContext.TicketAssignmentRequests
                .Where(item => item.TicketID == request.TicketID &&
                    item.ID != request.ID &&
                    item.Status == AssignmentRequestStatusNames.Pending)
                .ToListAsync(token);
            foreach (var competing in competingRequests)
            {
                competing.Status = AssignmentRequestStatusNames.Rejected;
                competing.ReviewedByUserAccountID = managerId;
                competing.ReviewedDate = now;
            }
        }
        request.Status = approve
            ? AssignmentRequestStatusNames.Approved
            : AssignmentRequestStatusNames.Rejected;
        request.ReviewedByUserAccountID = managerId;
        request.ReviewedDate = now;
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = request.TicketID,
            PerformedByUserAccountID = managerId,
            ActionType = approve
                ? TicketHistoryActionNames.AssignmentRequestApproved
                : TicketHistoryActionNames.AssignmentRequestRejected,
            OldValue = AssignmentRequestStatusNames.Pending,
            NewValue = request.Status,
            Description = approve
                ? "Manager approved the assignment request."
                : "Manager rejected the assignment request.",
            CreatedDate = now
        });
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = managerId,
            ActionType = approve
                ? TicketHistoryActionNames.AssignmentRequestApproved
                : TicketHistoryActionNames.AssignmentRequestRejected,
            EntityType = "Ticket",
            EntityID = request.Ticket.TicketReferenceNumber,
            Description = approve
                ? "Manager approved an agent assignment request."
                : "Manager rejected an agent assignment request.",
            OldValue = AssignmentRequestStatusNames.Pending,
            NewValue = request.Status,
            CreatedDate = now
        });
        await dbContext.SaveChangesAsync(token);
    }

    public async Task<TicketServiceResult<TicketCommentDto>> AddCommentAsync(
        int managerId, string ticketReference,
        AddTicketCommentRequestDto request, CancellationToken token)
    {
        var content = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > TicketCommentRules.MaximumMessageLength)
            return new(TicketOperationStatus.Invalid,
                Message: "Comment content is required and cannot exceed 5000 characters.");
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.TicketReferenceNumber == ticketReference && !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (TicketCommentRules.IsReadOnly(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name)
                    ? DuplicateTicketRules.ReadOnlyMessage
                    : "Comments cannot be added to a closed or cancelled ticket.");
        var now = DateTime.UtcNow;
        var comment = new TicketComment
        {
            TicketID = ticket.ID,
            AuthorUserAccountID = managerId,
            Content = content,
            Visibility = CommentVisibility.Public,
            CreatedDate = now
        };
        dbContext.TicketComments.Add(comment);
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = ticket.ID,
            PerformedByUserAccountID = managerId,
            ActionType = TicketHistoryActionNames.CommentAdded,
            Description = TicketCommentRules.HistoryDescription(
                CommentVisibility.Public),
            CreatedDate = now
        });
        ticket.UpdatedDate = now;
        await dbContext.SaveChangesAsync(token);
        var author = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == managerId)
            .Select(user => new
            {
                Name = user.FirstName + " " + user.LastName,
                Role = user.UserAccountRoles.Select(item => item.Role.Name!)
                    .FirstOrDefault() ?? string.Empty
            })
            .SingleAsync(token);
        return new(TicketOperationStatus.Success,
            new(comment.ID, author.Name, author.Role, comment.Content,
                now, null, false,
                CommentVisibility.Public.ToString()));
    }

    public async Task<ManagerDashboardDto> GetDashboardAsync(CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);
        var tickets = dbContext.Tickets.AsNoTracking().Where(ticket => !ticket.IsDeleted);
        var counts = await tickets.GroupBy(_ => 1).Select(group => new
        {
            Total = group.Count(),
            Open = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.Open),
            InProgress = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.InProgress),
            Unassigned = group.Count(ticket =>
                ticket.AssignedToUserAccountID == null && !ticket.TicketStatus.IsFinalStatus),
            Resolved = group.Count(ticket =>
                ticket.ResolvedDate >= monthStart && ticket.ResolvedDate < nextMonth),
            Critical = group.Count(ticket =>
                ticket.TicketPriority.Name == "Critical" && !ticket.TicketStatus.IsFinalStatus)
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
                Value = group.Count(),
                group.Key.SortOrder
            })
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .ToListAsync(token);
        var priorityRows = await tickets
            .GroupBy(ticket => new
            {
                ticket.TicketPriorityID,
                ticket.TicketPriority.Name,
                ticket.TicketPriority.SortOrder
            })
            .Select(group => new
            {
                group.Key.Name,
                Value = group.Count(),
                group.Key.SortOrder
            })
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .ToListAsync(token);
        var assignments = await adminTicketService.GetAssignmentsAsync(
            new AdminTicketFilterDto(), token);
        var workload = await GetWorkloadAsync(token);
        var activity = await GetActivityAsync(token);
        var attention = await tickets
            .Where(ticket =>
                !ticket.TicketStatus.IsFinalStatus &&
                (ticket.AssignedToUserAccountID == null ||
                 ticket.TicketPriority.Name == "Critical" ||
                 ticket.TicketPriority.Name == "High"))
            .OrderByDescending(ticket => ticket.TicketPriority.SortOrder)
            .ThenBy(ticket => ticket.CreatedDate)
            .Take(5)
            .Select(ticket => new AdminTicketListItemDto(
                ticket.ID, ticket.TicketReferenceNumber, ticket.Title,
                ticket.CreatedByUserAccountID,
                ticket.CreatedByUserAccount.FirstName + " " + ticket.CreatedByUserAccount.LastName,
                ticket.TicketCategoryID, ticket.TicketCategory.Name,
                ticket.TicketPriorityID, ticket.TicketPriority.Name,
                ticket.TicketStatusID, ticket.TicketStatus.Name,
                ticket.AssignedToUserAccountID,
                ticket.AssignedToUserAccount == null ? null :
                    ticket.AssignedToUserAccount.FirstName + " " +
                    ticket.AssignedToUserAccount.LastName,
                ticket.CreatedDate, ticket.UpdatedDate,
                ticket.OriginalTicket == null ? null :
                    ticket.OriginalTicket.TicketReferenceNumber))
            .ToListAsync(token);

        return new ManagerDashboardDto(
            counts?.Total ?? 0,
            counts?.Open ?? 0,
            counts?.InProgress ?? 0,
            counts?.Unassigned ?? 0,
            counts?.Resolved ?? 0,
            counts?.Critical ?? 0,
            statusRows.Select(item => new AdminChartItemDto(item.Name, item.Value)).ToList(),
            priorityRows.Select(item => new ManagerPriorityCountDto(item.Name, item.Value)).ToList(),
            assignments.UnassignedTickets.Take(5).ToList(),
            workload,
            activity.Items.Take(8).ToList(),
            attention);
    }

    public async Task<IReadOnlyCollection<ManagerAgentWorkloadDto>> GetWorkloadAsync(
        CancellationToken token)
    {
        var monthStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var agents = await (
            from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.IsActive && role.Name == RoleNames.ITSupportAgent
            select new { user.Id, user.FirstName, user.LastName, user.Email })
            .Distinct()
            .OrderBy(agent => agent.FirstName)
            .ThenBy(agent => agent.LastName)
            .ToListAsync(token);
        var agentIds = agents.Select(agent => agent.Id).ToArray();
        var rows = await dbContext.Tickets.AsNoTracking()
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
                Pending = group.Count(ticket =>
                    ticket.TicketStatus.Name == TicketStatusNames.Pending),
                Resolved = group.Count(ticket => ticket.ResolvedDate >= monthStart),
                Critical = group.Count(ticket =>
                    ticket.TicketPriority.Name == "Critical" &&
                    !ticket.TicketStatus.IsFinalStatus)
            })
            .ToDictionaryAsync(row => row.AgentId, token);

        return agents.Select(agent =>
        {
            rows.TryGetValue(agent.Id, out var row);
            var active = row?.Active ?? 0;
            return new ManagerAgentWorkloadDto(
                agent.Id,
                $"{agent.FirstName} {agent.LastName}",
                agent.Email ?? string.Empty,
                active,
                row?.Assigned ?? 0,
                row?.InProgress ?? 0,
                row?.Pending ?? 0,
                row?.Resolved ?? 0,
                row?.Critical ?? 0,
                TicketWorkloadRules.MaxActiveTicketsPerAgent,
                TicketWorkloadRules.GetRemainingCapacity(active),
                TicketWorkloadRules.GetCapacityState(active),
                TicketWorkloadRules.IsAtCapacity(active));
        }).ToList();
    }

    public async Task<ManagerActivityResultDto> GetActivityAsync(CancellationToken token)
    {
        var rows = await dbContext.TicketHistory.AsNoTracking()
            .Where(history => !history.Ticket.IsDeleted)
            .OrderByDescending(history => history.CreatedDate)
            .Take(100)
            .Select(history => new ManagerActivityDto(
                history.ID,
                history.ActionType,
                history.Ticket.TicketReferenceNumber,
                history.Ticket.Title,
                history.PerformedByUserAccount.FirstName + " " +
                    history.PerformedByUserAccount.LastName,
                history.Description ?? history.ActionType,
                history.CreatedDate))
            .ToListAsync(token);
        return new ManagerActivityResultDto(rows);
    }
}
