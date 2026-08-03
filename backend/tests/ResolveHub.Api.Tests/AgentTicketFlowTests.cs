using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class AgentTicketFlowTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task AgentEndpoints_EnforceAuthenticationAndExactRole()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        using var anonymous = factory.CreateHttpsClient();
        var employee = await factory.CreateUserAsync("agent-role-employee@test.local", Password);
        var admin = await factory.CreateUserAsync(
            "agent-role-admin@test.local", Password, RoleNames.Admin);
        var agent = await factory.CreateUserAsync(
            "agent-role-agent@test.local", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/agent/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await employeeClient.GetAsync("/api/agent/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await adminClient.GetAsync("/api/agent/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await agentClient.GetAsync("/api/agent/dashboard")).StatusCode);
    }

    [Fact]
    public async Task AssignedTickets_AreIsolatedByAuthenticatedAgent()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("agent-isolation-owner@test.local", Password);
        var firstAgent = await factory.CreateUserAsync(
            "agent-isolation-first@test.local", Password, RoleNames.ITSupportAgent);
        var secondAgent = await factory.CreateUserAsync(
            "agent-isolation-second@test.local", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var firstClient = await LoginAsync(factory, firstAgent.Email!);
        using var secondClient = await LoginAsync(factory, secondAgent.Email!);
        var firstTicket = await CreateTicketAsync(factory, employeeClient, "First assigned ticket");
        var secondTicket = await CreateTicketAsync(factory, employeeClient, "Second assigned ticket");
        await factory.SetTicketStateAsync(
            firstTicket.Id, TicketStatusNames.Assigned, firstAgent.Id);
        await factory.SetTicketStateAsync(
            secondTicket.Id, TicketStatusNames.Assigned, secondAgent.Id);

        var page = await firstClient.GetFromJsonAsync<
            PagedResultDto<AgentTicketListItemDto>>("/api/agent/tickets");
        var inaccessible = await firstClient.GetAsync(
            $"/api/agent/tickets/{secondTicket.TicketReferenceNumber}");
        var update = await firstClient.PatchAsJsonAsync(
            $"/api/agent/tickets/{secondTicket.TicketReferenceNumber}/status",
            new { statusId = await LookupIdAsync(factory, "status", TicketStatusNames.InProgress) });

        Assert.Single(page!.Items);
        Assert.Equal(firstTicket.Id, page.Items.Single().Id);
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task Dashboard_ComputesOwnedWorkflowCountsAndExcludesDeletedTickets()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("agent-dashboard-owner@test.local", Password);
        var agent = await factory.CreateUserAsync(
            "agent-dashboard-agent@test.local", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        var assigned = await CreateTicketAsync(factory, employeeClient, "Assigned dashboard ticket");
        var progress = await CreateTicketAsync(factory, employeeClient, "Critical progress ticket");
        var pending = await CreateTicketAsync(factory, employeeClient, "High pending ticket");
        var resolved = await CreateTicketAsync(factory, employeeClient, "Resolved dashboard ticket");
        await SetAgentStateAsync(factory, assigned.Id, agent.Id, TicketStatusNames.Assigned, "Low");
        await SetAgentStateAsync(factory, progress.Id, agent.Id, TicketStatusNames.InProgress, "Critical");
        await SetAgentStateAsync(factory, pending.Id, agent.Id, TicketStatusNames.Pending, "High");
        await SetAgentStateAsync(factory, resolved.Id, agent.Id, TicketStatusNames.Resolved, "High");

        var dashboard = await agentClient.GetFromJsonAsync<AgentDashboardDto>(
            "/api/agent/dashboard");

        Assert.Equal(3, dashboard!.ActiveAssignedTickets);
        Assert.Equal(1, dashboard.InProgress);
        Assert.Equal(1, dashboard.Pending);
        Assert.Equal(1, dashboard.HighPriorityOpen);
        Assert.Equal(1, dashboard.CriticalOpen);
        Assert.Equal(1, dashboard.ResolvedThisMonth);
        Assert.DoesNotContain(dashboard.PriorityAttentionTickets,
            ticket => ticket.Id == resolved.Id);
    }

    [Fact]
    public async Task AssignedTicketFilters_SearchDatesAndPaginationWork()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("agent-filter-owner@test.local", Password);
        var agent = await factory.CreateUserAsync(
            "agent-filter-agent@test.local", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        var first = await CreateTicketAsync(factory, employeeClient, "Network adapter failure");
        var second = await CreateTicketAsync(factory, employeeClient, "Printer toner warning");
        await SetAgentStateAsync(factory, first.Id, agent.Id, TicketStatusNames.InProgress, "High");
        await SetAgentStateAsync(factory, second.Id, agent.Id, TicketStatusNames.Assigned, "Low");
        await factory.SetTicketCreatedDateAsync(first.Id, DateTime.UtcNow.Date.AddDays(-2));

        var search = await agentClient.GetFromJsonAsync<
            PagedResultDto<AgentTicketListItemDto>>(
            $"/api/agent/tickets?search={Uri.EscapeDataString(first.TicketReferenceNumber)}");
        var dates = await agentClient.GetFromJsonAsync<
            PagedResultDto<AgentTicketListItemDto>>(
            $"/api/agent/tickets?fromDate={DateTime.UtcNow.Date.AddDays(-2):yyyy-MM-dd}" +
            $"&toDate={DateTime.UtcNow.Date.AddDays(-2):yyyy-MM-dd}");
        var page = await agentClient.GetFromJsonAsync<
            PagedResultDto<AgentTicketListItemDto>>("/api/agent/tickets?page=1&pageSize=1");

        Assert.Equal(first.Id, Assert.Single(search!.Items).Id);
        Assert.Equal(first.Id, Assert.Single(dates!.Items).Id);
        Assert.Equal(2, page!.TotalItems);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact]
    public async Task StatusCommentsNotesResolutionAndHistory_ArePersistedSecurely()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("agent-workflow-owner@test.local", Password);
        var agent = await factory.CreateUserAsync(
            "agent-workflow-agent@test.local", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient, "VPN workflow ticket");
        await factory.SetTicketStateAsync(
            ticket.Id, TicketStatusNames.Assigned, agent.Id);
        var progressId = await LookupIdAsync(factory, "status", TicketStatusNames.InProgress);
        var closedId = await LookupIdAsync(factory, "status", TicketStatusNames.Closed);

        var progress = await agentClient.PatchAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/status",
            new { statusId = progressId, reason = "Investigation started." });
        var invalid = await agentClient.PatchAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/status",
            new { statusId = closedId });
        var comment = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Please test the VPN connection again.", visibility = "Public" });
        var note = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "VPN profile was rebuilt by the support agent.", visibility = "Private" });
        var resolve = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/resolve",
            new { resolutionSummary = "Rebuilt the VPN profile and reset cached credentials." });
        var publicComments = (await employeeClient.GetFromJsonAsync<
            TicketCommentPageDto>($"/api/tickets/{ticket.Id}/comments"))!.Items;
        var details = await agentClient.GetFromJsonAsync<AgentTicketDetailsDto>(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}");

        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        Assert.Equal(2, publicComments.Count);
        Assert.Equal(TicketStatusNames.Resolved, details!.StatusName);
        Assert.NotNull(details.ResolvedDate);
        Assert.Equal(agent.Id, await ResolvedByAsync(factory, ticket.Id));
        Assert.Contains(details.History,
            item => item.ActionType == TicketHistoryActionNames.TicketResolved);
        Assert.Contains(publicComments, item => item.Content.Contains("rebuilt by"));
    }

    [Fact]
    public async Task AssignedAgent_CompletesResolvedAndClosedWorkflow_WithAuditAndVisibility()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "close-workflow-owner@test.local", Password);
        var agent = await factory.CreateUserAsync(
            "close-workflow-agent@test.local", Password,
            RoleNames.ITSupportAgent);
        var manager = await factory.CreateUserAsync(
            "close-workflow-manager@test.local", Password, RoleNames.Manager);
        var admin = await factory.CreateUserAsync(
            "close-workflow-admin@test.local", Password, RoleNames.Admin);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        var ticket = await CreateTicketAsync(
            factory, employeeClient, "Close lifecycle ticket");
        using var upload = new MultipartFormDataContent();
        var fileContent =
            new ByteArrayContent(Encoding.UTF8.GetBytes("preserved file"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        upload.Add(fileContent, "file", "preserved.txt");
        var attachmentResponse = await employeeClient.PostAsync(
            $"/api/tickets/{ticket.Id}/attachments", upload);
        attachmentResponse.EnsureSuccessStatusCode();
        await factory.SetTicketStateAsync(
            ticket.Id, TicketStatusNames.Assigned, agent.Id);

        var assignedClose = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/close",
            new { closingNote = "Too early." });
        var assignedResolve = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/resolve",
            new { resolutionSummary = "Cannot resolve before work begins." });
        var progress = await agentClient.PatchAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/status",
            new
            {
                statusId = await LookupIdAsync(
                    factory, "status", TicketStatusNames.InProgress)
            });
        var inProgressClose = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/close",
            new { });
        var comment = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "The corrected configuration is ready for verification.", visibility = "Public" });
        var note = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Configuration backup retained for audit purposes.", visibility = "Private" });
        var resolve = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/resolve",
            new { resolutionSummary = "Corrected the configuration and verified connectivity." });
        var afterResolution = await agentClient.GetFromJsonAsync<AgentDashboardDto>(
            "/api/agent/dashboard");
        var close = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/close",
            new { closingNote = "Employee confirmed that service is restored." });
        var closed = await close.Content.ReadFromJsonAsync<AgentTicketDetailsDto>();
        var afterClose = await agentClient.GetFromJsonAsync<AgentDashboardDto>(
            "/api/agent/dashboard");
        var closeAgain = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/close",
            new { });
        var agentCommentAfterClose = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Too late.", visibility = "Public" });
        var employeeCommentAfterClose = await employeeClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments",
            new { message = "Too late.", visibility = "Public" });
        var managerCommentAfterClose = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Too late." });
        var adminCommentAfterClose = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Too late." });
        var employeeDetails = await employeeClient.GetFromJsonAsync<TicketDetailsDto>(
            $"/api/tickets/{ticket.Id}");
        var managerDetails = await managerClient.GetFromJsonAsync<AdminTicketDetailsDto>(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}");
        var adminDetails = await adminClient.GetFromJsonAsync<AdminTicketDetailsDto>(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}");

        Assert.Equal(HttpStatusCode.Conflict, assignedClose.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, assignedResolve.StatusCode);
        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, inProgressClose.StatusCode);
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        Assert.Equal(0, afterResolution!.ActiveAssignedTickets);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Equal(TicketStatusNames.Closed, closed!.StatusName);
        Assert.NotNull(closed.ClosedDate);
        Assert.False(closed.CanClose);
        Assert.False(closed.CanResolve);
        Assert.False(closed.CanComment);
        Assert.Equal(2, closed.Comments.Count);
        Assert.Single(closed.Attachments);
        Assert.Contains(closed.History,
            item => item.ActionType == TicketHistoryActionNames.TicketWorkStarted);
        Assert.Contains(closed.History,
            item => item.ActionType == TicketHistoryActionNames.TicketResolved);
        Assert.Contains(closed.History,
            item => item.ActionType == TicketHistoryActionNames.TicketClosed);
        Assert.Equal(0, afterClose!.ActiveAssignedTickets);
        Assert.Equal(HttpStatusCode.Conflict, closeAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, agentCommentAfterClose.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, employeeCommentAfterClose.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, managerCommentAfterClose.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, adminCommentAfterClose.StatusCode);
        Assert.Equal(TicketStatusNames.Closed, employeeDetails!.StatusName);
        Assert.Equal(TicketStatusNames.Closed, managerDetails!.StatusName);
        Assert.Equal(TicketStatusNames.Closed, adminDetails!.StatusName);
        Assert.Equal(2, employeeDetails.Comments.Count);
        Assert.Single(managerDetails.Comments);
        Assert.Single(adminDetails.Comments);
        Assert.Contains(employeeDetails.History,
            item => item.ActionType == TicketHistoryActionNames.TicketClosed);
        Assert.Contains(managerDetails.History,
            item => item.ActionType == TicketHistoryActionNames.TicketClosed);
        Assert.Contains(adminDetails.History,
            item => item.ActionType == TicketHistoryActionNames.TicketClosed);
        Assert.NotNull(managerDetails.ResolvedDate);
        Assert.NotNull(adminDetails.ClosedDate);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await context.ActivityLogs.AnyAsync(item =>
            item.EntityID == ticket.TicketReferenceNumber &&
            item.ActionType == TicketHistoryActionNames.TicketClosed));
    }

    [Fact]
    public async Task CloseEndpoint_RejectsOtherAgentEmployeeAndManager()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "close-auth-owner@test.local", Password);
        var assignedAgent = await factory.CreateUserAsync(
            "close-auth-assigned@test.local", Password,
            RoleNames.ITSupportAgent);
        var otherAgent = await factory.CreateUserAsync(
            "close-auth-other@test.local", Password,
            RoleNames.ITSupportAgent);
        var manager = await factory.CreateUserAsync(
            "close-auth-manager@test.local", Password, RoleNames.Manager);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var assignedClient = await LoginAsync(factory, assignedAgent.Email!);
        using var otherClient = await LoginAsync(factory, otherAgent.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        var ticket = await CreateTicketAsync(
            factory, employeeClient, "Close authorization ticket");
        await factory.SetTicketStateAsync(
            ticket.Id, TicketStatusNames.Resolved, assignedAgent.Id);
        var endpoint =
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/close";

        var other = await otherClient.PostAsJsonAsync(endpoint, new { });
        var employeeAttempt = await employeeClient.PostAsJsonAsync(endpoint, new { });
        var managerAttempt = await managerClient.PostAsJsonAsync(endpoint, new { });

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employeeAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerAttempt.StatusCode);
    }

    [Fact]
    public async Task AgentAttachmentDownload_RequiresAssignmentAndMatchingTicket()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("agent-file-owner@test.local", Password);
        var assignedAgent = await factory.CreateUserAsync(
            "agent-file-assigned@test.local", Password, RoleNames.ITSupportAgent);
        var otherAgent = await factory.CreateUserAsync(
            "agent-file-other@test.local", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var assignedClient = await LoginAsync(factory, assignedAgent.Email!);
        using var otherClient = await LoginAsync(factory, otherAgent.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient, "Attachment access ticket");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("diagnostic content"))
        {
            Headers = { ContentType = new MediaTypeHeaderValue("text/plain") }
        }, "file", "agent-diagnostic.txt");
        var upload = await employeeClient.PostAsync(
            $"/api/tickets/{ticket.Id}/attachments", form);
        var attachment = await upload.Content.ReadFromJsonAsync<TicketAttachmentDto>();
        await factory.SetTicketStateAsync(
            ticket.Id, TicketStatusNames.Assigned, assignedAgent.Id);

        var allowed = await assignedClient.GetAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/attachments/{attachment!.Id}/download");
        var denied = await otherClient.GetAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/attachments/{attachment.Id}/download");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task ConcurrentEmployeeCreation_ProducesUniqueNumericReferences()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync("reference-owner@test.local", Password);
        using var client = await LoginAsync(factory, employee.Email!);
        var lookups = await factory.GetTicketLookupIdsAsync();

        var tasks = Enumerable.Range(1, 6).Select(index =>
            client.PostAsJsonAsync("/api/tickets", new
            {
                title = $"Concurrent reference ticket {index}",
                description = "A sufficiently detailed concurrent ticket description.",
                ticketCategoryId = lookups.CategoryId,
                ticketPriorityId = lookups.PriorityId
            }));
        var responses = await Task.WhenAll(tasks);
        var tickets = new List<TicketDetailsDto>();
        foreach (var response in responses)
        {
            response.EnsureSuccessStatusCode();
            tickets.Add((await response.Content.ReadFromJsonAsync<TicketDetailsDto>())!);
        }

        Assert.Equal(tickets.Count,
            tickets.Select(ticket => ticket.TicketReferenceNumber).Distinct().Count());
        Assert.All(tickets, ticket =>
            Assert.Matches(@"^RH-\d{4}-\d{4,}$", ticket.TicketReferenceNumber));
    }

    [Fact]
    public async Task AssignedAgent_PausesResumesAndResolves_WithDistinctWorkSessions()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "pending-owner@test.local", Password);
        var agent = await factory.CreateUserAsync(
            "pending-agent@test.local", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient,
            "Pending work-session lifecycle");
        await factory.SetTicketStateAsync(
            ticket.Id, TicketStatusNames.Assigned, agent.Id);

        var start = await agentClient.PatchAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/status",
            new { statusId = await LookupIdAsync(
                factory, "status", TicketStatusNames.InProgress) });
        start.EnsureSuccessStatusCode();

        var pause = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/pending",
            new
            {
                reasonCode = TicketPendingReasons.EmployeeResponse,
                additionalNote = "Requested confirmation from the employee."
            });
        var paused = await pause.Content
            .ReadFromJsonAsync<AgentTicketWorkflowResultDto>();
        var resolveWhilePending = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/resolve",
            new { resolutionSummary = "This must not resolve while pending." });
        var resume = await agentClient.PostAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/resume-work", null);
        var resumed = await resume.Content
            .ReadFromJsonAsync<AgentTicketWorkflowResultDto>();
        var resolve = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/resolve",
            new { resolutionSummary = "Employee confirmed the solution after work resumed." });
        var summary = await agentClient.GetFromJsonAsync<TicketActivitySummaryDto>(
            $"/api/tickets/{ticket.TicketReferenceNumber}/activity-summary");
        var timeline = await agentClient.GetFromJsonAsync<List<TicketActivityDto>>(
            $"/api/tickets/{ticket.TicketReferenceNumber}/activity?descending=false");

        Assert.Equal(HttpStatusCode.OK, pause.StatusCode);
        Assert.Equal(TicketStatusNames.Pending, paused!.Ticket.StatusName);
        Assert.NotNull(paused.Ticket.CurrentPending);
        Assert.False(paused.ActivitySummary.IsWorkSessionActive);
        Assert.Equal(HttpStatusCode.Conflict, resolveWhilePending.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        Assert.Equal(TicketStatusNames.InProgress, resumed!.Ticket.StatusName);
        Assert.Null(resumed.Ticket.CurrentPending);
        Assert.True(resumed.ActivitySummary.IsWorkSessionActive);
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        Assert.Equal(2, summary!.TotalWorkSessions);
        Assert.Equal(1, summary.PendingPeriodCount);
        Assert.False(summary.IsWorkSessionActive);
        Assert.True(summary.TotalWorkMinutes >= 0);
        Assert.Single(timeline!, item =>
            item.ActivityType == TicketHistoryActionNames.WorkPaused);
        Assert.Single(timeline!, item =>
            item.ActivityType == TicketHistoryActionNames.WorkResumed);
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

    private static async Task<TicketDetailsDto> CreateTicketAsync(
        ResolveHubApiFactory factory, HttpClient client, string title)
    {
        var lookups = await factory.GetTicketLookupIdsAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title,
            description = "A detailed ticket description for Agent workflow testing.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDetailsDto>())!;
    }

    private static async Task<int> LookupIdAsync(
        ResolveHubApiFactory factory, string type, string name)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return type == "status"
            ? await context.TicketStatuses.Where(item => item.Name == name)
                .Select(item => item.ID).SingleAsync()
            : await context.TicketPriorities.Where(item => item.Name == name)
                .Select(item => item.ID).SingleAsync();
    }

    private static async Task SetAgentStateAsync(
        ResolveHubApiFactory factory, int ticketId, int agentId,
        string status, string priority)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ticket = await context.Tickets.FindAsync(ticketId)
            ?? throw new InvalidOperationException("Ticket not found.");
        ticket.AssignedToUserAccountID = agentId;
        ticket.AssignedDate = DateTime.UtcNow;
        ticket.TicketStatusID = await context.TicketStatuses
            .Where(item => item.Name == status).Select(item => item.ID).SingleAsync();
        ticket.TicketPriorityID = await context.TicketPriorities
            .Where(item => item.Name == priority).Select(item => item.ID).SingleAsync();
        if (status == TicketStatusNames.Resolved)
            ticket.ResolvedDate = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private static async Task<int?> ResolvedByAsync(
        ResolveHubApiFactory factory, int ticketId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Tickets.Where(ticket => ticket.ID == ticketId)
            .Select(ticket => ticket.ResolvedByUserAccountID).SingleAsync();
    }
}
