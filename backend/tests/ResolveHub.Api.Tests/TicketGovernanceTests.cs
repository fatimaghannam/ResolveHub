using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Data;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Tickets;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class TicketGovernanceTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task AdministratorMarksDuplicateImmediately_WithoutReviewRecord()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var admin = await factory.CreateUserAsync(
            "admin-reporter@resolvehub.test", Password, RoleNames.Admin);
        var employee = await factory.CreateUserAsync(
            "direct-duplicate-owner@resolvehub.test", Password, RoleNames.Employee);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var original = await CreateTicketAsync(
            factory, employeeClient, "Original direct issue");
        var duplicate = await CreateTicketAsync(
            factory, employeeClient, "Repeated direct issue");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{duplicate.TicketReferenceNumber}/mark-duplicate",
            new
            {
                originalTicketReference = original.TicketReferenceNumber,
                reason = "Both tickets describe the same confirmed incident.",
                confirmed = true
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var details = await adminClient.GetFromJsonAsync<AdminTicketDetailsDto>(
            $"/api/admin/tickets/{duplicate.TicketReferenceNumber}");
        Assert.Equal(TicketStatusNames.Duplicate, details!.StatusName);
        Assert.Equal(original.Id, details.OriginalTicketId);
        Assert.Null(details.PendingDuplicateReview);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.DuplicateReviews.AnyAsync(item =>
            item.TicketID == duplicate.Id));
        Assert.True(await db.TicketHistory.AnyAsync(item =>
            item.TicketID == duplicate.Id && item.IsInternal &&
            item.Description == "Both tickets describe the same confirmed incident."));
        Assert.True(await db.TicketHistory.AnyAsync(item =>
            item.TicketID == duplicate.Id &&
            item.ActionType == TicketHistoryActionNames.DuplicateMarked &&
            item.Description == $"Test User marked {duplicate.TicketReferenceNumber} as a duplicate of {original.TicketReferenceNumber}."));
        Assert.True(await db.ActivityLogs.AnyAsync(item =>
            item.EntityID == duplicate.TicketReferenceNumber &&
            item.ActionType == TicketHistoryActionNames.DuplicateMarked &&
            item.Description.Contains(original.TicketReferenceNumber)));
        Assert.False(await db.UserNotifications.AnyAsync(item =>
            item.UserAccountID == admin.Id &&
            item.TicketReferenceNumber == duplicate.TicketReferenceNumber));

        var assign = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{duplicate.TicketReferenceNumber}/assign",
            new { agentUserId = admin.Id });
        Assert.Equal(HttpStatusCode.Conflict, assign.StatusCode);
        Assert.Contains(DuplicateTicketRules.ReadOnlyMessage,
            await assign.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AdministratorDirectDuplicateEndpoint_ValidatesInputLifecycleAndRole()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var admin = await factory.CreateUserAsync(
            "direct-validation-admin@resolvehub.test", Password, RoleNames.Admin);
        var manager = await factory.CreateUserAsync(
            "direct-validation-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "direct-validation-owner@resolvehub.test", Password, RoleNames.Employee);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var original = await CreateTicketAsync(
            factory, employeeClient, "Direct validation original");
        var duplicate = await CreateTicketAsync(
            factory, employeeClient, "Direct validation duplicate");
        var endpoint =
            $"/api/admin/tickets/{duplicate.TicketReferenceNumber.ToLowerInvariant()}/mark-duplicate";

        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PostAsJsonAsync(
            endpoint, new
            {
                originalTicketReference = duplicate.TicketReferenceNumber,
                confirmed = true
            })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PostAsJsonAsync(
            endpoint, new
            {
                originalTicketReference = original.TicketReferenceNumber,
                confirmed = false
            })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.PostAsJsonAsync(
            endpoint, new
            {
                originalTicketReference = "RH-2099-9999",
                confirmed = true
            })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await managerClient.PostAsJsonAsync(
            endpoint, new
            {
                originalTicketReference = original.TicketReferenceNumber,
                confirmed = true
            })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync(
            endpoint, new
            {
                originalTicketReference = $" {original.TicketReferenceNumber.ToLowerInvariant()} ",
                reason = (string?)null,
                confirmed = true
            })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PostAsJsonAsync(
            endpoint, new
            {
                originalTicketReference = original.TicketReferenceNumber,
                confirmed = true
            })).StatusCode);
    }

    [Fact]
    public async Task ManagerReportsAndAdministratorApprovesDuplicate_WithAuditAndNotifications()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var admin = await factory.CreateUserAsync(
            "governance-admin@resolvehub.test", Password, RoleNames.Admin);
        var manager = await factory.CreateUserAsync(
            "governance-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "governance-employee@resolvehub.test", Password, RoleNames.Employee);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        var original = await CreateTicketAsync(factory, employeeClient, "Original issue");
        var duplicate = await CreateTicketAsync(factory, employeeClient, "Duplicate issue");

        var report = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{duplicate.TicketReferenceNumber}/duplicate-reviews",
            new { suggestedOriginalTicketReference = original.TicketReferenceNumber,
                reason = "The requester reported the same incident twice." });
        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        var review = (await report.Content.ReadFromJsonAsync<DuplicateReviewDto>())!;
        var beforeApproval = await adminClient.GetFromJsonAsync<AdminTicketDetailsDto>(
            $"/api/admin/tickets/{duplicate.TicketReferenceNumber}");
        Assert.Equal(TicketStatusNames.Open, beforeApproval!.StatusName);
        Assert.NotNull(beforeApproval.PendingDuplicateReview);

        var approve = await adminClient.PostAsync(
            $"/api/admin/duplicate-reviews/{review.Id}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        var details = await adminClient.GetFromJsonAsync<AdminTicketDetailsDto>(
            $"/api/admin/tickets/{duplicate.TicketReferenceNumber}");
        Assert.Equal(TicketStatusNames.Duplicate, details!.StatusName);
        Assert.Equal(original.TicketReferenceNumber, details.OriginalTicketReference);
        Assert.Equal(original.Title, details.OriginalTicketTitle);
        Assert.Null(details.PendingDuplicateReview);

        var assign = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{duplicate.TicketReferenceNumber}/assign",
            new { agentUserId = 99999 });
        Assert.Equal(HttpStatusCode.Conflict, assign.StatusCode);
        Assert.Contains(DuplicateTicketRules.ReadOnlyMessage,
            await assign.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await employeeClient.PutAsJsonAsync(
            $"/api/tickets/{duplicate.Id}", new
            {
                title = "A changed duplicate title",
                description = "This change must be rejected by the backend.",
                ticketCategoryId = duplicate.TicketCategoryId,
                ticketPriorityId = duplicate.TicketPriorityId
            })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await employeeClient.PostAsJsonAsync(
            $"/api/tickets/{duplicate.Id}/comments",
            new { message = "Do not add", visibility = "Public" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{duplicate.TicketReferenceNumber}/duplicate-reviews",
            new { suggestedOriginalTicketReference = original.TicketReferenceNumber }))
            .StatusCode);

        var notifications = (await adminClient.GetFromJsonAsync<List<UserNotificationDto>>(
            "/api/admin/notifications"))!;
        var pendingNotification = Assert.Single(notifications, item =>
            item.Title == "Duplicate Review Pending");
        Assert.Equal(duplicate.TicketReferenceNumber,
            pendingNotification.TicketReferenceNumber);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PatchAsync(
            $"/api/admin/notifications/{pendingNotification.Id}/read", null)).StatusCode);
        Assert.True((await adminClient.GetFromJsonAsync<List<UserNotificationDto>>(
            "/api/admin/notifications"))!.Single(item =>
                item.Id == pendingNotification.Id).IsRead);
        Assert.Equal(HttpStatusCode.NoContent, (await managerClient.PatchAsync(
            "/api/manager/notifications/read-all", null)).StatusCode);
        Assert.All((await managerClient.GetFromJsonAsync<List<UserNotificationDto>>(
            "/api/manager/notifications"))!, item => Assert.True(item.IsRead));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.TicketHistory.AnyAsync(item => item.TicketID == duplicate.Id &&
            item.ActionType == TicketHistoryActionNames.DuplicateReviewApproved));
        Assert.False(await db.TicketHistory.AnyAsync(item => item.TicketID == duplicate.Id &&
            item.ActionType == TicketHistoryActionNames.DuplicateReviewRejected));
        Assert.True(await db.ActivityLogs.AnyAsync(item =>
            item.EntityID == duplicate.TicketReferenceNumber &&
            item.ActionType == TicketHistoryActionNames.DuplicateReviewApproved));
        Assert.True(await db.UserNotifications.AnyAsync(item =>
            item.UserAccountID == admin.Id && item.Title == "Duplicate Review Pending"));
        Assert.True(await db.UserNotifications.AnyAsync(item =>
            item.UserAccountID == manager.Id && item.Title == "Duplicate Review Approved"));
    }

    [Fact]
    public async Task DuplicateReview_IsRoleRestricted_RejectsSelfReference_AndCanBeRejected()
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
        var selfRequest = new { suggestedOriginalTicketReference = ticket.TicketReferenceNumber };
        Assert.Equal(HttpStatusCode.BadRequest, (await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/duplicate-reviews",
            selfRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employeeClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/duplicate-reviews",
            selfRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await managerClient.PostAsync(
            "/api/admin/duplicate-reviews/1/approve", null)).StatusCode);

        var original = await CreateTicketAsync(factory, employeeClient, "Original for rejection");
        var report = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/duplicate-reviews",
            new { suggestedOriginalTicketReference = original.TicketReferenceNumber });
        var review = (await report.Content.ReadFromJsonAsync<DuplicateReviewDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync(
            $"/api/admin/duplicate-reviews/{review.Id}/reject", null)).StatusCode);
        var snapshot = await factory.GetTicketSnapshotAsync(ticket.Id);
        Assert.Equal(TicketStatusNames.Open, snapshot.StatusName);
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
