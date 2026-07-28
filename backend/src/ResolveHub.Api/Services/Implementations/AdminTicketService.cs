using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AdminTicketService(ApplicationDbContext dbContext)
    : IAdminTicketService
{
    private static readonly string[] ActiveStatuses =
        [TicketStatusNames.Assigned, TicketStatusNames.InProgress, TicketStatusNames.Pending];

    public async Task<AdminAssignmentOverviewDto> GetAssignmentsAsync(
        CancellationToken token) =>
        new(
            await GetUnassignedTickets().ToListAsync(token),
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
            Unassigned = group.Count(ticket => ticket.AssignedToUserAccountID == null),
            Resolved = group.Count(ticket =>
                ticket.ResolvedDate >= monthStart && ticket.ResolvedDate < nextMonth)
        }).SingleOrDefaultAsync(token);

        return new(
            await dbContext.Users.CountAsync(token),
            counts?.Total ?? 0,
            counts?.Open ?? 0,
            counts?.InProgress ?? 0,
            counts?.Unassigned ?? 0,
            counts?.Resolved ?? 0,
            await GetUnassignedTickets().Take(5).ToListAsync(token),
            await GetAgentWorkloadsAsync(token));
    }

    public async Task<TicketServiceResult<bool>> AssignAsync(
        int administratorId,
        string ticketReference,
        int agentUserId,
        CancellationToken token)
    {
        var agentIsEligible = await (
            from user in dbContext.Users
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where user.Id == agentUserId &&
                  user.IsActive &&
                  role.Name == RoleNames.ITAgent
            select user.Id).AnyAsync(token);
        if (!agentIsEligible)
            return new(TicketOperationStatus.Invalid,
                Message: "Select an active IT Support Agent.");

        var ticket = await dbContext.Tickets
            .Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.TicketReferenceNumber == ticketReference &&
                !item.IsDeleted,
                token);
        if (ticket is null)
            return new(TicketOperationStatus.NotFound);
        if (ticket.AssignedToUserAccountID is not null)
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket has already been assigned.");

        var assignedStatusId = await dbContext.TicketStatuses
            .Where(status =>
                status.IsActive &&
                status.Name == TicketStatusNames.Assigned)
            .Select(status => status.ID)
            .SingleAsync(token);
        var now = DateTime.UtcNow;
        ticket.AssignedToUserAccountID = agentUserId;
        ticket.TicketStatusID = assignedStatusId;
        ticket.AssignedDate = now;
        ticket.UpdatedDate = now;
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = ticket.ID,
            PerformedByUserAccountID = administratorId,
            ActionType = TicketHistoryActionNames.TicketAssigned,
            OldValue = null,
            NewValue = agentUserId.ToString(),
            Description = "Ticket assigned to an IT Support Agent.",
            CreatedDate = now
        });
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success, true);
    }

    private IQueryable<AdminUnassignedTicketDto> GetUnassignedTickets() =>
        dbContext.Tickets.AsNoTracking()
            .Where(ticket =>
                !ticket.IsDeleted &&
                ticket.AssignedToUserAccountID == null)
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

    private async Task<IReadOnlyCollection<AdminAgentWorkloadDto>>
        GetAgentWorkloadsAsync(CancellationToken token)
    {
        var agents = await (
            from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where role.Name == RoleNames.ITAgent && user.IsActive
            orderby user.FirstName, user.LastName
            select new
            {
                user.Id,
                Name = user.FirstName + " " + user.LastName,
                user.Email
            }).ToListAsync(token);
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
                Active = group.Count(ticket => ActiveStatuses.Contains(ticket.TicketStatus.Name)),
                InProgress = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.InProgress),
                Pending = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.Pending)
            }).ToDictionaryAsync(item => item.AgentId, token);

        return agents.Select(agent =>
        {
            counts.TryGetValue(agent.Id, out var workload);
            var active = workload?.Active ?? 0;
            return new AdminAgentWorkloadDto(
                agent.Id,
                agent.Name,
                agent.Email!,
                active,
                workload?.InProgress ?? 0,
                workload?.Pending ?? 0,
                active >= 8 ? "High Workload" :
                    active >= 4 ? "Balanced" : "Available");
        }).ToList();
    }
}
