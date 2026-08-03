using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class TicketActivityService(ApplicationDbContext dbContext)
    : ITicketActivityService
{
    public async Task<TicketServiceResult<IReadOnlyCollection<TicketActivityDto>>> GetTimelineAsync(
        int userId, string ticketReference, bool descending, CancellationToken token)
    {
        var access = await GetAccessAsync(userId, ticketReference, token);
        if (access.Level == ActivityAccess.NotFound)
            return new(TicketOperationStatus.NotFound);
        if (access.Level == ActivityAccess.Forbidden)
            return new(TicketOperationStatus.Forbidden,
                Message: "You do not have permission to view this ticket activity.");
        var query = dbContext.TicketHistory.AsNoTracking()
            .Where(item => item.TicketID == access.TicketId &&
                (access.Level == ActivityAccess.Internal || !item.IsInternal));
        query = descending ? query.OrderByDescending(item => item.CreatedDate)
            : query.OrderBy(item => item.CreatedDate);
        var rows = await query.Select(item => new
        {
            item.ID, item.ActionType, item.Description,
            item.PerformedByUserAccountID,
            PerformerFullName = item.PerformedByUserAccount.FirstName + " " +
                item.PerformedByUserAccount.LastName,
            PerformerRole = item.PerformedByUserAccount.UserAccountRoles
                .Select(link => link.Role.Name!).FirstOrDefault() ?? "User",
            item.CreatedDate, item.OldValue, item.NewValue,
            item.WorkDurationMinutes, item.IsInternal
        }).ToListAsync(token);
        var items = rows.Select(item => new TicketActivityDto(
            item.ID, item.ActionType, item.Description,
            item.PerformedByUserAccountID, item.PerformerFullName,
            item.PerformerRole, AsUtc(item.CreatedDate), item.OldValue,
            item.NewValue, item.WorkDurationMinutes, item.IsInternal)).ToList();
        return new(TicketOperationStatus.Success, items);
    }

    public async Task<TicketServiceResult<TicketActivitySummaryDto>> GetSummaryAsync(
        int userId, string ticketReference, CancellationToken token)
    {
        var access = await GetAccessAsync(userId, ticketReference, token);
        if (access.Level == ActivityAccess.NotFound)
            return new(TicketOperationStatus.NotFound);
        if (access.Level == ActivityAccess.Forbidden)
            return new(TicketOperationStatus.Forbidden,
                Message: "You do not have permission to view this ticket activity.");
        var now = DateTime.UtcNow;
        var ticket = await dbContext.Tickets.AsNoTracking().Where(item => item.ID == access.TicketId)
            .Select(item => new
            {
                item.ID, item.TicketReferenceNumber, item.Title,
                Department = item.CreatedByUserAccount.Department == null ? null : item.CreatedByUserAccount.Department.Name,
                Category = item.TicketCategory.Name, Priority = item.TicketPriority.Name,
                CreatorId = item.CreatedByUserAccountID,
                CreatorName = item.CreatedByUserAccount.FirstName + " " + item.CreatedByUserAccount.LastName,
                CreatorRole = item.CreatedByUserAccount.UserAccountRoles.Select(x => x.Role.Name!).FirstOrDefault() ?? "User",
                item.CreatedDate, Status = item.TicketStatus.Name,
                AgentId = item.AssignedToUserAccountID,
                AgentName = item.AssignedToUserAccount == null ? null : item.AssignedToUserAccount.FirstName + " " + item.AssignedToUserAccount.LastName,
                item.ResolvedDate, item.ClosedDate
            }).SingleAsync(token);
        var sessions = await dbContext.TicketWorkSessions.AsNoTracking()
            .Where(item => item.TicketID == access.TicketId)
            .Select(item => new { item.ITAgentUserAccountID,
                Name = item.ITAgentUserAccount.FirstName + " " + item.ITAgentUserAccount.LastName,
                item.StartedAt, item.EndedAt, item.DurationMinutes }).ToListAsync(token);
        var total = sessions.Sum(item => SessionMinutes(
            item.StartedAt, item.EndedAt, item.DurationMinutes, now));
        var history = dbContext.TicketHistory.AsNoTracking().Where(item => item.TicketID == access.TicketId);
        var publicComments = dbContext.TicketComments.AsNoTracking().Where(item =>
            item.TicketID == access.TicketId && !item.IsDeleted && item.Visibility == Entities.CommentVisibility.Public);
        var privateComments = dbContext.TicketComments.AsNoTracking().Where(item =>
            item.TicketID == access.TicketId && !item.IsDeleted && item.Visibility == Entities.CommentVisibility.Private);
        var breakdown = sessions.GroupBy(item => new { item.ITAgentUserAccountID, item.Name })
            .Select(group => new AgentWorkTimeDto(group.Key.ITAgentUserAccountID,
                group.Key.Name, group.Sum(item => SessionMinutes(
                    item.StartedAt, item.EndedAt, item.DurationMinutes, now)),
                Format(group.Sum(item => SessionMinutes(
                    item.StartedAt, item.EndedAt, item.DurationMinutes, now))))).ToList();
        var summary = new TicketActivitySummaryDto(ticket.ID, ticket.TicketReferenceNumber, ticket.Title, ticket.Department,
            ticket.Category, ticket.Priority, ticket.CreatorId, ticket.CreatorName,
            ticket.CreatorRole, ticket.Department, AsUtc(ticket.CreatedDate), ticket.Status,
            ticket.AgentId, ticket.AgentName, sessions.OrderBy(x => x.StartedAt)
                .Select(x => (DateTime?)AsUtc(x.StartedAt)).FirstOrDefault(), AsUtc(ticket.ResolvedDate),
            AsUtc(ticket.ClosedDate), total, Math.Round(total / 60m, 2), Format(total),
            sessions.Any(x => x.EndedAt == null), sessions.Where(x => x.EndedAt == null)
                .Select(x => (DateTime?)AsUtc(x.StartedAt)).SingleOrDefault(),
            await history.CountAsync(x => x.ActionType == TicketHistoryActionNames.TicketAssigned || x.ActionType == TicketHistoryActionNames.TicketReassigned, token),
            await history.CountAsync(x => x.ActionType == TicketHistoryActionNames.StatusChanged, token),
            await publicComments.CountAsync(token), access.Level == ActivityAccess.Internal ? await privateComments.CountAsync(token) : null,
            await dbContext.TicketAttachments.CountAsync(x => x.TicketID == access.TicketId, token),
            await history.CountAsync(x => x.ActionType == TicketHistoryActionNames.TicketReopened, token),
            breakdown, await history.CountAsync(x => access.Level == ActivityAccess.Internal || !x.IsInternal, token));
        return new(TicketOperationStatus.Success, summary);
    }

    private async Task<ActivityAccessResult> GetAccessAsync(
        int userId, string ticketReference, CancellationToken token)
    {
        var normalizedReference = ticketReference.Trim();
        var role = await dbContext.Users.Where(x => x.Id == userId)
            .SelectMany(x => x.UserAccountRoles).Select(x => x.Role.Name).FirstOrDefaultAsync(token);
        var ticket = await dbContext.Tickets.AsNoTracking()
            .Where(item => item.TicketReferenceNumber == normalizedReference)
            .Select(item => new { item.ID, item.CreatedByUserAccountID, item.AssignedToUserAccountID,
                Status = item.TicketStatus.Name, item.IsDeleted,
                CreatorDepartmentId = item.CreatedByUserAccount.DepartmentID })
            .SingleOrDefaultAsync(token);
        if (ticket is null) return new(ActivityAccess.NotFound, 0);
        var userDepartmentId = role == RoleNames.Manager
            ? await dbContext.Users.Where(item => item.Id == userId)
                .Select(item => item.DepartmentID).SingleAsync(token)
            : null;
        var visible = ticket.CreatedByUserAccountID == userId ||
            ticket.AssignedToUserAccountID == userId || role == RoleNames.Admin ||
            (role == RoleNames.ITSupportAgent && !ticket.IsDeleted &&
                ticket.AssignedToUserAccountID == null && ticket.Status == TicketStatusNames.Open) ||
            (role == RoleNames.Manager && ticket.CreatorDepartmentId == userDepartmentId);
        if (!visible) return new(ActivityAccess.Forbidden, ticket.ID);
        return new(role is RoleNames.Admin or RoleNames.Manager or RoleNames.ITSupportAgent
            ? ActivityAccess.Internal : ActivityAccess.Public, ticket.ID);
    }

    private static string Format(int minutes) => minutes <= 0 ? "0m" :
        minutes < 60 ? $"{minutes}m" : $"{minutes / 60}h {minutes % 60}m";

    private static int SessionMinutes(
        DateTime startedAt, DateTime? endedAt, int? storedMinutes, DateTime now) =>
        Math.Max(0, storedMinutes ?? (int)Math.Floor(
            ((endedAt ?? now) - startedAt).TotalMinutes));

    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value) => value.HasValue
        ? AsUtc(value.Value) : null;

    private enum ActivityAccess { NotFound, Forbidden, Public, Internal }
    private sealed record ActivityAccessResult(ActivityAccess Level, int TicketId);
}
