using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Tickets;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class AdminAgentDirectoryTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task Directory_ContainsOnlyActiveAgents_AndAssignmentUsesSelectedUserId()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var administrator = await factory.CreateUserAsync(
            "directory-admin@resolvehub.test", Password, RoleNames.Admin);
        var alpha = await factory.CreateUserAsync(
            "alpha-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var beta = await factory.CreateUserAsync(
            "beta-agent@resolvehub.test", Password, RoleNames.ITSupportAgent,
            isActive: false);
        var employee = await factory.CreateUserAsync(
            "directory-employee@resolvehub.test", Password, RoleNames.Employee);
        await factory.CreateUserAsync(
            "directory-manager@resolvehub.test", Password, RoleNames.Manager);

        using var adminClient = await LoginClientAsync(
            factory, administrator.Email!);
        var initial = await adminClient.GetFromJsonAsync<
            IReadOnlyCollection<AdminAgentWorkloadDto>>("/api/admin/users/agents");
        Assert.NotNull(initial);
        Assert.Contains(initial, item => item.UserId == alpha.Id);
        Assert.DoesNotContain(initial, item => item.UserId == beta.Id);
        Assert.DoesNotContain(initial, item => item.UserId == employee.Id);
        Assert.Equal(initial.Select(item => item.FirstName),
            initial.Select(item => item.FirstName).OrderBy(name => name));

        var reactivate = await adminClient.PatchAsJsonAsync(
            $"/api/admin/users/{beta.Id}/status", new { isActive = true });
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);
        var afterReactivation = await adminClient.GetFromJsonAsync<
            IReadOnlyCollection<AdminAgentWorkloadDto>>("/api/admin/users/agents");
        Assert.Contains(afterReactivation!, item => item.UserId == beta.Id);

        using var employeeClient = await LoginClientAsync(factory, employee.Email!);
        var lookups = await factory.GetTicketLookupIdsAsync();
        var create = await employeeClient.PostAsJsonAsync("/api/tickets", new
        {
            title = "Directory assignment verification",
            description = "Verify that assignment stores the selected IT Agent user ID.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        var ticket = await create.Content.ReadFromJsonAsync<TicketDetailsDto>();
        Assert.NotNull(ticket);

        var assign = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = alpha.Id });
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        var stored = await factory.GetTicketSnapshotAsync(ticket.Id);
        Assert.Equal(alpha.Id, stored.AssignedToUserAccountID);
    }

    private static async Task<HttpClient> LoginClientAsync(
        ResolveHubApiFactory factory, string email)
    {
        var client = factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }
}
