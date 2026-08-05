using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Services.Implementations;

public sealed class SystemAuditLogService(ApplicationDbContext dbContext) : ISystemAuditLogService
{
    private static readonly string[] AssignmentActions =
    [
        "Assignment Approved", "Assignment Rejected", "Assignment Request Approved",
        "Assignment Request Rejected", "Ticket Assigned", "Ticket Reassigned",
        "Ticket Unassigned", "Duplicate Review Approved", "Duplicate Review Rejected"
    ];

    public async Task<SystemAuditPageDto> GetAsync(
        SystemAuditFilterDto filter, CancellationToken token)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var query = dbContext.ActivityLogs.AsNoTracking()
            .Where(log => log.EntityType == "UserAccount" ||
                log.EntityType == "TicketCategory" || log.EntityType == "Category" ||
                AssignmentActions.Contains(log.ActionType) ||
                log.ActionType.Contains("Password") || log.ActionType.Contains("Account Locked") ||
                log.ActionType.Contains("Account Unlocked") || log.ActionType.Contains("Authorization") ||
                log.EntityType == "SystemConfiguration");

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(log => log.ActionType.Contains(search) ||
                log.Description.Contains(search) || log.EntityID.Contains(search) ||
                log.EntityType.Contains(search) ||
                log.PerformedByUserAccount.FirstName.Contains(search) ||
                log.PerformedByUserAccount.LastName.Contains(search) ||
                (log.PerformedByUserAccount.Email != null &&
                    log.PerformedByUserAccount.Email.Contains(search)));

        query = ApplyDates(query, filter);

        var totalItems = await query.CountAsync(token);
        var rows = await query.OrderByDescending(log => log.CreatedDate)
            .ThenByDescending(log => log.ID)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(log => new
            {
                log.ID, log.CreatedDate, log.PerformedByUserAccountID,
                PerformerFirstName = log.PerformedByUserAccount.FirstName,
                PerformerLastName = log.PerformedByUserAccount.LastName,
                PerformerEmail = log.PerformedByUserAccount.Email,
                log.ActionType, log.EntityType, log.EntityID, log.Description,
                log.OldValue, log.NewValue
            }).ToListAsync(token);

        var performerIds = rows.Select(row => row.PerformedByUserAccountID).Distinct().ToArray();
        var roles = await (from assignment in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where performerIds.Contains(assignment.UserId)
            select new { assignment.UserId, role.Name }).ToListAsync(token);
        var roleByUser = roles.GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Name)
                .FirstOrDefault(name => name == "Administrator") ?? group.First().Name ?? "Unknown");

        var userTargetIds = rows.Where(row => row.EntityType == "UserAccount")
            .Select(row => int.TryParse(row.EntityID, out var id) ? id : 0)
            .Where(id => id > 0).Distinct().ToArray();
        var userTargets = await dbContext.Users.AsNoTracking()
            .Where(user => userTargetIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id,
                user => (user.FirstName + " " + user.LastName).Trim(), token);

        var items = rows.Select(row =>
        {
            var targetName = row.EntityType == "UserAccount" &&
                int.TryParse(row.EntityID, out var userId) && userTargets.TryGetValue(userId, out var name)
                    ? name : row.EntityID;
            return new SystemAuditRecordDto(row.ID, row.CreatedDate,
                row.PerformedByUserAccountID,
                $"{row.PerformerFirstName} {row.PerformerLastName}".Trim(),
                row.PerformerEmail ?? "", roleByUser.GetValueOrDefault(row.PerformedByUserAccountID, "Unknown"),
                row.ActionType, Category(row.ActionType, row.EntityType), row.EntityType,
                row.EntityID, targetName, row.Description, "Successful",
                row.OldValue, row.NewValue, RelatedUrl(row.EntityType, row.EntityID), null);
        }).ToList();

        return new(items, page, pageSize, totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    private static IQueryable<ActivityLog> ApplyDates(
        IQueryable<ActivityLog> query, SystemAuditFilterDto filter)
    {
        var today = DateTime.UtcNow.Date;
        return filter.DateRange switch
        {
            "today" => query.Where(log => log.CreatedDate >= today),
            "yesterday" => query.Where(log => log.CreatedDate >= today.AddDays(-1) && log.CreatedDate < today),
            "7" => query.Where(log => log.CreatedDate >= today.AddDays(-6)),
            "30" => query.Where(log => log.CreatedDate >= today.AddDays(-29)),
            "custom" => query.Where(log =>
                (filter.FromDate == null || log.CreatedDate >= filter.FromDate.Value.Date) &&
                (filter.ToDate == null || log.CreatedDate < filter.ToDate.Value.Date.AddDays(1))),
            _ => query
        };
    }

    private static string Category(string action, string entityType)
    {
        if (action.Contains("Password") || action.Contains("Locked") || action.Contains("Authorization"))
            return "Security and Authentication";
        if (entityType == "UserAccount") return "User Management";
        if (entityType is "TicketCategory" or "Category") return "Category Management";
        if (AssignmentActions.Contains(action)) return "Assignment Administration";
        return "System Configuration";
    }

    private static string? RelatedUrl(string entityType, string entityId) => entityType switch
    {
        "UserAccount" when int.TryParse(entityId, out _) => $"/admin/users/{entityId}",
        "Ticket" => $"/admin/tickets/{Uri.EscapeDataString(entityId)}",
        _ => null
    };
}
