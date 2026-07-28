using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class PersonalTicketRoleTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task AdministratorAndManager_SeeOnlyOwnTickets_AndCannotModifyAssignedTickets()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var administrator = await factory.CreateUserAsync(
            "personal-admin@resolvehub.test", Password, RoleNames.Admin);
        var manager = await factory.CreateUserAsync(
            "personal-manager@resolvehub.test", Password, RoleNames.Manager);
        var agent = await factory.CreateUserAsync(
            "personal-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        using var adminClient = await LoginAsync(factory, administrator.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        var adminTicket = await CreateTicketAsync(factory, adminClient, "Administrator ticket");
        var managerTicket = await CreateTicketAsync(factory, managerClient, "Manager ticket");

        var adminMine = await adminClient.GetFromJsonAsync<
            PagedResultDto<TicketListItemDto>>("/api/tickets");
        var managerMine = await managerClient.GetFromJsonAsync<
            PagedResultDto<TicketListItemDto>>("/api/tickets");

        Assert.Single(adminMine!.Items);
        Assert.Equal(adminTicket.Id, adminMine.Items.Single().Id);
        Assert.True(adminMine.Items.Single().CanEdit);
        Assert.True(adminMine.Items.Single().CanDelete);
        Assert.Single(managerMine!.Items);
        Assert.Equal(managerTicket.Id, managerMine.Items.Single().Id);
        Assert.DoesNotContain(adminMine.Items, item => item.Id == managerTicket.Id);
        Assert.DoesNotContain(managerMine.Items, item => item.Id == adminTicket.Id);

        foreach (var ticket in new[] { adminTicket, managerTicket })
        {
            var assigned = await adminClient.PostAsJsonAsync(
                $"/api/admin/tickets/{ticket.TicketReferenceNumber}/assign",
                new { agentUserId = agent.Id });
            Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        }

        var adminUpdate = await adminClient.PutAsJsonAsync(
            $"/api/tickets/{adminTicket.Id}", UpdatePayload("Blocked admin update"));
        var adminDelete = await adminClient.PostAsJsonAsync(
            $"/api/tickets/{adminTicket.Id}/cancel", new { reason = "Not allowed" });
        var managerUpdate = await managerClient.PutAsJsonAsync(
            $"/api/tickets/{managerTicket.Id}", UpdatePayload("Blocked manager update"));
        var managerDelete = await managerClient.PostAsJsonAsync(
            $"/api/tickets/{managerTicket.Id}/cancel", new { reason = "Not allowed" });
        var adminAfterAssignment = await adminClient.GetFromJsonAsync<
            PagedResultDto<TicketListItemDto>>("/api/tickets");
        var managerAfterAssignment = await managerClient.GetFromJsonAsync<
            PagedResultDto<TicketListItemDto>>("/api/tickets");

        Assert.Equal(HttpStatusCode.Forbidden, adminUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerDelete.StatusCode);
        Assert.False(adminAfterAssignment!.Items.Single().CanEdit);
        Assert.False(adminAfterAssignment.Items.Single().CanDelete);
        Assert.False(managerAfterAssignment!.Items.Single().CanEdit);
        Assert.False(managerAfterAssignment.Items.Single().CanDelete);
    }

    private static object UpdatePayload(string title) => new
    {
        title,
        description = "This update must be rejected after ticket assignment.",
        ticketCategoryId = 1,
        ticketPriorityId = 1
    };

    private static async Task<TicketDetailsDto> CreateTicketAsync(
        ResolveHubApiFactory factory, HttpClient client, string title)
    {
        var lookups = await factory.GetTicketLookupIdsAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title,
            description = "A personal support request used to verify ownership.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDetailsDto>())!;
    }

    private static async Task<HttpClient> LoginAsync(
        ResolveHubApiFactory factory, string email)
    {
        var client = factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }
}
