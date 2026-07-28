using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class OrganizationalTicketFilteringTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task AdminAndManagerFilters_AreSqlBackedCombinedInclusiveAndDuplicateFree()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var administrator = await factory.CreateUserAsync(
            "filter-admin@resolvehub.test", Password, RoleNames.Admin);
        var manager = await factory.CreateUserAsync(
            "filter-manager@resolvehub.test", Password, RoleNames.Manager);
        var requester = await factory.CreateUserAsync(
            "olivia-filter@resolvehub.test", Password, RoleNames.Employee);
        var otherRequester = await factory.CreateUserAsync(
            "daniel-filter@resolvehub.test", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "natalie-filter@resolvehub.test", Password, RoleNames.ITSupportAgent);
        await SetNamesAsync(factory, requester.Id, "Olivia", "Bennett");
        await SetNamesAsync(factory, otherRequester.Id, "Daniel", "Brooks");
        await SetNamesAsync(factory, agent.Id, "Natalie", "Hayes");
        using var adminClient = await LoginAsync(factory, administrator.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var requesterClient = await LoginAsync(factory, requester.Email!);
        using var otherClient = await LoginAsync(factory, otherRequester.Email!);
        var target = await CreateTicketAsync(
            factory, requesterClient, "Conference room network outage");
        var unrelated = await CreateTicketAsync(
            factory, otherClient, "Payroll software question");
        await factory.SetTicketCreatedDateAsync(
            target.Id, new DateTime(2026, 7, 28, 23, 59, 59, DateTimeKind.Utc));
        await factory.SetTicketCreatedDateAsync(
            unrelated.Id, new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc));
        var assignmentFilter = await adminClient.GetFromJsonAsync<
            AdminAssignmentOverviewDto>(
            "/api/admin/ticket-assignments?search=conference" +
            "&fromUtc=2026-07-28T00%3A00%3A00Z" +
            "&toUtcExclusive=2026-07-29T00%3A00%3A00Z");
        var managerAssignmentFilter = await managerClient.GetFromJsonAsync<
            AdminAssignmentOverviewDto>(
            "/api/manager/assignments?search=conference");
        Assert.Equal(target.Id,
            Assert.Single(assignmentFilter!.UnassignedTickets).Id);
        Assert.Equal(target.Id,
            Assert.Single(managerAssignmentFilter!.UnassignedTickets).Id);
        var assign = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{target.TicketReferenceNumber}/assign",
            new { agentUserId = agent.Id });
        assign.EnsureSuccessStatusCode();
        var lookupIds = await factory.GetTicketLookupIdsAsync();
        var assignedStatusId = await StatusIdAsync(factory, TicketStatusNames.Assigned);

        var byReference = await GetAsync(adminClient,
            $"search={Uri.EscapeDataString(target.TicketReferenceNumber[3..])}");
        var byTitle = await GetAsync(adminClient, "search=network%20outage");
        var byRequester = await GetAsync(adminClient, "search=Olivia%20Bennett");
        var byAgent = await GetAsync(adminClient, "search=Natalie%20Hayes");
        var specificAgent = await GetAsync(adminClient, $"agentUserId={agent.Id}");
        var requesterId = await GetAsync(adminClient, $"requesterId={requester.Id}");
        var assigned = await GetAsync(adminClient, "assignedOnly=true");
        var unassigned = await GetAsync(adminClient, "unassignedOnly=true");
        var inclusiveDate = await GetAsync(adminClient,
            "fromDate=2026-07-28&toDate=2026-07-28");
        var combined = await GetAsync(managerClient,
            $"search=conference&agentUserId={agent.Id}&requesterId={requester.Id}" +
            $"&statusId={assignedStatusId}&categoryId={lookupIds.CategoryId}" +
            $"&priorityId={lookupIds.PriorityId}" +
            "&fromDate=2026-07-28&toDate=2026-07-28&page=1&pageSize=1" +
            "&sortBy=title&sortDirection=asc", "manager");

        foreach (var result in new[]
                 {
                     byReference, byTitle, byRequester, byAgent, specificAgent,
                     requesterId, inclusiveDate, combined
                 })
        {
            Assert.Equal(target.Id, Assert.Single(result.Items).Id);
            Assert.Equal(result.Items.Count, result.Items.Select(item => item.Id).Distinct().Count());
            Assert.Equal(1, result.TotalItems);
        }
        Assert.Contains(assigned.Items, item => item.Id == target.Id);
        Assert.DoesNotContain(assigned.Items, item => item.Id == unrelated.Id);
        Assert.Contains(unassigned.Items, item => item.Id == unrelated.Id);
        Assert.DoesNotContain(unassigned.Items, item => item.Id == target.Id);
        Assert.Equal(1, combined.TotalPages);
    }

    [Fact]
    public async Task AgentCanLoadActiveLookupOptions_WithoutSeeingOtherAgentsTickets()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "agent-filter-requester@resolvehub.test", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "filter-agent-one@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var otherAgent = await factory.CreateUserAsync(
            "filter-agent-two@resolvehub.test", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        var own = await CreateTicketAsync(factory, employeeClient, "Assigned to first agent");
        var other = await CreateTicketAsync(factory, employeeClient, "Assigned to second agent");
        await factory.SetTicketStateAsync(own.Id, TicketStatusNames.InProgress, agent.Id);
        await factory.SetTicketStateAsync(other.Id, TicketStatusNames.Assigned, otherAgent.Id);

        var categories = await agentClient.GetAsync("/api/ticket-categories");
        var priorities = await agentClient.GetAsync("/api/ticket-priorities");
        var statuses = await agentClient.GetAsync("/api/ticket-statuses");
        var list = await agentClient.GetFromJsonAsync<
            PagedResultDto<AgentTicketListItemDto>>("/api/agent/tickets");
        var lookupIds = await factory.GetTicketLookupIdsAsync();
        var progressId = await StatusIdAsync(factory, TicketStatusNames.InProgress);
        var filtered = await agentClient.GetFromJsonAsync<
            PagedResultDto<AgentTicketListItemDto>>(
            $"/api/agent/tickets?statusId={progressId}" +
            $"&categoryId={lookupIds.CategoryId}&priorityId={lookupIds.PriorityId}");

        categories.EnsureSuccessStatusCode();
        priorities.EnsureSuccessStatusCode();
        statuses.EnsureSuccessStatusCode();
        Assert.Equal(own.Id, Assert.Single(list!.Items).Id);
        Assert.DoesNotContain(list.Items, item => item.Id == other.Id);
        Assert.Equal(own.Id, Assert.Single(filtered!.Items).Id);
    }

    private static Task<PagedResultDto<AdminTicketListItemDto>> GetAsync(
        HttpClient client, string query, string roleArea = "admin") =>
        client.GetFromJsonAsync<PagedResultDto<AdminTicketListItemDto>>(
            $"/api/{roleArea}/tickets?{query}")!;

    private static async Task<TicketDetailsDto> CreateTicketAsync(
        ResolveHubApiFactory factory, HttpClient client, string title)
    {
        var lookups = await factory.GetTicketLookupIdsAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title,
            description = "Filtering integration test support request.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDetailsDto>())!;
    }

    private static async Task SetNamesAsync(
        ResolveHubApiFactory factory, int userId, string firstName, string lastName)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Users.SingleAsync(item => item.Id == userId);
        user.FirstName = firstName;
        user.LastName = lastName;
        await context.SaveChangesAsync();
    }

    private static async Task<int> StatusIdAsync(
        ResolveHubApiFactory factory, string name)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.TicketStatuses
            .Where(status => status.Name == name)
            .Select(status => status.ID)
            .SingleAsync();
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
