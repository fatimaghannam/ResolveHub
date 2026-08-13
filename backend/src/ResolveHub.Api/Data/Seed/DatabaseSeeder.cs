using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Entities;

namespace ResolveHub.Api.Data.Seed;

public static class DatabaseSeeder
{
    private static readonly string[] CompanyDepartments =
    [
        "Information Technology",
        "Human Resources",
        "Finance and Accounting",
        "Sales",
        "Marketing",
        "Operations",
        "Customer Support",
        "Administration",
        "Legal and Compliance",
        "Engineering"
    ];

    private static readonly SeedUser[] TestUsers =
    [
        new(
            Email: "ethan.brooks@resolvehub.test",
            FirstName: "Ethan",
            LastName: "Brooks",
            JobTitle: "Employee",
            RoleName: RoleNames.Employee),

        new(
            Email: "natalie.hayes@resolvehub.test",
            FirstName: "Natalie",
            LastName: "Hayes",
            JobTitle: "IT Support Agent",
            RoleName: RoleNames.ITSupportAgent),

        new(
            Email: "emily.carter@resolvehub.test",
            FirstName: "Emily",
            LastName: "Carter",
            JobTitle: "IT Support Agent",
            RoleName: RoleNames.ITSupportAgent),

        new(
            Email: "michael.thompson@resolvehub.test",
            FirstName: "Michael",
            LastName: "Thompson",
            JobTitle: "IT Support Agent",
            RoleName: RoleNames.ITSupportAgent),

        new(
            Email: "ryan.whitmore@resolvehub.test",
            FirstName: "Ryan",
            LastName: "Whitmore",
            JobTitle: "System Administrator",
            RoleName: RoleNames.Admin),

        new(
            Email: "lauren.prescott@resolvehub.test",
            FirstName: "Lauren",
            LastName: "Prescott",
            JobTitle: "Department Manager",
            RoleName: RoleNames.Manager)
    ];

    private static readonly SeedUser[] DevelopmentTicketRequesters =
    [
        new("olivia.bennett@resolvehub.test", "Olivia", "Bennett", "Employee", RoleNames.Employee),
        new("daniel.brooks@resolvehub.test", "Daniel", "Brooks", "Employee", RoleNames.Employee),
        new("sophia.mitchell@resolvehub.test", "Sophia", "Mitchell", "Employee", RoleNames.Employee),
        new("ethan.parker@resolvehub.test", "Ethan", "Parker", "Employee", RoleNames.Employee),
        new("ava.collins@resolvehub.test", "Ava", "Collins", "Employee", RoleNames.Employee),
        new("michael.reed@resolvehub.test", "Michael", "Reed", "Employee", RoleNames.Employee),
        new("jessica.morgan@resolvehub.test", "Jessica", "Morgan", "Employee", RoleNames.Employee),
        new("ryan.cooper@resolvehub.test", "Ryan", "Cooper", "Employee", RoleNames.Employee),
        new("hannah.foster@resolvehub.test", "Hannah", "Foster", "Employee", RoleNames.Employee),
        new("brandon.turner@resolvehub.test", "Brandon", "Turner", "Employee", RoleNames.Employee)
    ];

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        await using var scope = services.CreateAsyncScope();

        var serviceProvider = scope.ServiceProvider;

        var dbContext =
            serviceProvider.GetRequiredService<ApplicationDbContext>();

        var roleManager =
            serviceProvider.GetRequiredService<RoleManager<Role>>();

        var userManager =
            serviceProvider.GetRequiredService<UserManager<UserAccount>>();

        await dbContext.Database.MigrateAsync();

        await ProductionSeeder.SeedAsync(dbContext, roleManager);

