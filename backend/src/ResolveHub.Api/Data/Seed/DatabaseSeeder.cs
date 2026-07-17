using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Entities;

namespace ResolveHub.Api.Data.Seed;

public static class DatabaseSeeder
{
    private static readonly SeedUser[] TestUsers =
    [
        new(
            Email: "employee@resolvehub.test",
            FirstName: "Test",
            LastName: "Employee",
            JobTitle: "Employee",
            RoleName: RoleNames.Employee),

        new(
            Email: "agent@resolvehub.test",
            FirstName: "Test",
            LastName: "Agent",
            JobTitle: "IT Support Agent",
            RoleName: RoleNames.ITAgent),

        new(
            Email: "admin@resolvehub.test",
            FirstName: "Test",
            LastName: "Administrator",
            JobTitle: "System Administrator",
            RoleName: RoleNames.Admin),

        new(
            Email: "manager@resolvehub.test",
            FirstName: "Test",
            LastName: "Manager",
            JobTitle: "Department Manager",
            RoleName: RoleNames.Manager)
    ];

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();

        var serviceProvider = scope.ServiceProvider;

        var dbContext =
            serviceProvider.GetRequiredService<ApplicationDbContext>();

        var roleManager =
            serviceProvider.GetRequiredService<RoleManager<Role>>();

        var userManager =
            serviceProvider.GetRequiredService<UserManager<UserAccount>>();

        // Apply pending migrations during local development.
        await dbContext.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);

        var defaultPassword =
            configuration["SeedData:DefaultPassword"]
            ?? throw new InvalidOperationException(
                "SeedData:DefaultPassword was not found in User Secrets.");

        await SeedUsersAsync(userManager, defaultPassword);
    }

    private static async Task SeedRolesAsync(
        RoleManager<Role> roleManager)
    {
        foreach (var roleName in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var role = new Role
            {
                Name = roleName,
                Description = GetRoleDescription(roleName),
                IsSystemRole = true,
                IsActive = true
            };

            var result = await roleManager.CreateAsync(role);

            EnsureSucceeded(
                result,
                $"creating the '{roleName}' role");
        }
    }

    private static async Task SeedUsersAsync(
        UserManager<UserAccount> userManager,
        string defaultPassword)
    {
        foreach (var seedUser in TestUsers)
        {
            var user =
                await userManager.FindByEmailAsync(seedUser.Email);

            if (user is null)
            {
                user = new UserAccount
                {
                    UserName = seedUser.Email,
                    Email = seedUser.Email,
                    EmailConfirmed = true,
                    FirstName = seedUser.FirstName,
                    LastName = seedUser.LastName,
                    JobTitle = seedUser.JobTitle,
                    IsActive = true,
                    LockoutEnabled = true,
                    CreatedDate = DateTime.UtcNow
                };

                var creationResult =
                    await userManager.CreateAsync(
                        user,
                        defaultPassword);

                EnsureSucceeded(
                    creationResult,
                    $"creating user '{seedUser.Email}'");
            }

            if (!await userManager.IsInRoleAsync(
                    user,
                    seedUser.RoleName))
            {
                var roleResult =
                    await userManager.AddToRoleAsync(
                        user,
                        seedUser.RoleName);

                EnsureSucceeded(
                    roleResult,
                    $"assigning role '{seedUser.RoleName}' " +
                    $"to '{seedUser.Email}'");
            }
        }
    }

    private static string GetRoleDescription(string roleName)
    {
        return roleName switch
        {
            RoleNames.Employee =>
                "Creates and tracks IT support tickets.",

            RoleNames.ITAgent =>
                "Handles assigned IT support tickets.",

            RoleNames.Admin =>
                "Manages users, roles, assignments, and system settings.",

            RoleNames.Manager =>
                "Reviews department tickets, reports, and performance.",

            _ => string.Empty
        };
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error => error.Description));

        throw new InvalidOperationException(
            $"An error occurred while {operation}: {errors}");
    }

    private sealed record SeedUser(
        string Email,
        string FirstName,
        string LastName,
        string JobTitle,
        string RoleName);
}