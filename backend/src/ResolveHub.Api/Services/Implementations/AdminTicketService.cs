using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AdminTicketService(ApplicationDbContext dbContext)
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
        if (filter.FromUtc.HasValue)
            query = query.Where(ticket =>
                ticket.CreatedDate >= filter.FromUtc.Value.UtcDateTime);
        else if (filter.FromDate.HasValue)
            query = query.Where(ticket => ticket.CreatedDate >= filter.FromDate.Value.Date);
        if (filter.ToUtcExclusive.HasValue)
            query = query.Where(ticket =>
                ticket.CreatedDate < filter.ToUtcExclusive.Value.UtcDateTime);
        else if (filter.ToDate.HasValue)
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
                ticket.CreatedDate, ticket.UpdatedDate))
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
                ticket.CreatedDate, ticket.UpdatedDate, ticket.AssignedDate, ticket.ResolvedDate,
                ticket.Attachments.Where(item => !item.IsDeleted).Select(item =>
                    new TicketAttachmentDto(item.ID, item.FileName, item.ContentType,
                        item.FileSizeBytes, item.UploadedDate, false)).ToList(),
                ticket.Comments.Where(item => !item.IsDeleted).Select(item =>
                    new TicketCommentDto(item.ID,
                        item.AuthorUserAccount.FirstName + " " + item.AuthorUserAccount.LastName,
                        item.Content, item.CreatedDate, item.UpdatedDate, item.IsEdited)).ToList(),
                ticket.History.OrderByDescending(item => item.CreatedDate).Select(item =>
                    new TicketHistoryDto(item.ID, item.ActionType,
                        item.PerformedByUserAccount.FirstName + " " +
                        item.PerformedByUserAccount.LastName,
                        item.OldValue, item.NewValue, item.Description, item.CreatedDate)).ToList()))
            .SingleOrDefaultAsync(token);

    public async Task<TicketServiceResult<bool>> AssignAsync(
        int administratorId,
        string ticketReference,
        int? agentUserId,
        CancellationToken token)
    {
        IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational())
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
                    ? TicketHistoryActionNames.TicketAssigned
                    : "Ticket Unassigned",
                OldValue = previousAgentId?.ToString(),
                NewValue = agentUserId?.ToString(),
                Description =
                    previousAgentId.HasValue && agentUserId.HasValue
                        ? "Ticket reassigned to another IT Support Agent."
                        : agentUserId.HasValue
                            ? "Ticket assigned to an IT Support Agent."
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

    public async Task<TicketServiceResult<bool>> RemoveDuplicateAsync(
        int administratorId,
        string duplicateTicketReference,
        RemoveDuplicateTicketRequestDto request,
        CancellationToken token)
    {
        var originalReference = request.OriginalTicketReference.Trim();
        if (!request.Confirmed)
        {
            return new(
                TicketOperationStatus.Invalid,
                Message: "Confirm that this ticket is a duplicate.");
        }
        if (string.Equals(
            duplicateTicketReference,
            originalReference,
            StringComparison.OrdinalIgnoreCase))
        {
            return new(
                TicketOperationStatus.Invalid,
                Message: "A duplicate ticket cannot reference itself.");
        }

        var tickets = await dbContext.Tickets
            .Include(ticket => ticket.TicketStatus)
            .Where(ticket =>
                !ticket.IsDeleted &&
                (ticket.TicketReferenceNumber == duplicateTicketReference ||
                 ticket.TicketReferenceNumber == originalReference))
            .ToListAsync(token);
        var duplicate = tickets.SingleOrDefault(ticket =>
            ticket.TicketReferenceNumber == duplicateTicketReference);
        var original = tickets.SingleOrDefault(ticket =>
            ticket.TicketReferenceNumber == originalReference);
        if (duplicate is null || original is null)
        {
            return new(
                TicketOperationStatus.NotFound,
                Message: "The duplicate or original ticket could not be found.");
        }

        var cancelledStatusId = await dbContext.TicketStatuses
            .Where(status =>
                status.IsActive &&
                status.Name == TicketStatusNames.Cancelled)
            .Select(status => status.ID)
            .SingleAsync(token);
        var now = DateTime.UtcNow;
        duplicate.IsDeleted = true;
        duplicate.TicketStatusID = cancelledStatusId;
        duplicate.CancelledDate = now;
        duplicate.CancelledReason =
            $"Duplicate ticket. Original: {original.TicketReferenceNumber}.";
        duplicate.UpdatedDate = now;
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = duplicate.ID,
            PerformedByUserAccountID = administratorId,
            ActionType = TicketHistoryActionNames.DuplicateRemoved,
            OldValue = duplicate.TicketReferenceNumber,
            NewValue = original.TicketReferenceNumber,
            Description = "Duplicate ticket removed by an Administrator.",
            CreatedDate = now
        });
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = administratorId,
            ActionType = TicketHistoryActionNames.DuplicateRemoved,
            EntityType = "Ticket",
            EntityID = duplicate.TicketReferenceNumber,
            Description =
                $"Duplicate ticket removed. Original ticket: {original.TicketReferenceNumber}.",
            OldValue = duplicate.TicketReferenceNumber,
            NewValue = original.TicketReferenceNumber,
            CreatedDate = now
        });
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success, true);
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
        if (filter.FromUtc.HasValue)
            query = query.Where(ticket =>
                ticket.CreatedDate >= filter.FromUtc.Value.UtcDateTime);
        if (filter.ToUtcExclusive.HasValue)
            query = query.Where(ticket =>
                ticket.CreatedDate < filter.ToUtcExclusive.Value.UtcDateTime);
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
