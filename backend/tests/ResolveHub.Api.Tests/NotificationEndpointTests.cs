using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.Entities;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class NotificationEndpointTests
{
    private const string Password = "ValidPassword123!";

    [Fact]
    public async Task SharedEndpoint_ReturnsOnlyCurrentUsersNotifications_ForEveryRole()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var roles = new[]
        {
            RoleNames.Employee,
            RoleNames.ITSupportAgent,
            RoleNames.Manager,
            RoleNames.Admin
        };
        var users = new List<UserAccount>();
        foreach (var role in roles)
            users.Add(await factory.CreateUserAsync(
                $"notifications-{users.Count}@resolvehub.test", Password, role));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var user in users)
                db.UserNotifications.Add(new UserNotification
                {
                    UserAccountID = user.Id,
                    Type = "Test",
                    Title = $"For {user.Id}",
                    Message = "Recipient isolation test",
                    CreatedDate = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        foreach (var user in users)
        {
            using var client = factory.CreateClient();
            var login = await client.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var auth = await login.Content.ReadFromJsonAsync<LoginPayload>();
            client.DefaultRequestHeaders.Authorization =
                new("Bearer", auth!.AccessToken);

            var response = await client.GetAsync("/api/notifications?limit=100");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var notifications = await response.Content
                .ReadFromJsonAsync<List<NotificationPayload>>();
            var notification = Assert.Single(notifications!);
            Assert.Equal($"For {user.Id}", notification.Title);
        }
    }

    private sealed record LoginPayload(string AccessToken);
    private sealed record NotificationPayload(string Title);
}
