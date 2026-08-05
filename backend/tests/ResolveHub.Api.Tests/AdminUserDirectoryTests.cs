using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Data;
using ResolveHub.Api.Data.Seed;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.Entities;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class AdminUserDirectoryTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task UserWithCanonicalAndLegacyAgentRoles_AppearsOnceWithCanonicalRole()
    {
        await using var factory = new ResolveHubApiFactory();
        var administrator = await factory.CreateUserAsync(
            "users-admin@resolvehub.test", Password, RoleNames.Admin);
        var agent = await factory.CreateUserAsync(
            "users-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);

        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
            var legacyRole = "ITAgent";
            Assert.True((await roleManager.CreateAsync(new Role
            {
                Name = legacyRole,
                IsActive = true,
                IsSystemRole = false
            })).Succeeded);
            var trackedAgent = await userManager.FindByIdAsync(agent.Id.ToString());
            Assert.NotNull(trackedAgent);
            Assert.True((await userManager.AddToRoleAsync(trackedAgent, legacyRole)).Succeeded);
        }

        using var client = factory.CreateHttpsClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = administrator.Email, password = Password });
        var auth = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var users = await client.GetFromJsonAsync<
            IReadOnlyCollection<AdminUserListItemDto>>("/api/admin/users");
        var rows = users!.Where(item => item.Id == agent.Id).ToList();

        Assert.Single(rows);
        Assert.Equal(RoleNames.ITSupportAgent, rows[0].Role);
    }

    [Fact]
    public async Task Administrator_CanCreateAndRetrieveRealUser_WithSetupEmail()
    {
        await using var factory = new ResolveHubApiFactory();
        var administrator = await factory.CreateUserAsync(
            "create-admin@resolvehub.test", Password, RoleNames.Admin);
        await factory.CreateUserAsync(
            "role-seed@resolvehub.test", Password, RoleNames.Employee);
        await factory.CreateUserAsync(
            "manager-role-seed@resolvehub.test", Password, RoleNames.Manager);
        int departmentId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Departments.Add(new Department { Name = "Existing Department", IsActive = true });
            await context.SaveChangesAsync();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            await ProductionSeeder.SeedAsync(context, roleManager);
            await ProductionSeeder.SeedAsync(context, roleManager);
            Assert.Equal(11, await context.Departments.CountAsync());
            departmentId = await context.Departments
                .Where(item => item.Name == "Operations")
                .Select(item => item.ID)
                .SingleAsync();
        }

        using var client = factory.CreateHttpsClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = administrator.Email, password = Password });
        var auth = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var departments = await client.GetFromJsonAsync<
            IReadOnlyCollection<AdminDepartmentDto>>("/api/admin/users/departments");
        Assert.Equal(11, departments!.Count);
        Assert.Equal(departments.OrderBy(item => item.Name).Select(item => item.Name),
            departments.Select(item => item.Name));

        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            firstName = "  Jamie ", lastName = " Rivera  ",
            email = "jamie.rivera@resolvehub.test",
            departmentId, role = RoleNames.Manager
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var creation = await response.Content.ReadFromJsonAsync<CreateAdminUserResultDto>();
        Assert.NotNull(creation);
        Assert.True(creation.InvitationSent);
        var created = creation.User;
        Assert.Equal("Jamie", created.FirstName);
        Assert.Equal("Rivera", created.LastName);
        Assert.Equal("Operations", created.Department);
        Assert.Equal("Pending ", created.Status);
        Assert.Single(factory.EmailSender.Messages);
        Assert.Contains("setup=true", factory.EmailSender.Messages.Single().ResetUrl);
        var details = await client.GetFromJsonAsync<AdminUserDetailsDto>(
            $"/api/admin/users/{created.Id}");
        Assert.Equal(created, details);
        var departmentManagers = await client.GetFromJsonAsync<
            IReadOnlyCollection<AdminUserListItemDto>>(
            $"/api/admin/users?role=Manager&departmentId={departmentId}&status=Pending%20Setup&search=Jamie");
        Assert.Single(departmentManagers!);
        Assert.Equal(created.Id, departmentManagers!.Single().Id);
        var unassignedManagers = await client.GetFromJsonAsync<
            IReadOnlyCollection<AdminUserListItemDto>>(
            "/api/admin/users?role=Manager&unassignedDepartment=true");
        Assert.Contains(unassignedManagers!, item =>
            item.Email == "manager-role-seed@resolvehub.test");
        var invalidCombination = await client.GetAsync(
            $"/api/admin/users?role=Employee&departmentId={departmentId}");
        Assert.Equal(HttpStatusCode.BadRequest, invalidCombination.StatusCode);

        using var verificationScope = factory.Services.CreateScope();
        var stored = await verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>().Users
            .SingleAsync(item => item.Id == created.Id);
        Assert.Null(stored.PasswordHash);
        Assert.True(stored.IsActive);

        var pendingLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = created.Email,
            password = "NotSetYet1!"
        });
        Assert.Equal(HttpStatusCode.Forbidden, pendingLogin.StatusCode);
        var invitationUri = new Uri(factory.EmailSender.Messages.Single().ResetUrl);
        var invitationQuery = QueryHelpers.ParseQuery(invitationUri.Query);
        var setupRequest = new
        {
            email = created.Email,
            token = invitationQuery["token"].ToString(),
            newPassword = "CreatedPassword1!",
            confirmPassword = "CreatedPassword1!",
            isAccountSetup = true
        };
        var setup = await client.PostAsJsonAsync("/api/auth/reset-password", setupRequest);
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var reused = await client.PostAsJsonAsync("/api/auth/reset-password", setupRequest);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        var activeDetails = await client.GetFromJsonAsync<AdminUserDetailsDto>(
            $"/api/admin/users/{created.Id}");
        Assert.Equal("Active", activeDetails!.Status);
        var activeLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = created.Email,
            password = "CreatedPassword1!"
        });
        Assert.Equal(HttpStatusCode.OK, activeLogin.StatusCode);

        var missingDepartment = await client.PostAsJsonAsync("/api/admin/users", new
        {
            firstName = "Missing", lastName = "Department",
            email = "missing.department@resolvehub.test",
            role = RoleNames.Manager
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingDepartment.StatusCode);

        var employeeResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            firstName = "No", lastName = "Department",
            email = "no.department@resolvehub.test",
            departmentId, role = RoleNames.Employee
        });
        Assert.Equal(HttpStatusCode.Created, employeeResponse.StatusCode);
        var employeeCreated = (await employeeResponse.Content
            .ReadFromJsonAsync<CreateAdminUserResultDto>())!.User;
        Assert.Null(employeeCreated!.Department);

        foreach (var roleName in new[] { RoleNames.ITSupportAgent, RoleNames.Admin })
        {
            var roleSeed = await factory.CreateUserAsync(
                $"{roleName.Replace(" ", "-").ToLowerInvariant()}-seed@resolvehub.test",
                Password, roleName);
            Assert.NotNull(roleSeed);
            var emailPrefix = roleName == RoleNames.Admin ? "created-admin" : "created-agent";
            var responseForRole = await client.PostAsJsonAsync("/api/admin/users", new
            {
                firstName = "No", lastName = "Department",
                email = $"{emailPrefix}@resolvehub.test",
                departmentId, role = roleName
            });
            Assert.Equal(HttpStatusCode.Created, responseForRole.StatusCode);
            var createdForRole = (await responseForRole.Content
                .ReadFromJsonAsync<CreateAdminUserResultDto>())!.User;
            Assert.Null(createdForRole!.Department);
            var storedForRole = await verificationScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>().Users
                .SingleAsync(item => item.Id == createdForRole.Id);
            Assert.Null(storedForRole.DepartmentID);
        }

        factory.EmailSender.ExceptionToThrow = new InvalidOperationException("Provider failure");
        var failedInvitationResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            firstName = "Pending", lastName = "Invitation",
            email = "pending.invitation@resolvehub.test",
            role = RoleNames.Employee
        });
        Assert.Equal(HttpStatusCode.Created, failedInvitationResponse.StatusCode);
        var failedInvitation = await failedInvitationResponse.Content
            .ReadFromJsonAsync<CreateAdminUserResultDto>();
        Assert.False(failedInvitation!.InvitationSent);
        Assert.Equal("Pending", failedInvitation.User.Status);
        Assert.NotNull(await factory.GetUserAsync("pending.invitation@resolvehub.test"));
        factory.EmailSender.ExceptionToThrow = null;
        var resend = await client.PostAsync(
            $"/api/admin/users/{failedInvitation.User.Id}/resend-invitation", null);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        var rapidResend = await client.PostAsync(
            $"/api/admin/users/{failedInvitation.User.Id}/resend-invitation", null);
        Assert.Equal(HttpStatusCode.Conflict, rapidResend.StatusCode);
    }

    [Fact]
    public async Task NonAdministrator_CannotAccessUserManagement()
    {
        await using var factory = new ResolveHubApiFactory();
        var employee = await factory.CreateUserAsync(
            "directory-employee@resolvehub.test", Password, RoleNames.Employee);
        using var client = factory.CreateHttpsClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = employee.Email, password = Password });
        var auth = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
