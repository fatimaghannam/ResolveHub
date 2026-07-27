using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class EmployeeTicketFlowTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task CreateTicket_UsesAuthenticatedOwnerAndOpenWorkflowDefaults()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "ticket-owner@resolvehub.test", Password);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);

        var ticket = await CreateTicketAsync(factory, client, "Laptop will not start");
        var stored = await factory.GetTicketSnapshotAsync(ticket.Id);

        Assert.Equal(employee.Id, stored.CreatedByUserAccountID);
        Assert.Equal(TicketStatusNames.Open, stored.StatusName);
        Assert.Null(stored.AssignedToUserAccountID);
        Assert.False(stored.IsDeleted);
        Assert.StartsWith($"RH-{DateTime.UtcNow.Year}-", ticket.TicketReferenceNumber);
        Assert.Matches(@"^RH-\d{4}-\d{4,}$", ticket.TicketReferenceNumber);
    }

    [Fact]
    public async Task Administrator_CanCreateTicketAsAuthenticatedRequester()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var administrator = await factory.CreateUserAsync(
            "admin-ticket-creator@resolvehub.test", Password, RoleNames.Admin);
        using var client = await CreateEmployeeClientAsync(factory, administrator.Email!);

        var categories = await client.GetAsync("/api/ticket-categories");
        var priorities = await client.GetAsync("/api/ticket-priorities");
        var ticket = await CreateTicketAsync(
            factory, client, "Administrator support request");
        var stored = await factory.GetTicketSnapshotAsync(ticket.Id);

        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        Assert.Equal(HttpStatusCode.OK, priorities.StatusCode);
        Assert.Equal(administrator.Id, stored.CreatedByUserAccountID);
        Assert.Equal(TicketStatusNames.Open, stored.StatusName);
        Assert.Null(stored.AssignedToUserAccountID);
    }

    [Fact]
    public async Task ItSupportAgent_CannotCreateTicketsOrLoadCreationLookups()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var agent = await factory.CreateUserAsync(
            "agent-ticket-creator@resolvehub.test", Password, RoleNames.ITAgent);
        using var client = await CreateEmployeeClientAsync(factory, agent.Email!);
        var lookups = await factory.GetTicketLookupIdsAsync();

        var createResponse = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Agent should not create this ticket",
            description = "IT Support Agents must not have ticket creation access.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        var lookupResponse = await client.GetAsync("/api/ticket-categories");

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, lookupResponse.StatusCode);
    }

    [Fact]
    public async Task TicketQueries_ReturnOnlyAuthenticatedEmployeesOwnTickets()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var first = await factory.CreateUserAsync("first@resolvehub.test", Password);
        var second = await factory.CreateUserAsync("second@resolvehub.test", Password);
        using var firstClient = await CreateEmployeeClientAsync(factory, first.Email!);
        using var secondClient = await CreateEmployeeClientAsync(factory, second.Email!);
        var own = await CreateTicketAsync(factory, firstClient, "First employee ticket");
        var other = await CreateTicketAsync(factory, secondClient, "Second employee ticket");

        var page = await firstClient.GetFromJsonAsync<PagedResultDto<TicketListItemDto>>(
            "/api/tickets");
        var otherDetails = await firstClient.GetAsync($"/api/tickets/{other.Id}");

        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(own.Id, page.Items.Single().Id);
        Assert.Equal(HttpStatusCode.NotFound, otherDetails.StatusCode);
    }

    [Fact]
    public async Task TicketList_AppliesSearchLookupDateSortingAndPagination()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("filters@resolvehub.test", Password);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);
        var first = await CreateTicketAsync(factory, client, "Unique printer failure");
        await CreateTicketAsync(factory, client, "Unrelated software request");
        await factory.SetTicketCreatedDateAsync(
            first.Id, new DateTime(2026, 7, 20, 14, 0, 0, DateTimeKind.Utc));
        var lookups = await factory.GetTicketLookupIdsAsync();

        var page = await client.GetFromJsonAsync<PagedResultDto<TicketListItemDto>>(
            $"/api/tickets?search={Uri.EscapeDataString(first.TicketReferenceNumber)}" +
            $"&categoryId={lookups.CategoryId}&priorityId={lookups.PriorityId}" +
            "&fromUtc=2026-07-20T00%3A00%3A00Z" +
            "&toUtcExclusive=2026-07-21T00%3A00%3A00Z&page=1&pageSize=1" +
            "&sortBy=title&sortDirection=asc");

        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal(first.Id, page.Items.Single().Id);
        Assert.Equal(1, page.TotalItems);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task TicketList_UsesInclusiveExclusiveUtcBoundaries()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "utc-boundaries@resolvehub.test", Password);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);

        var exactStart = await CreateTicketAsync(factory, client, "Exact UTC range start");
        var localToday = await CreateTicketAsync(factory, client, "Local today ticket");
        var beforeEnd = await CreateTicketAsync(factory, client, "Before UTC range end");
        var beforeStart = await CreateTicketAsync(factory, client, "Before UTC range start");
        var exactEnd = await CreateTicketAsync(factory, client, "Exact UTC range end");

        var fromUtc = new DateTime(2026, 7, 25, 21, 0, 0, DateTimeKind.Utc);
        var toUtcExclusive = new DateTime(2026, 7, 26, 21, 0, 0, DateTimeKind.Utc);
        await factory.SetTicketCreatedDateAsync(exactStart.Id, fromUtc);
        await factory.SetTicketCreatedDateAsync(
            localToday.Id,
            new DateTime(2026, 7, 25, 21, 30, 0, DateTimeKind.Utc));
        await factory.SetTicketCreatedDateAsync(beforeEnd.Id, toUtcExclusive.AddTicks(-1));
        await factory.SetTicketCreatedDateAsync(beforeStart.Id, fromUtc.AddTicks(-1));
        await factory.SetTicketCreatedDateAsync(exactEnd.Id, toUtcExclusive);

        var page = await client.GetFromJsonAsync<PagedResultDto<TicketListItemDto>>(
            "/api/tickets?fromUtc=2026-07-25T21%3A00%3A00Z" +
            "&toUtcExclusive=2026-07-26T21%3A00%3A00Z");

        Assert.NotNull(page);
        Assert.Equal(3, page.TotalItems);
        Assert.Contains(page.Items, ticket => ticket.Id == exactStart.Id);
        Assert.Contains(page.Items, ticket => ticket.Id == localToday.Id);
        Assert.Contains(page.Items, ticket => ticket.Id == beforeEnd.Id);
        Assert.DoesNotContain(page.Items, ticket => ticket.Id == beforeStart.Id);
        Assert.DoesNotContain(page.Items, ticket => ticket.Id == exactEnd.Id);
    }

    [Theory]
    [InlineData("?fromUtc=2026-07-25T21%3A00%3A00Z")]
    [InlineData("?toUtcExclusive=2026-07-26T21%3A00%3A00Z")]
    [InlineData(
        "?fromUtc=2026-07-26T21%3A00%3A00Z" +
        "&toUtcExclusive=2026-07-25T21%3A00%3A00Z")]
    public async Task TicketList_RejectsIncompleteOrInvalidUtcRanges(string query)
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            $"invalid-range-{Guid.NewGuid():N}@resolvehub.test", Password);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);

        var response = await client.GetAsync($"/api/tickets{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenUnassignedTicket_CanBeUpdated()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("update@resolvehub.test", Password);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, client, "Original ticket title");
        var lookups = await factory.GetTicketLookupIdsAsync();

        var response = await client.PutAsJsonAsync($"/api/tickets/{ticket.Id}", new
        {
            title = "Updated ticket title",
            description = "Updated description with enough detail.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        var updated = await response.Content.ReadFromJsonAsync<TicketDetailsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Updated ticket title", updated!.Title);
    }

    [Theory]
    [InlineData(TicketStatusNames.Assigned)]
    [InlineData(TicketStatusNames.InProgress)]
    public async Task NonOpenTicket_CannotBeUpdated(string status)
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync($"update-{status.Replace(" ", "-")}@resolvehub.test", Password);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, client, "Restricted update ticket");
        await factory.SetTicketStateAsync(ticket.Id, status);
        var lookups = await factory.GetTicketLookupIdsAsync();

        var response = await client.PutAsJsonAsync($"/api/tickets/{ticket.Id}", new
        {
            title = "Attempted update",
            description = "This update should not be accepted.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CancelTicket_SoftDeletesItAndRemovesItFromEmployeeViews()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("cancel@resolvehub.test", Password);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, client, "Ticket to cancel");

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/cancel",
            new { reason = "No longer required." });
        var list = await client.GetFromJsonAsync<PagedResultDto<TicketListItemDto>>(
            "/api/tickets");
        var dashboard = await client.GetFromJsonAsync<TicketDashboardSummaryDto>(
            "/api/employee/dashboard");
        var stored = await factory.GetTicketSnapshotAsync(ticket.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(list!.Items);
        Assert.Equal(0, dashboard!.TotalTickets);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.CancelledDate);
        Assert.Equal("No longer required.", stored.CancelledReason);
    }

    [Fact]
    public async Task AssignedTicket_CannotBeCancelled()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("assigned-cancel@resolvehub.test", Password);
        var agent = await factory.CreateUserAsync(
            "assigned-agent@resolvehub.test", Password, RoleNames.ITAgent);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, client, "Assigned ticket");
        await factory.SetTicketStateAsync(ticket.Id, TicketStatusNames.Assigned, agent.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/cancel", new { reason = "Cancel attempt." });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DashboardCountsOnlyOwnedNonDeletedTicketsByStatus()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("dashboard@resolvehub.test", Password);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);
        var open = await CreateTicketAsync(factory, client, "Open dashboard ticket");
        var progress = await CreateTicketAsync(factory, client, "Progress dashboard ticket");
        var resolved = await CreateTicketAsync(factory, client, "Resolved dashboard ticket");
        await factory.SetTicketStateAsync(progress.Id, TicketStatusNames.InProgress);
        await factory.SetTicketStateAsync(resolved.Id, TicketStatusNames.Resolved);

        var dashboard = await client.GetFromJsonAsync<TicketDashboardSummaryDto>(
            "/api/employee/dashboard");

        Assert.NotNull(dashboard);
        Assert.Equal(3, dashboard.TotalTickets);
        Assert.Equal(1, dashboard.OpenTickets);
        Assert.Equal(1, dashboard.InProgressTickets);
        Assert.Equal(1, dashboard.ResolvedTickets);
        Assert.Equal(3, dashboard.RecentTickets.Count);
    }

    [Theory]
    [InlineData(99999, 1)]
    [InlineData(1, 99999)]
    public async Task CreateTicket_RejectsInvalidLookupIds(int categoryId, int priorityId)
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            $"invalid-{categoryId}-{priorityId}@resolvehub.test", Password);
        using var client = await CreateEmployeeClientAsync(factory, employee.Email!);

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Invalid lookup ticket",
            description = "The lookup identifiers should be rejected.",
            ticketCategoryId = categoryId,
            ticketPriorityId = priorityId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TicketEndpoints_RequireAuthenticationAndEmployeeRole()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        using var anonymous = factory.CreateHttpsClient();
        var manager = await factory.CreateUserAsync(
            "manager-tickets@resolvehub.test", Password, RoleNames.Manager);
        using var managerClient = await CreateEmployeeClientAsync(
            factory, manager.Email!);

        var unauthenticated = await anonymous.GetAsync("/api/tickets");
        var forbidden = await managerClient.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private static async Task<HttpClient> CreateEmployeeClientAsync(
        ResolveHubApiFactory factory,
        string email)
    {
        var client = factory.CreateHttpsClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = Password
        });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private static async Task<TicketDetailsDto> CreateTicketAsync(
        ResolveHubApiFactory factory,
        HttpClient client,
        string title)
    {
        var lookups = await factory.GetTicketLookupIdsAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title,
            description = "A detailed ticket description for integration testing.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDetailsDto>())!;
    }
}
