using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Infrastructure;
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

        query = query.ApplyUtcDateRange(filter.FromUtc,
            filter.ToUtcExclusive, log => log.CreatedDate);

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
        var assignmentUserIds = rows.Where(row => row.ActionType is "Ticket Assigned" or "Ticket Reassigned" or "Ticket Unassigned")
            .SelectMany(row => new[] { ParseId(row.OldValue), ParseId(row.NewValue) })
            .Where(id => id > 0);
        userTargetIds = userTargetIds.Concat(assignmentUserIds).Distinct().ToArray();
        var userTargets = await dbContext.Users.AsNoTracking()
            .Where(user => userTargetIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id,
                user => (user.FirstName + " " + user.LastName).Trim(), token);

        var items = rows.Select(row =>
        {
            var targetName = row.EntityType == "UserAccount" &&
                int.TryParse(row.EntityID, out var userId) && userTargets.TryGetValue(userId, out var name)
                    ? name : row.EntityID;
            var oldValue = HumanizeValue(row.ActionType, row.OldValue, userTargets);
            var newValue = HumanizeValue(row.ActionType, row.NewValue, userTargets);
            return new SystemAuditRecordDto(row.ID, row.CreatedDate,
                row.PerformedByUserAccountID,
                $"{row.PerformerFirstName} {row.PerformerLastName}".Trim(),
                row.PerformerEmail ?? "", roleByUser.GetValueOrDefault(row.PerformedByUserAccountID, "Unknown"),
                row.ActionType, Category(row.ActionType, row.EntityType), FriendlyEntityType(row.EntityType),
                row.EntityID, targetName, FriendlyDescription(row.ActionType, targetName,
                    row.Description, oldValue, newValue), "Successful",
                oldValue, newValue, RelatedUrl(row.EntityType, row.EntityID), null);
        }).ToList();

        return new(items, page, pageSize, totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize));
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

    private static int ParseId(string? value) => int.TryParse(value, out var id) ? id : 0;

    private static string FriendlyEntityType(string entityType) => entityType switch
    {
        "UserAccount" => "User Account",
        "UserAccountRole" => "User Role",
        "TicketCategory" => "Ticket Category",
        "SystemConfiguration" => "System Configuration",
        _ => string.Concat(entityType.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()))
    };

    private static string? HumanizeValue(string action, string? value,
        IReadOnlyDictionary<int, string> users)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (action.Contains("User", StringComparison.OrdinalIgnoreCase) &&
            bool.TryParse(value, out var enabled)) return enabled ? "Active" : "Inactive";
        if ((action is "Ticket Assigned" or "Ticket Reassigned" or "Ticket Unassigned") &&
            int.TryParse(value, out var userId)) return users.GetValueOrDefault(userId);
        if (value is "1" or "0") return null;
        return value;
    }

    private static string FriendlyDescription(string action, string entityName,
        string storedDescription, string? oldValue, string? newValue) => action switch
    {
        "User Created" => $"Created a {newValue ?? "new"} account for {entityName}.",
        "User Invitation Resent" => $"Resent the account invitation to {entityName}.",
        "User Deactivated" => $"{entityName}'s account was deactivated.",
        "User Reactivated" => $"{entityName}'s account was activated.",
        "Ticket Assigned" when newValue is not null => $"Assigned {entityName} to {newValue}.",
        "Ticket Reassigned" when newValue is not null && oldValue is not null =>
            $"Reassigned {entityName} from {oldValue} to {newValue}.",
        "Ticket Reassigned" when newValue is not null => $"Reassigned {entityName} to {newValue}.",
        "Ticket Unassigned" when oldValue is not null => $"Removed {oldValue} from {entityName}.",
        _ => storedDescription
    };
}
