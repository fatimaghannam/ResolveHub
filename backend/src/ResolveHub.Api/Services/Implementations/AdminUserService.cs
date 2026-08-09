using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;
using ResolveHub.Api.Settings;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AdminUserService(
    ApplicationDbContext dbContext,
    UserManager<UserAccount> userManager,
    IPasswordResetEmailSender emailSender,
    IOptions<FrontendSettings> frontendOptions,
    ILogger<AdminUserService> logger) : IAdminUserService
{
    private readonly FrontendSettings _frontendSettings = frontendOptions.Value;

    public async Task<IReadOnlyCollection<AdminUserListItemDto>> GetUsersAsync(
        CancellationToken token) => (await GetUsersAsync(
            new AdminUserFilterDto(null, null, null, null), token)).Value!;

    public async Task<TicketServiceResult<IReadOnlyCollection<AdminUserListItemDto>>> GetUsersAsync(
        AdminUserFilterDto filter, CancellationToken token)
    {
        var roleFilter = filter.Role?.Trim();
        var statusFilter = filter.Status?.Trim();
        var hasDepartmentFilter = filter.DepartmentId is not null || filter.UnassignedDepartment;
        if (!string.IsNullOrEmpty(roleFilter) &&
            !RoleNames.All.Contains(roleFilter, StringComparer.Ordinal))
            return new(TicketOperationStatus.Invalid, Message: "The selected role is invalid.");
        if (!string.IsNullOrEmpty(statusFilter) &&
            statusFilter is not ("Active" or "Inactive" or "Pending"))
            return new(TicketOperationStatus.Invalid, Message: "The selected status is invalid.");
        if (hasDepartmentFilter && roleFilter != RoleNames.Manager)
            return new(TicketOperationStatus.Invalid,
                Message: "Department filtering is available only for Managers.");
        if (filter.DepartmentId is not null && filter.UnassignedDepartment)
            return new(TicketOperationStatus.Invalid,
                Message: "Select either a department or unassigned department.");
        if (filter.DepartmentId is not null && !await dbContext.Departments.AsNoTracking()
            .AnyAsync(item => item.ID == filter.DepartmentId && item.IsActive, token))
            return new(TicketOperationStatus.Invalid,
                Message: "The selected department is invalid.");

        var query = dbContext.Users.AsNoTracking().AsQueryable();
        var search = filter.Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(user =>
                user.FirstName.ToLower().Contains(search) ||
                user.LastName.ToLower().Contains(search) ||
                (user.Email != null && user.Email.ToLower().Contains(search)));
        if (!string.IsNullOrEmpty(roleFilter))
            query = query.Where(user => user.UserAccountRoles
                .Any(assignment => assignment.Role.Name == roleFilter));
        if (statusFilter == "Active")
            query = query.Where(user => user.IsActive && user.PasswordHash != null);
        if (statusFilter == "Inactive") query = query.Where(user => !user.IsActive);
        if (statusFilter == "Pending")
            query = query.Where(user => user.IsActive && user.PasswordHash == null);
        if (filter.DepartmentId is not null)
            query = query.Where(user => user.DepartmentID == filter.DepartmentId);
        if (filter.UnassignedDepartment)
            query = query.Where(user => user.DepartmentID == null);

        var users = await query
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Select(user => new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PasswordHash,
                Department = user.Department == null
                    ? null
                    : user.Department.Name,
                user.IsActive,
                user.CreatedDate
            })
            .ToListAsync(token);
        var roleRows = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            select new { userRole.UserId, RoleName = role.Name! })
            .ToListAsync(token);
        var primaryRoles = roleRows
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.RoleName)
                    .OrderBy(RolePriority)
                    .ThenBy(name => name)
                    .First());

        IReadOnlyCollection<AdminUserListItemDto> result = users.Select(user => new AdminUserListItemDto(
                user.Id, user.FirstName, user.LastName, user.Email!,
                primaryRoles.GetValueOrDefault(user.Id, "Unassigned"),
                user.Department,
                AccountStatus(user.IsActive, user.PasswordHash),
                user.CreatedDate))
            .ToList();
        return new(TicketOperationStatus.Success, result);
    }

    public async Task<AdminUserDetailsDto?> GetUserAsync(
        int userId, CancellationToken token)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new
            {
                item.Id, item.FirstName, item.LastName, item.Email,
                item.PasswordHash,
                Department = item.Department == null ? null : item.Department.Name,
                item.IsActive, item.CreatedDate, item.LastLoginDate,
                item.ProfileImagePath
            })
            .SingleOrDefaultAsync(token);
        if (user is null) return null;

        var role = await GetPrimaryRoleAsync(user.Id, token);
        return new AdminUserDetailsDto(user.Id, user.FirstName, user.LastName,
            user.Email!, role, user.Department,
            AccountStatus(user.IsActive, user.PasswordHash), user.CreatedDate,
            user.LastLoginDate, user.ProfileImagePath);
    }

    public async Task<IReadOnlyCollection<AdminDepartmentDto>> GetDepartmentsAsync(
        CancellationToken token) => await dbContext.Departments.AsNoTracking()
        .Where(item => item.IsActive)
        .OrderBy(item => item.Name)
        .Select(item => new AdminDepartmentDto(item.ID, item.Name))
        .ToListAsync(token);

    public async Task<TicketServiceResult<CreateAdminUserResultDto>> CreateUserAsync(
        int administratorId, CreateAdminUserRequestDto request, CancellationToken token)
    {
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var email = request.Email.Trim();
        var role = request.Role.Trim();
        if (firstName.Length == 0 || lastName.Length == 0 || email.Length == 0)
            return new(TicketOperationStatus.Invalid, Message: "All user fields are required.");
        if (!RoleNames.All.Contains(role, StringComparer.Ordinal))
            return new(TicketOperationStatus.Invalid, Message: "The selected role is invalid.");
        if (await userManager.FindByEmailAsync(email) is not null)
            return new(TicketOperationStatus.Conflict,
                Message: "A user with this email address already exists.");
        Department? department = null;
        if (role == RoleNames.Manager)
        {
            if (request.DepartmentId is null)
                return new(TicketOperationStatus.Invalid,
                    Message: "Department is required for Manager accounts.");
            department = await dbContext.Departments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ID == request.DepartmentId && item.IsActive, token);
            if (department is null)
                return new(TicketOperationStatus.NotFound,
                    Message: "The selected department is unavailable.");
        }

        var now = DateTime.UtcNow;
        var user = new UserAccount
        {
            UserName = $"user-{Guid.NewGuid():N}",
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            DepartmentID = department?.ID,
            IsActive = true,
            EmailConfirmed = true,
            CreatedDate = now
        };
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(token)
            : null;
        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
            return new(TicketOperationStatus.Invalid,
                Message: string.Join(" ", createResult.Errors.Select(item => item.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return new(TicketOperationStatus.Invalid,
                Message: "The selected role could not be assigned.");
        }

        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = administratorId,
            ActionType = "User Created",
            EntityType = "UserAccount",
            EntityID = user.Id.ToString(),
            Description = $"User account {user.Id} was created.",
            NewValue = role,
            CreatedDate = now
        });
        await dbContext.SaveChangesAsync(token);
        if (transaction is not null)
        {
            await transaction.CommitAsync(token);
            await transaction.DisposeAsync();
        }

        try
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));
            var resetUrl = QueryHelpers.AddQueryString(
                new Uri(new Uri(_frontendSettings.BaseUrl, UriKind.Absolute), "/reset-password").ToString(),
                new Dictionary<string, string?>
                {
                    ["email"] = email,
                    ["token"] = encodedToken,
                    ["setup"] = "true"
                });
            await emailSender.SendAccountInvitationEmailAsync(email,
                $"{firstName} {lastName}", role, department?.Name, resetUrl, token);
            user.UpdatedDate = DateTime.UtcNow;
            await userManager.UpdateAsync(user);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Account invitation email failed for newly created user {UserId}.", user.Id);
            return new(TicketOperationStatus.Success,
                new CreateAdminUserResultDto(
                    new AdminUserDetailsDto(user.Id, firstName, lastName, email, role,
                        department?.Name, "Pending", now, null, null), false));
        }

        return new(TicketOperationStatus.Success,
            new CreateAdminUserResultDto(
                new AdminUserDetailsDto(user.Id, firstName, lastName, email, role,
                    department?.Name, "Pending", now, null, null), true));
    }

    public async Task<TicketServiceResult<bool>> ResendInvitationAsync(
        int administratorId, int userId, CancellationToken token)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return new(TicketOperationStatus.NotFound);
        if (!user.IsActive || await userManager.HasPasswordAsync(user))
            return new(TicketOperationStatus.Conflict,
                Message: "Only Pending accounts can receive an invitation.");
        if (user.UpdatedDate is not null && DateTime.UtcNow - user.UpdatedDate < TimeSpan.FromSeconds(60))
            return new(TicketOperationStatus.Conflict,
                Message: "Please wait before sending another invitation.");

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
            return new(TicketOperationStatus.Invalid, Message: "A new invitation could not be generated.");
        try
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));
            var setupUrl = QueryHelpers.AddQueryString(
                new Uri(new Uri(_frontendSettings.BaseUrl, UriKind.Absolute), "/reset-password").ToString(),
                new Dictionary<string, string?>
                {
                    ["email"] = user.Email,
                    ["token"] = encodedToken,
                    ["setup"] = "true"
                });
            await emailSender.SendAccountInvitationEmailAsync(user.Email!,
                $"{user.FirstName} {user.LastName}",
                await GetPrimaryRoleAsync(user.Id, token),
                user.DepartmentID is null
                    ? null
                    : await dbContext.Departments.Where(item => item.ID == user.DepartmentID)
                        .Select(item => item.Name).SingleOrDefaultAsync(token),
                setupUrl, token);
            user.UpdatedDate = DateTime.UtcNow;
            await userManager.UpdateAsync(user);
            dbContext.ActivityLogs.Add(new ActivityLog
            {
                PerformedByUserAccountID = administratorId,
                ActionType = "User Invitation Resent",
                EntityType = "UserAccount",
                EntityID = user.Id.ToString(),
                Description = $"An account invitation was resent for user {user.Id}.",
                CreatedDate = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(token);
            return new(TicketOperationStatus.Success, true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Account invitation resend failed for user {UserId}.", user.Id);
            return new(TicketOperationStatus.Invalid, Message: "The invitation email could not be sent.");
        }
    }

    private static string AccountStatus(bool isActive, string? passwordHash) =>
        !isActive ? "Inactive" : string.IsNullOrEmpty(passwordHash) ? "Pending" : "Active";

    private async Task<string> GetPrimaryRoleAsync(int userId, CancellationToken token)
    {
        var roles = await (from assignment in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.UserId == userId
            select role.Name!).ToListAsync(token);
        return roles.OrderBy(RolePriority).ThenBy(name => name).FirstOrDefault() ?? "Unassigned";
    }

    private static int RolePriority(string roleName) =>
        roleName switch
        {
            RoleNames.Admin => 0,
            RoleNames.Manager => 1,
            RoleNames.ITSupportAgent => 2,
            RoleNames.Employee => 3,
            _ => 4
        };

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
