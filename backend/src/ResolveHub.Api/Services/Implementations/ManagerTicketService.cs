using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class ManagerTicketService(
    ApplicationDbContext dbContext,
    IAdminTicketService adminTicketService) : IManagerTicketService
{
    private static readonly string[] ActiveStatuses =
        [TicketStatusNames.Assigned, TicketStatusNames.InProgress, TicketStatusNames.Pending];

    public Task<PagedResultDto<AdminTicketListItemDto>> GetTicketsAsync(
        AdminTicketFilterDto filter, CancellationToken token) =>
        adminTicketService.GetTicketsAsync(filter, token);

    public Task<AdminTicketDetailsDto?> GetTicketAsync(
        string ticketReference, CancellationToken token) =>
        adminTicketService.GetTicketAsync(ticketReference, token);

    public Task<AdminAssignmentOverviewDto> GetAssignmentsAsync(
        CancellationToken token) =>
        adminTicketService.GetAssignmentsAsync(token);

    public Task<TicketServiceResult<bool>> AssignAsync(
        int managerId, string ticketReference, int agentUserId,
        CancellationToken token) =>
        adminTicketService.AssignAsync(managerId, ticketReference, agentUserId, token);

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
        var assignments = await adminTicketService.GetAssignmentsAsync(token);
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
                ticket.CreatedDate, ticket.UpdatedDate))
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
                Active = group.Count(ticket => ActiveStatuses.Contains(ticket.TicketStatus.Name)),
                Open = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.Open),
                InProgress = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.InProgress),
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
                row?.Open ?? 0,
                row?.InProgress ?? 0,
                row?.Resolved ?? 0,
                row?.Critical ?? 0,
                active >= 8 ? "High Workload" : active >= 4 ? "Balanced" : "Available");
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
