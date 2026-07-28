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
            RoleName: RoleNames.ITAgent),

        new(
            Email: "emily.carter@resolvehub.test",
            FirstName: "Emily",
            LastName: "Carter",
            JobTitle: "IT Support Agent",
            RoleName: RoleNames.ITAgent),

        new(
            Email: "michael.thompson@resolvehub.test",
            FirstName: "Michael",
            LastName: "Thompson",
            JobTitle: "IT Support Agent",
            RoleName: RoleNames.ITAgent),

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

        // Apply pending migrations during local development.
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
        await SeedTicketLookupsAsync(dbContext);
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
        await SeedDevelopmentAgentTicketsAsync(dbContext, userManager);

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

    private static async Task SeedDevelopmentAgentTicketsAsync(
        ApplicationDbContext dbContext,
        UserManager<UserAccount> userManager)
    {
        string[] agentEmails =
        [
            "natalie.hayes@resolvehub.test",
            "emily.carter@resolvehub.test",
            "michael.thompson@resolvehub.test"
        ];
        var agents = await dbContext.Users
            .Where(user => agentEmails.Contains(user.Email!))
            .ToDictionaryAsync(user => user.Email!, StringComparer.OrdinalIgnoreCase);

        var requesterEmails = DevelopmentTicketRequesters
            .Select(user => user.Email)
            .ToArray();
        var requesters = await dbContext.Users
            .Where(user => requesterEmails.Contains(user.Email!))
            .ToDictionaryAsync(user => user.Email!, StringComparer.OrdinalIgnoreCase);

        var categories = await dbContext.TicketCategories
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var priorities = await dbContext.TicketPriorities
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var statuses = await dbContext.TicketStatuses
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase);

        SeedAgentTicket[] seedTickets =
        [
            new("RH-2026-9001", "olivia.bennett@resolvehub.test", null,
                "VPN access unavailable after password change", "Network", "Critical",
                TicketStatusNames.Open, 2),
            new("RH-2026-9002", "daniel.brooks@resolvehub.test", null,
                "Finance shared printer is offline", "Hardware", "High",
                TicketStatusNames.Open, 3),
            new("RH-2026-9003", "sophia.mitchell@resolvehub.test", "emily.carter@resolvehub.test",
                "Suspicious email attachment reported", "Security", "Medium",
                TicketStatusNames.Assigned, 4),
            new("RH-2026-9004", "ethan.parker@resolvehub.test", "emily.carter@resolvehub.test",
                "Outlook calendar is not synchronizing", "Email", "Critical",
                TicketStatusNames.InProgress, 6),
            new("RH-2026-9005", "ava.collins@resolvehub.test", "emily.carter@resolvehub.test",
                "Intermittent Wi-Fi in conference room", "Network", "High",
                TicketStatusNames.InProgress, 7),
            new("RH-2026-9006", "michael.reed@resolvehub.test", "emily.carter@resolvehub.test",
                "Payroll application closes unexpectedly", "Software", "Medium",
                TicketStatusNames.InProgress, 9),
            new("RH-2026-9007", "jessica.morgan@resolvehub.test", "michael.thompson@resolvehub.test",
                "New employee workstation setup", "Hardware", "High",
                TicketStatusNames.Pending, 11),
            new("RH-2026-9008", "ryan.cooper@resolvehub.test", "michael.thompson@resolvehub.test",
                "Cannot open archived project folder", "Access Request", "Medium",
                TicketStatusNames.Pending, 13),
            new("RH-2026-9009", "hannah.foster@resolvehub.test", "michael.thompson@resolvehub.test",
                "Browser certificate warning on intranet", "Security", "Low",
                TicketStatusNames.Pending, 15),
            new("RH-2026-9010", "brandon.turner@resolvehub.test", "natalie.hayes@resolvehub.test",
                "Teams microphone is not detected", "Hardware", "Medium",
                TicketStatusNames.Resolved, 17),
            new("RH-2026-9011", "olivia.bennett@resolvehub.test", "natalie.hayes@resolvehub.test",
                "Distribution list delivery delayed", "Email", "Low",
                TicketStatusNames.Resolved, 20),
            new("RH-2026-9012", "daniel.brooks@resolvehub.test", "natalie.hayes@resolvehub.test",
                "Ethernet connection drops periodically", "Network", "Low",
                TicketStatusNames.Resolved, 23)
        ];

        var references = seedTickets
            .Select(ticket => ticket.Reference)
            .ToArray();
        var existingTickets = await dbContext.Tickets
            .Where(ticket => references.Contains(ticket.TicketReferenceNumber))
            .ToDictionaryAsync(
                ticket => ticket.TicketReferenceNumber,
                StringComparer.OrdinalIgnoreCase);

        foreach (var seedTicket in seedTickets)
        {
            var createdDate = new DateTime(
                2026, 7, seedTicket.Day, 8 + seedTicket.Day % 8, 0, 0,
                DateTimeKind.Utc);
            var assignedDate = createdDate.AddHours(2);
            var isResolved = seedTicket.Status == TicketStatusNames.Resolved;
            var resolvedDate = isResolved
                ? assignedDate.AddDays(1).AddHours(3)
                : (DateTime?)null;

            var ticket = existingTickets.GetValueOrDefault(seedTicket.Reference)
                ?? new Ticket { TicketReferenceNumber = seedTicket.Reference };
            var assignedAgent = seedTicket.AgentEmail is null
                ? null
                : agents[seedTicket.AgentEmail];
            ticket.CreatedByUserAccountID = requesters[seedTicket.RequesterEmail].Id;
            ticket.AssignedToUserAccountID = assignedAgent?.Id;
            ticket.TicketCategoryID = categories[seedTicket.Category].ID;
            ticket.TicketPriorityID = priorities[seedTicket.Priority].ID;
            ticket.TicketStatusID = statuses[seedTicket.Status].ID;
            ticket.Title = seedTicket.Title;
            ticket.Description =
                $"Development seed ticket for {seedTicket.Title.ToLowerInvariant()}.";
            ticket.CreatedDate = createdDate;
            ticket.UpdatedDate = resolvedDate ?? assignedDate;
            ticket.AssignedDate = assignedAgent is null ? null : assignedDate;
            ticket.ResolvedDate = resolvedDate;
            ticket.ResolutionSummary = isResolved
                ? "Issue resolved and confirmed with the requester."
                : null;
            ticket.ResolvedByUserAccountID = isResolved ? assignedAgent?.Id : null;
            ticket.IsDeleted = false;
            if (!existingTickets.ContainsKey(seedTicket.Reference))
                dbContext.Tickets.Add(ticket);
        }

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
                        TicketStatusNames.Cancelled,
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

    private sealed record SeedAgentTicket(
        string Reference,
        string RequesterEmail,
        string? AgentEmail,
        string Title,
        string Category,
        string Priority,
        string Status,
        int Day);

    private sealed record LegacyDemoUser(
        string LegacyEmail,
        string Email,
        string FirstName,
        string LastName,
        string JobTitle);
}
