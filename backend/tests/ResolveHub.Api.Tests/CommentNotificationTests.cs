using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class CommentNotificationTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task Comments_NotifyExactlyTheAllowedAudienceForEachVisibilityAndAuthorRole()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var requester = await factory.CreateUserAsync(
            "notification-requester@resolvehub.test", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "notification-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var manager = await factory.CreateUserAsync(
            "notification-manager@resolvehub.test", Password, RoleNames.Manager);
        var unrelated = await factory.CreateUserAsync(
            "notification-unrelated@resolvehub.test", Password, RoleNames.Employee);
        var adminOne = await factory.CreateUserAsync(
            "notification-admin-one@resolvehub.test", Password, RoleNames.Admin);
        var adminTwo = await factory.CreateUserAsync(
            "notification-admin-two@resolvehub.test", Password, RoleNames.Admin);
        var inactiveAdmin = await factory.CreateUserAsync(
            "notification-inactive-admin@resolvehub.test", Password, RoleNames.Admin,
            isActive: false);

        using var requesterClient = await LoginAsync(factory, requester.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        using var adminClient = await LoginAsync(factory, adminOne.Email!);
        var ticket = await CreateTicketAsync(factory, requesterClient);
        var assigned = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = agent.Id });
        assigned.EnsureSuccessStatusCode();
        await ClearNotificationsAsync(factory);

        await PostAndAssertAsync(requesterClient,
            $"/api/tickets/{ticket.Id}/comments", "Public",
            [agent.Id, adminOne.Id, adminTwo.Id]);
        await PostAndAssertAsync(agentClient,
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments", "Public",
            [requester.Id, adminOne.Id, adminTwo.Id]);
        await PostAndAssertAsync(adminClient,
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/comments", "Public",
            [requester.Id, agent.Id, adminTwo.Id]);
        await PostAndAssertAsync(requesterClient,
            $"/api/tickets/{ticket.Id}/comments", "Private", [agent.Id]);
        await PostAndAssertAsync(agentClient,
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments", "Private",
            [requester.Id]);

        async Task PostAndAssertAsync(HttpClient client, string path,
            string visibility, int[] expectedRecipientIds)
        {
            var response = await client.PostAsJsonAsync(path, new
            {
                message = $"{visibility} notification test.",
                visibility
            });
            response.EnsureSuccessStatusCode();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notifications = await db.UserNotifications.AsNoTracking().ToListAsync();
            Assert.Equal(expectedRecipientIds.Order(),
                notifications.Select(item => item.UserAccountID).Order());
            Assert.DoesNotContain(notifications, item =>
                item.UserAccountID == manager.Id ||
                item.UserAccountID == unrelated.Id ||
                item.UserAccountID == inactiveAdmin.Id);
            Assert.All(notifications, item =>
            {
                Assert.Equal(visibility == "Public"
                    ? NotificationTypeNames.PublicCommentAdded
                    : NotificationTypeNames.PrivateCommentAdded, item.Type);
                Assert.Equal($"New {visibility} Comment", item.Title);
                Assert.Equal(ticket.TicketReferenceNumber, item.TicketReferenceNumber);
                Assert.Contains($"added a {visibility.ToLowerInvariant()} comment to {ticket.TicketReferenceNumber}.",
                    item.Message);
                Assert.DoesNotContain("notification test", item.Message);
            });
            db.UserNotifications.RemoveRange(await db.UserNotifications.ToListAsync());
            await db.SaveChangesAsync();
        }
    }

    private static async Task<TicketDetailsDto> CreateTicketAsync(
        ResolveHubApiFactory factory, HttpClient client)
    {
        var lookups = await factory.GetTicketLookupIdsAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Comment notification recipients",
            description = "Verifies public and private notification audiences.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TicketDetailsDto>())!;
    }

    private static async Task ClearNotificationsAsync(ResolveHubApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserNotifications.RemoveRange(await db.UserNotifications.ToListAsync());
        await db.SaveChangesAsync();
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
