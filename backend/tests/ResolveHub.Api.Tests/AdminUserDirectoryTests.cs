using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
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
}
