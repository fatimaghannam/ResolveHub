using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Tickets;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class ManagerWorkflowTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task Manager_CanCreateDraftAndTicket_WhileEmployeeCannotUseManagerApis()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "workflow-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "workflow-employee@resolvehub.test", Password, RoleNames.Employee);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);

        var draftResponse = await managerClient.PostAsJsonAsync(
            "/api/ticket-drafts", new { title = "Manager-owned draft" });
        var draft = await draftResponse.Content.ReadFromJsonAsync<TicketDraftDto>();
        var employeeDraftRead = await employeeClient.GetAsync(
            $"/api/ticket-drafts/{draft!.Id}");
        var employeeDashboard = await employeeClient.GetAsync("/api/manager/dashboard");
        var lookups = await factory.GetTicketLookupIdsAsync();
        var create = await managerClient.PostAsJsonAsync("/api/tickets", new
        {
            title = "Manager-created support request",
            description = "A real ticket submitted by an authenticated Manager.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });

        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, employeeDraftRead.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employeeDashboard.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task Manager_SharedTicketLookupsAndCompleteDraftWorkflow_AreAuthorized()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "shared-options-manager@resolvehub.test", Password, RoleNames.Manager);
        using var client = await LoginAsync(factory, manager.Email!);

        var categories = await client.GetAsync("/api/ticket-categories");
        var priorities = await client.GetAsync("/api/ticket-priorities");
        var statuses = await client.GetAsync("/api/ticket-statuses");
        var lookups = await factory.GetTicketLookupIdsAsync();
        var save = await client.PostAsJsonAsync("/api/ticket-drafts", new
        {
            title = "Manager draft",
            description = "Manager-owned draft ready for submission.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        var draft = await save.Content.ReadFromJsonAsync<TicketDraftDto>();
        var list = await client.GetAsync("/api/ticket-drafts");
        var read = await client.GetAsync($"/api/ticket-drafts/{draft!.Id}");
        var update = await client.PutAsJsonAsync(
            $"/api/ticket-drafts/{draft.Id}", new
            {
                title = "Updated manager draft",
                description = "Manager-owned draft ready for submission.",
                ticketCategoryId = lookups.CategoryId,
                ticketPriorityId = lookups.PriorityId
            });
        var submit = await client.PostAsync(
            $"/api/ticket-drafts/{draft.Id}/submit", null);
        var ticket = await submit.Content.ReadFromJsonAsync<TicketDetailsDto>();
        var details = await client.GetAsync(
            $"/api/manager/tickets/{ticket!.TicketReferenceNumber}");

        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        Assert.Equal(HttpStatusCode.OK, priorities.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statuses.StatusCode);
        Assert.Equal(HttpStatusCode.Created, save.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var snapshot = await factory.GetDraftSubmissionSnapshotAsync(
            ticket.Id, draft.Id);
        Assert.False(snapshot.DraftExists);
        Assert.Equal(manager.Id, snapshot.CreatorId);
    }

    [Fact]
    public async Task Manager_DashboardListsTickets_AndAssignsOnlyActiveAgents()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "assignment-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "assignment-requester@resolvehub.test", Password, RoleNames.Employee);
        var activeAgent = await factory.CreateUserAsync(
            "active-manager-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var inactiveAgent = await factory.CreateUserAsync(
            "inactive-manager-agent@resolvehub.test", Password,
            RoleNames.ITSupportAgent, isActive: false);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);

        var dashboard = await managerClient.GetFromJsonAsync<ManagerDashboardDto>(
            "/api/manager/dashboard");
        var invalid = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = inactiveAgent.Id });
        var assigned = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = activeAgent.Id });
        var snapshot = await factory.GetTicketSnapshotAsync(ticket.Id);

        Assert.NotNull(dashboard);
        Assert.Equal(1, dashboard.TotalTickets);
        Assert.Contains(dashboard.Unassigned,
            item => item.TicketReferenceNumber == ticket.TicketReferenceNumber);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        Assert.Equal(activeAgent.Id, snapshot.AssignedToUserAccountID);
    }

    private static async Task<TicketDetailsDto> CreateTicketAsync(
        ResolveHubApiFactory factory, HttpClient client)
    {
        var lookups = await factory.GetTicketLookupIdsAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Unassigned manager workflow ticket",
            description = "This ticket validates Manager visibility and assignment.",
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