        if (environment.IsDevelopment())
            await DemoDataSeeder.SeedAsync(
                dbContext,
                userManager,
                configuration);
    }

    internal static async Task SeedProductionDataAsync(
        ApplicationDbContext dbContext,
        RoleManager<Role> roleManager)
    {
        await SeedRolesAsync(roleManager);
        await NormalizeLegacyItSupportAgentRoleAsync(dbContext, roleManager);
        await SeedDepartmentsAsync(dbContext);
        await SeedTicketLookupsAsync(dbContext);
    }

    internal static async Task SeedDepartmentsAsync(ApplicationDbContext dbContext)
    {
        var existing = await dbContext.Departments
            .Select(item => item.Name)
            .ToListAsync();
        var existingNames = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in CompanyDepartments)
        {
            if (!existingNames.Add(name))
                continue;

            dbContext.Departments.Add(new Department
            {
                Name = name,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task NormalizeLegacyItSupportAgentRoleAsync(
        ApplicationDbContext dbContext,
        RoleManager<Role> roleManager)
    {
        const string legacyRoleName = "ITAgent";

        var canonicalRole = await roleManager.FindByNameAsync(
            RoleNames.ITSupportAgent)
            ?? throw new InvalidOperationException(
                "The canonical IT Support Agent role was not found.");
        var legacyRole = await dbContext.Roles.SingleOrDefaultAsync(
            role => role.Name == legacyRoleName);
        if (legacyRole is null || legacyRole.Id == canonicalRole.Id)
            return;

        var legacyAssignments = await dbContext.UserRoles
            .Where(assignment => assignment.RoleId == legacyRole.Id)
            .ToListAsync();
        var assignedCanonicalUserIds = await dbContext.UserRoles
            .Where(assignment => assignment.RoleId == canonicalRole.Id)
            .Select(assignment => assignment.UserId)
            .ToHashSetAsync();

        foreach (var assignment in legacyAssignments)
        {
            if (assignedCanonicalUserIds.Add(assignment.UserId))
            {
                dbContext.UserRoles.Add(new UserAccountRole
                {
                    UserId = assignment.UserId,
                    RoleId = canonicalRole.Id,
                    AssignedByUserAccountID =
                        assignment.AssignedByUserAccountID,
                    AssignedDate = assignment.AssignedDate
                });
            }
        }

        dbContext.UserRoles.RemoveRange(legacyAssignments);
        await dbContext.SaveChangesAsync();
        EnsureSucceeded(
            await roleManager.DeleteAsync(legacyRole),
            "removing the obsolete IT Agent role");
    }

    internal static async Task SeedDemoDataAsync(
        ApplicationDbContext dbContext,
        UserManager<UserAccount> userManager,
        IConfiguration configuration)
    {
        await RenameLegacyDemoUsersAsync(userManager);

        var defaultPassword =
            configuration["SeedData:DefaultPassword"]
            ?? throw new InvalidOperationException(
                "SeedData:DefaultPassword was not found in User Secrets.");

        await SeedUsersAsync(
            userManager,
            defaultPassword,
            TestUsers,
            includeEmailInErrors: true);
        await SeedUsersAsync(
            userManager,
            defaultPassword,
            DevelopmentTicketRequesters,
            includeEmailInErrors: true);
        var passwordResetTestEmail =
            configuration["SeedData:PasswordResetTestEmail"]?.Trim();

        if (string.IsNullOrWhiteSpace(passwordResetTestEmail))
            return;

        SeedUser[] passwordResetTestUsers =
        [
            new(
                Email: passwordResetTestEmail,
                FirstName: "Password",
                LastName: "Tester",
                JobTitle: "Employee",
                RoleName: RoleNames.Employee)
        ];

        await SeedUsersAsync(
            userManager,
            defaultPassword,
            passwordResetTestUsers,
            includeEmailInErrors: false);
    }

    private static async Task RenameLegacyDemoUsersAsync(
        UserManager<UserAccount> userManager)
    {
        LegacyDemoUser[] users =
        [
            new("employee@resolvehub.test", "ethan.brooks@resolvehub.test",
                "Ethan", "Brooks", "Employee"),
            new("agent@resolvehub.test", "natalie.hayes@resolvehub.test",
                "Natalie", "Hayes", "IT Support Agent"),
            new("admin@resolvehub.test", "ryan.whitmore@resolvehub.test",
                "Ryan", "Whitmore", "System Administrator"),
            new("manager@resolvehub.test", "lauren.prescott@resolvehub.test",
                "Lauren", "Prescott", "Department Manager"),
            new("Emily.Carter@company.com", "emily.carter@resolvehub.test",
                "Emily", "Carter", "IT Support Agent"),
            new("Michael.Thompson@company.com", "michael.thompson@resolvehub.test",
                "Michael", "Thompson", "IT Support Agent")
        ];

        foreach (var item in users)
        {
            var legacy = await userManager.FindByEmailAsync(item.LegacyEmail);
            if (legacy is null)
                continue;

            var canonical = await userManager.FindByEmailAsync(item.Email);
            if (canonical is not null && canonical.Id != legacy.Id)
            {
                legacy.IsActive = false;
                EnsureSucceeded(
                    await userManager.UpdateAsync(legacy),
                    "disabling a duplicate legacy demo user");
                continue;
            }

            EnsureSucceeded(
                await userManager.SetEmailAsync(legacy, item.Email),
                "standardizing a development user email");
            EnsureSucceeded(
                await userManager.SetUserNameAsync(legacy, item.Email),
                "standardizing a development username");
            legacy.FirstName = item.FirstName;
            legacy.LastName = item.LastName;
            legacy.JobTitle = item.JobTitle;
            legacy.EmailConfirmed = true;
            legacy.IsActive = true;
            EnsureSucceeded(
                await userManager.UpdateAsync(legacy),
                "updating a standardized development user");
        }
    }

    private static async Task SeedTicketLookupsAsync(
        ApplicationDbContext dbContext)
    {
        await SeedCategoriesAsync(dbContext);
        await SeedPrioritiesAsync(dbContext);
        await SeedStatusesAsync(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedCategoriesAsync(
        ApplicationDbContext dbContext)
    {
        string[] names =
        [
            "Hardware", "Software", "Network", "Email",
            "Access Request", "Security", "Other"
        ];

        var existing = await dbContext.TicketCategories
            .Select(item => item.Name)
            .ToListAsync();

        for (var index = 0; index < names.Length; index++)
        {
            if (existing.Contains(names[index], StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            dbContext.TicketCategories.Add(new TicketCategory
            {
                Name = names[index],
                SortOrder = index + 1,
                IsActive = true
            });
        }
    }

    private static async Task SeedPrioritiesAsync(
        ApplicationDbContext dbContext)
    {
        string[] names = ["Low", "Medium", "High", "Critical"];
        var existing = await dbContext.TicketPriorities
            .Select(item => item.Name)
            .ToListAsync();

        for (var index = 0; index < names.Length; index++)
        {
            if (existing.Contains(names[index], StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            dbContext.TicketPriorities.Add(new TicketPriority
            {
                Name = names[index],
                SortOrder = index + 1,
                IsActive = true
            });
        }
    }

    private static async Task SeedStatusesAsync(
        ApplicationDbContext dbContext)
    {
        var existing = await dbContext.TicketStatuses
            .Select(item => item.Name)
            .ToListAsync();

        for (var index = 0; index < TicketStatusNames.All.Length; index++)
        {
            var name = TicketStatusNames.All[index];
            if (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            dbContext.TicketStatuses.Add(new TicketStatus
            {
                Name = name,
                SortOrder = index + 1,
                IsFinalStatus =
                    name is TicketStatusNames.Closed or
                        TicketStatusNames.Cancelled or
                        TicketStatusNames.Duplicate,
                IsActive = true
            });
        }
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
        string defaultPassword,
        IEnumerable<SeedUser> seedUsers,
        bool includeEmailInErrors)
    {
        foreach (var seedUser in seedUsers)
        {
            var userDescription = includeEmailInErrors
                ? $"user '{seedUser.Email}'"
                : "password-reset test user";

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
                    $"creating {userDescription}");
            }
            else if (!user.LockoutEnabled)
            {
                user.LockoutEnabled = true;

                EnsureSucceeded(
                    await userManager.UpdateAsync(user),
                    $"enabling lockout for {userDescription}");
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
                    $"to {userDescription}");
            }
        }
    }

    private static string GetRoleDescription(string roleName)
    {
        return roleName switch
        {
            RoleNames.Employee =>
                "Creates and tracks IT support tickets.",

            RoleNames.ITSupportAgent =>
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

    private sealed record LegacyDemoUser(
        string LegacyEmail,
        string Email,
        string FirstName,
        string LastName,
        string JobTitle);
}
