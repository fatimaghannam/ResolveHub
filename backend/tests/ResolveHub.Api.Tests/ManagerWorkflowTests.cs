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
    public async Task Manager_PersonalTicketDraftAndWriteEndpoints_ReturnForbidden()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "workflow-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "workflow-employee@resolvehub.test", Password, RoleNames.Employee);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);
        var lookups = await factory.GetTicketLookupIdsAsync();

        var personalList = await managerClient.GetAsync("/api/tickets");
        var personalDetails = await managerClient.GetAsync(
            $"/api/tickets/{ticket.Id}");
        var create = await managerClient.PostAsJsonAsync("/api/tickets", new
        {
            title = "Manager-created support request",
            description = "Managers must not create personal support requests.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        var update = await managerClient.PutAsJsonAsync(
            $"/api/tickets/{ticket.Id}", new
            {
                title = "Manager update",
                description = "Managers must not update personal tickets.",
                ticketCategoryId = lookups.CategoryId,
                ticketPriorityId = lookups.PriorityId
            });
        var cancel = await managerClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/cancel",
            new { reason = "Managers must not cancel tickets." });
        var draftList = await managerClient.GetAsync("/api/ticket-drafts");
        var draftRead = await managerClient.GetAsync("/api/ticket-drafts/1");
        var draftCreate = await managerClient.PostAsJsonAsync(
            "/api/ticket-drafts", new { title = "Forbidden Manager draft" });
        var draftUpdate = await managerClient.PutAsJsonAsync(
            "/api/ticket-drafts/1", new { title = "Forbidden update" });
        var draftDelete = await managerClient.DeleteAsync("/api/ticket-drafts/1");
        var draftSubmit = await managerClient.PostAsync(
            "/api/ticket-drafts/1/submit", null);
        using var upload = new MultipartFormDataContent();
        upload.Add(new ByteArrayContent([1, 2, 3]), "file", "manager.txt");
        var attachmentUpload = await managerClient.PostAsync(
            $"/api/tickets/{ticket.Id}/attachments", upload);
        var attachmentDelete = await managerClient.DeleteAsync(
            $"/api/tickets/{ticket.Id}/attachments/1");
        var employeeDashboard = await employeeClient.GetAsync("/api/manager/dashboard");

        foreach (var response in new[]
        {
            personalList, personalDetails, create, update, cancel,
            draftList, draftRead, draftCreate, draftUpdate, draftDelete,
            draftSubmit, attachmentUpload, attachmentDelete
        })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, employeeDashboard.StatusCode);
    }

    [Fact]
    public async Task Manager_SharedLookupsAndOrganizationTicketDetails_AreAuthorized()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "shared-options-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "shared-options-employee@resolvehub.test", Password, RoleNames.Employee);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);

        var categories = await managerClient.GetAsync("/api/ticket-categories");
        var priorities = await managerClient.GetAsync("/api/ticket-priorities");
        var statuses = await managerClient.GetAsync("/api/ticket-statuses");
        var details = await managerClient.GetAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}");
        var ticketDetails =
            await details.Content.ReadFromJsonAsync<AdminTicketDetailsDto>();

        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        Assert.Equal(HttpStatusCode.OK, priorities.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statuses.StatusCode);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Contains(ticketDetails!.History,
            item => item.ActionType == TicketHistoryActionNames.TicketCreated);
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
        var nonAgent = await factory.CreateUserAsync(
            "assignment-non-agent@resolvehub.test", Password,
            RoleNames.Employee);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);

        var dashboard = await managerClient.GetFromJsonAsync<ManagerDashboardDto>(
            "/api/manager/dashboard");
        var invalid = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = inactiveAgent.Id });
        var invalidRole = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = nonAgent.Id });
        var assigned = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = activeAgent.Id });
        var snapshot = await factory.GetTicketSnapshotAsync(ticket.Id);

        Assert.NotNull(dashboard);
        Assert.Equal(1, dashboard.TotalTickets);
        Assert.Contains(dashboard.Unassigned,
            item => item.TicketReferenceNumber == ticket.TicketReferenceNumber);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
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
