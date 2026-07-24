using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class PasswordResetFlowTests
{
    private const string OriginalPassword = "ValidPassword1!";
    private const string NewPassword = "BetterPassword2!";
    private const string GenericForgotMessage =
        "If an eligible account exists for that email address, password reset instructions have been sent.";
    private const string GenericInvalidLinkMessage =
        "The password reset link is invalid or has expired. Please request a new one.";

    [Fact]
    public async Task ForgotPassword_EligibleUser_ReturnsGenericAcceptedAndSafeLink()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "eligible@resolvehub.test",
            OriginalPassword);
        using var client = factory.CreateHttpsClient();

        var response = await ForgotAsync(
            client,
            "eligible@resolvehub.test");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(GenericForgotMessage, await ReadMessageAsync(response));
        AssertNoCache(response);
        var email = Assert.Single(factory.EmailSender.Messages);
        var resetUri = new Uri(email.ResetUrl);
        var query = QueryHelpers.ParseQuery(resetUri.Query);
        Assert.Equal(
            "https://frontend.resolvehub.test/reset-password",
            resetUri.GetLeftPart(UriPartial.Path));
        Assert.Equal("eligible@resolvehub.test", query["email"]);
        Assert.False(string.IsNullOrWhiteSpace(query["token"]));
        Assert.DoesNotContain(
            query["token"].ToString(),
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unknown@resolvehub.test", true, true)]
    [InlineData("inactive@resolvehub.test", false, true)]
    [InlineData("unconfirmed@resolvehub.test", true, false)]
    public async Task ForgotPassword_IneligibleAccounts_DoNotRevealAccountState(
        string email,
        bool isActive,
        bool emailConfirmed)
    {
        await using var factory = new ResolveHubApiFactory();

        if (!email.StartsWith("unknown", StringComparison.Ordinal))
        {
            await factory.CreateUserAsync(
                email,
                OriginalPassword,
                isActive: isActive,
                emailConfirmed: emailConfirmed);
        }

        using var client = factory.CreateHttpsClient();
        var response = await ForgotAsync(client, email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(GenericForgotMessage, await ReadMessageAsync(response));
        Assert.Empty(factory.EmailSender.Messages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task ForgotPassword_InvalidInput_ReturnsBadRequest(
        string? email)
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.EmailSender.Messages);
    }

    [Fact]
    public async Task ForgotPassword_OversizedEmail_ReturnsBadRequest()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await ForgotAsync(
            client,
            $"{new string('a', 250)}@test.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.EmailSender.Messages);
    }

    [Fact]
    public async Task ForgotPassword_IsRateLimited()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();
        HttpResponseMessage? response = null;

        for (var request = 0; request < 6; request++)
        {
            response = await ForgotAsync(
                client,
                "unknown@resolvehub.test");
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [Fact]
    public async Task ResetPassword_ValidLink_ChangesPasswordAndCannotBeReused()
    {
        await using var factory = new ResolveHubApiFactory();
        const string email = "reset@resolvehub.test";
        await factory.CreateUserAsync(email, OriginalPassword);
        using var client = factory.CreateHttpsClient();
        var link = await CreateResetLinkAsync(factory, client, email);

        var response = await ResetAsync(
            client,
            link.Email,
            link.Token,
            NewPassword,
            NewPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoCache(response);
        Assert.Equal(
            "Your password has been reset successfully. You can now sign in with your new password.",
            await ReadMessageAsync(response));
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync(client, email, OriginalPassword)).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await LoginAsync(client, email, NewPassword)).StatusCode);

        var reused = await ResetAsync(
            client,
            link.Email,
            link.Token,
            "AnotherPassword3!",
            "AnotherPassword3!");
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal(GenericInvalidLinkMessage, await ReadMessageAsync(reused));
    }

    [Fact]
    public async Task ResetPassword_ClearsExistingLockout()
    {
        await using var factory = new ResolveHubApiFactory();
        const string email = "locked-reset@resolvehub.test";
        await factory.CreateUserAsync(email, OriginalPassword);
        using var client = factory.CreateHttpsClient();
        var link = await CreateResetLinkAsync(factory, client, email);
        await factory.SetLockoutAsync(
            email,
            5,
            DateTimeOffset.UtcNow.AddMinutes(10));

        var response = await ResetAsync(
            client,
            link.Email,
            link.Token,
            NewPassword,
            NewPassword);
        var user = await factory.GetUserAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_ReturnsPolicyErrors()
    {
        await using var factory = new ResolveHubApiFactory();
        const string email = "weak@resolvehub.test";
        await factory.CreateUserAsync(email, OriginalPassword);
        using var client = factory.CreateHttpsClient();
        var link = await CreateResetLinkAsync(factory, client, email);

        var response = await ResetAsync(
            client,
            link.Email,
            link.Token,
            "weak",
            "weak");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "The new password does not meet the password requirements.",
            json.GetProperty("message").GetString());
        Assert.NotEmpty(json.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task ResetPassword_MismatchedConfirmation_ReturnsBadRequest()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await ResetAsync(
            client,
            "user@resolvehub.test",
            "token",
            NewPassword,
            "DifferentPassword3!");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("not-base64url")]
    [InlineData("")]
    public async Task ResetPassword_MalformedOrMissingToken_ReturnsSafeBadRequest(
        string token)
    {
        await using var factory = new ResolveHubApiFactory();
        const string email = "malformed@resolvehub.test";
        await factory.CreateUserAsync(email, OriginalPassword);
        using var client = factory.CreateHttpsClient();

        var response = await ResetAsync(
            client,
            email,
            token,
            NewPassword,
            NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetPassword_TokenCannotBeUsedForDifferentUser()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "first@resolvehub.test",
            OriginalPassword);
        await factory.CreateUserAsync(
            "second@resolvehub.test",
            OriginalPassword);
        using var client = factory.CreateHttpsClient();
        var link = await CreateResetLinkAsync(
            factory,
            client,
            "first@resolvehub.test");

        var response = await ResetAsync(
            client,
            "second@resolvehub.test",
            link.Token,
            NewPassword,
            NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(GenericInvalidLinkMessage, await ReadMessageAsync(response));
    }

    [Fact]
    public async Task ResetPassword_InactiveUser_RemainsInactive()
    {
        await using var factory = new ResolveHubApiFactory();
        const string email = "inactive-reset@resolvehub.test";
        await factory.CreateUserAsync(
            email,
            OriginalPassword,
            isActive: false);
        using var client = factory.CreateHttpsClient();

        var response = await ResetAsync(
            client,
            email,
            "c29tZS10b2tlbg",
            NewPassword,
            NewPassword);
        var user = await factory.GetUserAsync(email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(user.IsActive);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await LoginAsync(
                client,
                email,
                OriginalPassword)).StatusCode);
    }

    [Fact]
    public async Task UnexpectedFailure_ReturnsSafeServerError()
    {
        await using var factory = new ResolveHubApiFactory(
            throwPasswordResetRequests: true);
        using var client = factory.CreateHttpsClient();

        var response = await ForgotAsync(
            client,
            "user@resolvehub.test");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);
        Assert.Contains(
            "The request could not be completed.",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Sensitive internal test detail.",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "InvalidOperationException",
            body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_ReturnsSafeBadRequest()
    {
        await using var factory =
            new ResolveHubApiFactory(TimeSpan.FromTicks(-1));
        const string email = "expired@resolvehub.test";
        await factory.CreateUserAsync(email, OriginalPassword);
        using var client = factory.CreateHttpsClient();
        var link = await CreateResetLinkAsync(factory, client, email);

        var response = await ResetAsync(
            client,
            link.Email,
            link.Token,
            NewPassword,
            NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(GenericInvalidLinkMessage, await ReadMessageAsync(response));
    }

    private static Task<HttpResponseMessage> ForgotAsync(
        HttpClient client,
        string email)
    {
        return client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email });
    }

    private static Task<HttpResponseMessage> ResetAsync(
        HttpClient client,
        string email,
        string token,
        string newPassword,
        string confirmPassword)
    {
        return client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email, token, newPassword, confirmPassword });
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        return client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });
    }

    private static async Task<(string Email, string Token)>
        CreateResetLinkAsync(
            ResolveHubApiFactory factory,
            HttpClient client,
            string email)
    {
        var response = await ForgotAsync(client, email);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var sentEmail = factory.EmailSender.Messages.Last();
        var query = QueryHelpers.ParseQuery(
            new Uri(sentEmail.ResetUrl).Query);

        return (
            query["email"].ToString(),
            query["token"].ToString());
    }

    private static async Task<string?> ReadMessageAsync(
        HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("message").GetString();
    }

    private static void AssertNoCache(HttpResponseMessage response)
    {
        Assert.Equal(
            "no-store, no-cache",
            response.Headers.CacheControl?.ToString());
        Assert.Contains("no-cache", response.Headers.Pragma.ToString());
    }
}
