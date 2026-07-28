using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AdminUserService(
    ApplicationDbContext dbContext,
    UserManager<UserAccount> userManager) : IAdminUserService
{
    public async Task<IReadOnlyCollection<AdminUserListItemDto>> GetUsersAsync(
        CancellationToken token) =>
        await (from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            orderby user.FirstName, user.LastName
            select new AdminUserListItemDto(
                user.Id, user.FirstName, user.LastName, user.Email!,
                role.Name!, user.Department == null ? null : user.Department.Name,
                user.IsActive ? "Active" : "Inactive", user.CreatedDate))
            .ToListAsync(token);

    public async Task<TicketServiceResult<bool>> SetActiveAsync(
        int administratorId, int userId, bool isActive, CancellationToken token)
    {
        if (administratorId == userId && !isActive)
            return new(TicketOperationStatus.Invalid,
                Message: "You cannot deactivate your own account.");

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return new(TicketOperationStatus.NotFound);
        if (user.IsActive == isActive)
            return new(TicketOperationStatus.Conflict,
                Message: $"The account is already {(isActive ? "active" : "inactive")}.");

        if (!isActive && await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            var activeAdministrators = await (
                from account in dbContext.Users
                join assignment in dbContext.UserRoles on account.Id equals assignment.UserId
                join role in dbContext.Roles on assignment.RoleId equals role.Id
                where account.IsActive && role.Name == RoleNames.Admin
                select account.Id).CountAsync(token);
            if (activeAdministrators <= 1)
                return new(TicketOperationStatus.Conflict,
                    Message: "The final active Administrator cannot be deactivated.");
        }

        var now = DateTime.UtcNow;
        var previous = user.IsActive;
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(token)
            : null;
        user.IsActive = isActive;
        user.UpdatedDate = now;
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = administratorId,
            ActionType = isActive ? "User Reactivated" : "User Deactivated",
            EntityType = "UserAccount",
            EntityID = user.Id.ToString(),
            Description = isActive
                ? $"User account {user.Id} was reactivated."
                : $"User account {user.Id} was deactivated.",
            OldValue = previous.ToString(),
            NewValue = isActive.ToString(),
            CreatedDate = now
        });

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(token);
            return new(TicketOperationStatus.Invalid,
                Message: "The account status could not be updated.");
        }
        await dbContext.SaveChangesAsync(token);
        if (transaction is not null)
            await transaction.CommitAsync(token);
        return new(TicketOperationStatus.Success, true);
    }
}
