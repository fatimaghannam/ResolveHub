using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class TicketActivityEndpointTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task ExistingOpenTicket_ActivityAndSummary_AreReadableByAgent()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var owner = await factory.CreateUserAsync("activity-owner@test.local", Password);
        var agent = await factory.CreateUserAsync(
            "activity-agent@test.local", Password, RoleNames.ITSupportAgent);
        using var ownerClient = await LoginAsync(factory, owner.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        var lookups = await factory.GetTicketLookupIdsAsync();
        var create = await ownerClient.PostAsJsonAsync("/api/tickets", new
        {
            title = "Existing activity endpoint ticket",
            description = "A detailed ticket used to verify the activity endpoint flow.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        create.EnsureSuccessStatusCode();
        var ticket = (await create.Content.ReadFromJsonAsync<TicketDetailsDto>())!;

        var timelineResponse = await agentClient.GetAsync(
            $"/api/tickets/{ticket.TicketReferenceNumber}/activity");
        var summaryResponse = await agentClient.GetAsync(
            $"/api/tickets/{ticket.TicketReferenceNumber}/activity-summary");

        Assert.Equal(HttpStatusCode.OK, timelineResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        var timeline = await timelineResponse.Content
            .ReadFromJsonAsync<IReadOnlyCollection<TicketActivityDto>>();
        var summary = await summaryResponse.Content
            .ReadFromJsonAsync<TicketActivitySummaryDto>();
        Assert.Contains(timeline!, item =>
            item.ActivityType == TicketHistoryActionNames.TicketCreated);
        Assert.Equal(ticket.TicketReferenceNumber, summary!.TicketReferenceNumber);
        Assert.Equal(0, summary.TotalWorkMinutes);
        Assert.Equal("0m", summary.FormattedTotalWorkTime);
    }

    [Fact]
    public async Task ActivityEndpoints_DistinguishForbiddenFromMissingTicket()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var owner = await factory.CreateUserAsync("activity-private-owner@test.local", Password);
        var other = await factory.CreateUserAsync("activity-other@test.local", Password);
        using var ownerClient = await LoginAsync(factory, owner.Email!);
        using var otherClient = await LoginAsync(factory, other.Email!);
        var lookups = await factory.GetTicketLookupIdsAsync();
        var create = await ownerClient.PostAsJsonAsync("/api/tickets", new
        {
            title = "Private activity endpoint ticket",
            description = "A detailed ticket used to verify forbidden activity access.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        var ticket = (await create.Content.ReadFromJsonAsync<TicketDetailsDto>())!;

        Assert.Equal(HttpStatusCode.Forbidden,
            (await otherClient.GetAsync($"/api/tickets/{ticket.TicketReferenceNumber}/activity")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await ownerClient.GetAsync("/api/tickets/RH-2099-9999/activity")).StatusCode);
    }

    [Fact]
    public async Task MultiRoleAdministrator_CanReadCompleteActivityForEveryTicket()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var owner = await factory.CreateUserAsync("activity-admin-owner@test.local", Password);
        var administrator = await factory.CreateUserAsync(
            "activity-multirole-admin@test.local", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "activity-admin-agent@test.local", Password, RoleNames.ITSupportAgent);
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
                Assert.True((await roleManager.CreateAsync(new Role
                    { Name = RoleNames.Admin, IsActive = true, IsSystemRole = true })).Succeeded);
            var trackedAdministrator = await userManager.FindByIdAsync(
                administrator.Id.ToString());
            Assert.NotNull(trackedAdministrator);
            Assert.True((await userManager.AddToRoleAsync(
                trackedAdministrator!, RoleNames.Admin)).Succeeded);
        }
        using var ownerClient = await LoginAsync(factory, owner.Email!);
        using var adminClient = await LoginAsync(factory, administrator.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        var lookups = await factory.GetTicketLookupIdsAsync();
        var create = await ownerClient.PostAsJsonAsync("/api/tickets", new
        {
            title = "Administrator activity visibility ticket",
            description = "A ticket used to verify complete read-only Administrator activity access.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        create.EnsureSuccessStatusCode();
        var ticket = (await create.Content.ReadFromJsonAsync<TicketDetailsDto>())!;

        var adminTimelineResponse = await adminClient.GetAsync(
            $"/api/tickets/{ticket.TicketReferenceNumber}/activity");
        var adminSummaryResponse = await adminClient.GetAsync(
            $"/api/tickets/{ticket.TicketReferenceNumber}/activity-summary");
        var agentTimelineResponse = await agentClient.GetAsync(
            $"/api/tickets/{ticket.TicketReferenceNumber}/activity");

        Assert.Equal(HttpStatusCode.OK, adminTimelineResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminSummaryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, agentTimelineResponse.StatusCode);
        var adminTimeline = (await adminTimelineResponse.Content
            .ReadFromJsonAsync<IReadOnlyCollection<TicketActivityDto>>())!;
        var agentTimeline = (await agentTimelineResponse.Content
            .ReadFromJsonAsync<IReadOnlyCollection<TicketActivityDto>>())!;
        Assert.Equal(agentTimeline.Select(item => item.Id), adminTimeline.Select(item => item.Id));
    }

    private static async Task<HttpClient> LoginAsync(
        ResolveHubApiFactory factory, string email)
    {
        var client = factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }
}
