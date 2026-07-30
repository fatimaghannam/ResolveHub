using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Tickets;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class TicketGovernanceTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task Administrator_CanRemoveDuplicate_WithAuditTrail()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var admin = await factory.CreateUserAsync(
            "governance-admin@resolvehub.test", Password, RoleNames.Admin);
        var employee = await factory.CreateUserAsync(
            "governance-employee@resolvehub.test", Password, RoleNames.Employee);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var original = await CreateTicketAsync(factory, employeeClient, "Original issue");
        var duplicate = await CreateTicketAsync(factory, employeeClient, "Duplicate issue");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{duplicate.TicketReferenceNumber}/remove-duplicate",
            new
            {
                originalTicketReference = original.TicketReferenceNumber,
                confirmed = true
            });
        var snapshot = await factory.GetTicketGovernanceSnapshotAsync(
            duplicate.Id, TicketHistoryActionNames.DuplicateRemoved);
        var details = await adminClient.GetAsync(
            $"/api/admin/tickets/{duplicate.TicketReferenceNumber}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(snapshot.IsDeleted);
        Assert.Equal(TicketStatusNames.Cancelled, snapshot.StatusName);
        Assert.True(snapshot.HasHistory);
        Assert.True(snapshot.HasActivity);
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
    }

    [Fact]
    public async Task DuplicateRemoval_IsAdminOnly_AndRejectsSelfReference()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var admin = await factory.CreateUserAsync(
            "duplicate-admin@resolvehub.test", Password, RoleNames.Admin);
        var manager = await factory.CreateUserAsync(
            "duplicate-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "duplicate-owner@resolvehub.test", Password, RoleNames.Employee);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient, "Self duplicate");
        var request = new
        {
            originalTicketReference = ticket.TicketReferenceNumber,
            confirmed = true
        };

        var managerResponse = await managerClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/remove-duplicate",
            request);
        var employeeResponse = await employeeClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/remove-duplicate",
            request);
        var adminResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/remove-duplicate",
            request);
        var snapshot = await factory.GetTicketSnapshotAsync(ticket.Id);

        Assert.Equal(HttpStatusCode.Forbidden, managerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employeeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, adminResponse.StatusCode);
        Assert.False(snapshot.IsDeleted);
    }

    [Fact]
    public async Task AssignmentCapacity_CountsOnlyActiveStatuses_AndBlocksAtFive()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var admin = await factory.CreateUserAsync(
            "capacity-admin@resolvehub.test", Password, RoleNames.Admin);
        var manager = await factory.CreateUserAsync(
            "capacity-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "capacity-employee@resolvehub.test", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "capacity-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var otherAgent = await factory.CreateUserAsync(
            "capacity-other-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);

        foreach (var status in new[]
        {
            TicketStatusNames.Resolved,
            TicketStatusNames.Closed,
            TicketStatusNames.Cancelled
        })
        {
            var terminal = await CreateTicketAsync(
                factory, employeeClient, $"Terminal {status}");
            await factory.SetTicketStateAsync(terminal.Id, status, agent.Id);
        }

        var active = new List<TicketDetailsDto>();
        foreach (var status in new[]
        {
            TicketStatusNames.Assigned,
            TicketStatusNames.Assigned,
            TicketStatusNames.InProgress,
            TicketStatusNames.Pending
        })
        {
            var ticket = await CreateTicketAsync(
                factory, employeeClient, $"Active {status} {active.Count}");
            await factory.SetTicketStateAsync(ticket.Id, status, agent.Id);
            active.Add(ticket);
        }

        var adminAtFour = await adminClient.GetFromJsonAsync<
            IReadOnlyCollection<AdminAgentWorkloadDto>>("/api/admin/users/agents");
        var managerAtFour = await managerClient.GetFromJsonAsync<
            IReadOnlyCollection<ManagerAgentWorkloadDto>>("/api/manager/workload");
        var adminFour = Assert.Single(
            adminAtFour!, item => item.UserId == agent.Id);
        var managerFour = Assert.Single(
            managerAtFour!, item => item.UserId == agent.Id);
        Assert.Equal(4, adminFour.ActiveTicketCount);
        Assert.Equal(5, adminFour.MaxActiveTickets);
        Assert.Equal(1, adminFour.RemainingCapacity);
        Assert.Equal("Near Capacity", adminFour.CapacityState);
        Assert.False(adminFour.IsAtCapacity);
        Assert.Equal(adminFour.ActiveTicketCount, managerFour.ActiveTicketCount);
        Assert.Equal(adminFour.MaxActiveTickets, managerFour.MaxActiveTickets);
        Assert.Equal(adminFour.RemainingCapacity, managerFour.RemainingCapacity);
        Assert.Equal(adminFour.CapacityState, managerFour.CapacityState);
        Assert.Equal(adminFour.IsAtCapacity, managerFour.IsAtCapacity);

        var fifth = await CreateTicketAsync(factory, employeeClient, "Fifth active");
        var fifthResponse = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{fifth.TicketReferenceNumber}/assign",
            new { agentUserId = agent.Id });
        var workloads = await adminClient.GetFromJsonAsync<
            IReadOnlyCollection<AdminAgentWorkloadDto>>("/api/admin/users/agents");
        var workload = Assert.Single(workloads!, item => item.UserId == agent.Id);

        Assert.Equal(HttpStatusCode.NoContent, fifthResponse.StatusCode);
        Assert.Equal(5, workload.ActiveTicketCount);
        Assert.Equal(5, workload.MaxActiveTickets);
        Assert.Equal(0, workload.RemainingCapacity);
        Assert.Equal("Full", workload.CapacityState);
        Assert.True(workload.IsAtCapacity);

        var sixth = await CreateTicketAsync(factory, employeeClient, "Legacy sixth active");
        await factory.SetTicketStateAsync(
            sixth.Id, TicketStatusNames.Assigned, agent.Id);
        var legacyWorkloads = await adminClient.GetFromJsonAsync<
            IReadOnlyCollection<AdminAgentWorkloadDto>>("/api/admin/users/agents");
        var managerLegacyWorkloads = await managerClient.GetFromJsonAsync<
            IReadOnlyCollection<ManagerAgentWorkloadDto>>("/api/manager/workload");
        var legacy = Assert.Single(
            legacyWorkloads!, item => item.UserId == agent.Id);
        var managerLegacy = Assert.Single(
            managerLegacyWorkloads!, item => item.UserId == agent.Id);
        Assert.Equal(6, legacy.ActiveTicketCount);
        Assert.Equal(5, legacy.MaxActiveTickets);
        Assert.Equal(0, legacy.RemainingCapacity);
        Assert.Equal("Over Capacity", legacy.CapacityState);
        Assert.True(legacy.IsAtCapacity);
        Assert.Equal(legacy.ActiveTicketCount, managerLegacy.ActiveTicketCount);
        Assert.Equal(legacy.MaxActiveTickets, managerLegacy.MaxActiveTickets);
        Assert.Equal(legacy.RemainingCapacity, managerLegacy.RemainingCapacity);
        Assert.Equal(legacy.CapacityState, managerLegacy.CapacityState);
        Assert.Equal(legacy.IsAtCapacity, managerLegacy.IsAtCapacity);

        var seventh = await CreateTicketAsync(factory, employeeClient, "Blocked seventh active");
        var blocked = await adminClient.PutAsJsonAsync(
            $"/api/admin/tickets/{seventh.TicketReferenceNumber}/assignment",
            new { agentUserId = agent.Id });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        await factory.SetTicketStateAsync(
            active[0].Id, TicketStatusNames.Resolved, agent.Id);
        await factory.SetTicketStateAsync(
            active[1].Id, TicketStatusNames.Resolved, agent.Id);
        var afterResolution = await adminClient.PutAsJsonAsync(
            $"/api/admin/tickets/{seventh.TicketReferenceNumber}/assignment",
            new { agentUserId = agent.Id });

        Assert.Equal(HttpStatusCode.NoContent, afterResolution.StatusCode);

        await factory.SetTicketStateAsync(
            active[2].Id, TicketStatusNames.Resolved, agent.Id);
        var reassignment = await CreateTicketAsync(
            factory, employeeClient, "Administrator reassignment");
        await factory.SetTicketStateAsync(
            reassignment.Id, TicketStatusNames.Assigned, otherAgent.Id);
        var reassigned = await adminClient.PutAsJsonAsync(
            $"/api/admin/tickets/{reassignment.TicketReferenceNumber}/assignment",
            new { agentUserId = agent.Id });
        var reassignedSnapshot =
            await factory.GetTicketSnapshotAsync(reassignment.Id);

        Assert.Equal(HttpStatusCode.NoContent, reassigned.StatusCode);
        Assert.Equal(agent.Id, reassignedSnapshot.AssignedToUserAccountID);
    }

    [Fact]
    public async Task EmployeeCancellation_WritesHistoryAndActivity()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "cancel-audit@resolvehub.test", Password, RoleNames.Employee);
        using var client = await LoginAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, client, "Audited cancellation");

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/cancel",
            new { reason = "Created by mistake." });
        var snapshot = await factory.GetTicketGovernanceSnapshotAsync(
            ticket.Id, TicketHistoryActionNames.TicketCancelled);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(snapshot.IsDeleted);
        Assert.Equal(TicketStatusNames.Cancelled, snapshot.StatusName);
        Assert.True(snapshot.HasHistory);
        Assert.True(snapshot.HasActivity);
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
            description = $"Description for {title}.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDetailsDto>())!;
    }

    private static async Task<HttpClient> LoginAsync(
        ResolveHubApiFactory factory,
        string email)
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
