using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Tickets;
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
