using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class AuthFlowTests
{
    private const string ValidPassword = "ValidPassword1!";

    [Fact]
    public async Task ValidCredentials_ReturnBearerTokenAndSafeUser()
    {
        await using var factory = new ResolveHubApiFactory();
        var user = await factory.CreateUserAsync(
            "valid@resolvehub.test",
            ValidPassword);
        using var client = factory.CreateHttpsClient();

        var response = await LoginAsync(
            client,
            user.Email!,
            ValidPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store, no-cache",
            response.Headers.CacheControl?.ToString());

        var body =
            await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(body);
        Assert.Equal("Bearer", body.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.InRange(body.ExpiresInSeconds, 86_390, 86_400);
        Assert.Equal(user.Id, body.User.ID);
        Assert.Equal(user.Email, body.User.Email);
        Assert.Equal("Test", body.User.FirstName);
        Assert.Equal("User", body.User.LastName);
        Assert.Contains(RoleNames.Employee, body.User.Roles);
    }

    [Fact]
    public async Task ValidCredentials_IssueExpectedJwtClaimsAndLifetime()
    {
        await using var factory = new ResolveHubApiFactory();
        var user = await factory.CreateUserAsync(
            "claims@resolvehub.test",
            ValidPassword,
            RoleNames.Manager);
        using var client = factory.CreateHttpsClient();

        var response = await LoginAsync(
            client,
            user.Email!,
            ValidPassword);
        var body =
            await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(body);

        var token =
            new JwtSecurityTokenHandler()
                .ReadJwtToken(body.AccessToken);

        Assert.Equal(
            user.Id.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            token.Subject);
        Assert.Equal(
            user.Email,
            token.Claims.Single(
                claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(
            "Test",
            token.Claims.Single(
                claim => claim.Type == JwtRegisteredClaimNames.GivenName).Value);
        Assert.Equal(
            "User",
            token.Claims.Single(
                claim => claim.Type == JwtRegisteredClaimNames.FamilyName).Value);
        Assert.Contains(
            token.Claims,
            claim =>
                claim.Type == "role" &&
                claim.Value == RoleNames.Manager);
        Assert.False(
            string.IsNullOrWhiteSpace(
                token.Claims.Single(
                    claim => claim.Type == JwtRegisteredClaimNames.Jti).Value));

        var lifetime = token.ValidTo - token.ValidFrom;
        Assert.InRange(
            lifetime,
            TimeSpan.FromHours(23.99),
            TimeSpan.FromHours(24.01));

        var responseExpirationDifference =
            (body.ExpiresAtUtc.UtcDateTime - token.ValidTo)
                .Duration();

        Assert.InRange(
            responseExpirationDifference,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1));

        Assert.InRange(
            body.ExpiresAtUtc - DateTimeOffset.UtcNow,
            TimeSpan.FromHours(23.99),
            TimeSpan.FromHours(24.01));
    }

    [Fact]
    public async Task UnknownEmailAndWrongPassword_ReturnSameUnauthorizedMessage()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "known@resolvehub.test",
            ValidPassword);
        using var client = factory.CreateHttpsClient();

        var unknownResponse = await LoginAsync(
            client,
            "unknown@resolvehub.test",
            ValidPassword);
        var wrongResponse = await LoginAsync(
            client,
            "known@resolvehub.test",
            "WrongPassword1!");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            unknownResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            wrongResponse.StatusCode);
        Assert.Equal(
            await ReadMessageAsync(unknownResponse),
            await ReadMessageAsync(wrongResponse));
        Assert.Equal(
            "Invalid email or password.",
            await ReadMessageAsync(unknownResponse));
    }

    [Fact]
    public async Task InactiveAccount_ReturnsForbiddenWithoutToken()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "inactive@resolvehub.test",
            ValidPassword,
            isActive: false);
        using var client = factory.CreateHttpsClient();

        var response = await LoginAsync(
            client,
            "inactive@resolvehub.test",
            ValidPassword);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "This account is inactive. Please contact IT Support.",
            await ReadMessageAsync(response));
        Assert.DoesNotContain(
            "accessToken",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedPassword_IncrementsIdentityFailedCount()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "failed-count@resolvehub.test",
            ValidPassword);
        using var client = factory.CreateHttpsClient();

        var response = await LoginAsync(
            client,
            "failed-count@resolvehub.test",
            "WrongPassword1!");
        var user = await factory.GetUserAsync(
            "failed-count@resolvehub.test");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, user.AccessFailedCount);
    }

    [Fact]
    public async Task FifthFailedPassword_LocksAccount()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "lock-boundary@resolvehub.test",
            ValidPassword);
        using var client = factory.CreateHttpsClient();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var failedResponse = await LoginAsync(
                client,
                "lock-boundary@resolvehub.test",
                "WrongPassword1!");

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                failedResponse.StatusCode);
        }

        var fifthResponse = await LoginAsync(
            client,
            "lock-boundary@resolvehub.test",
            "WrongPassword1!");
        var user = await factory.GetUserAsync(
            "lock-boundary@resolvehub.test");

        Assert.Equal(
            (HttpStatusCode)StatusCodes.Status423Locked,
            fifthResponse.StatusCode);
        Assert.NotNull(user.LockoutEnd);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task LockedAccount_ReturnsLockoutEndAndNoToken()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "locked@resolvehub.test",
            ValidPassword);
        using var client = factory.CreateHttpsClient();

        HttpResponseMessage? response = null;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            response = await LoginAsync(
                client,
                "locked@resolvehub.test",
                "WrongPassword1!");
        }

        Assert.NotNull(response);
        Assert.Equal(
            (HttpStatusCode)StatusCodes.Status423Locked,
            response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.True(
            document.RootElement.TryGetProperty(
                "lockoutEndUtc",
                out var lockoutEnd));
        Assert.NotEqual(
            JsonValueKind.Null,
            lockoutEnd.ValueKind);
        Assert.False(
            document.RootElement.TryGetProperty(
                "accessToken",
                out _));
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsFailedAccessCount()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "reset-count@resolvehub.test",
            ValidPassword);
        using var client = factory.CreateHttpsClient();

        await LoginAsync(
            client,
            "reset-count@resolvehub.test",
            "WrongPassword1!");
        await LoginAsync(
            client,
            "reset-count@resolvehub.test",
            "WrongPassword1!");

        var response = await LoginAsync(
            client,
            "reset-count@resolvehub.test",
            ValidPassword);
        var user = await factory.GetUserAsync(
            "reset-count@resolvehub.test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task AccountWithoutRole_ReturnsGenericServerErrorWithoutToken()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "no-role@resolvehub.test",
            ValidPassword,
            roleName: null);
        using var client = factory.CreateHttpsClient();

        var response = await LoginAsync(
            client,
            "no-role@resolvehub.test",
            ValidPassword);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);
        Assert.Equal(
            "Unable to complete authentication.",
            await ReadMessageAsync(response));
        Assert.DoesNotContain(
            "accessToken",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, "ValidPassword1!")]
    [InlineData("", "ValidPassword1!")]
    [InlineData("   ", "ValidPassword1!")]
    [InlineData("not-an-email", "ValidPassword1!")]
    [InlineData("user@resolvehub.test", null)]
    [InlineData("user@resolvehub.test", "")]
    [InlineData("user@resolvehub.test", "   ")]
    public async Task InvalidLoginInput_ReturnsBadRequest(
        string? email,
        string? password)
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OversizedLoginInput_ReturnsBadRequest()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = $"{new string('a', 250)}@test.com",
                password = new string('x', 257)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequest()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();
        using var content = new StringContent(
            """{"email":"user@resolvehub.test","password":""",
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(
            "/api/auth/login",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync(
            "/api/authorization-test/authenticated");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithWrongRole_ReturnsForbidden()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "manager-role@resolvehub.test",
            ValidPassword,
            RoleNames.Manager);
        using var client = factory.CreateHttpsClient();
        var token = await GetTokenAsync(
            client,
            "manager-role@resolvehub.test");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            "/api/authorization-test/employee");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithRequiredRole_ReturnsOk()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.CreateUserAsync(
            "employee-role@resolvehub.test",
            ValidPassword,
            RoleNames.Employee);
        using var client = factory.CreateHttpsClient();
        var token = await GetTokenAsync(
            client,
            "employee-role@resolvehub.test");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            "/api/authorization-test/employee");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateExpiredToken());

        var response = await client.GetAsync(
            "/api/authorization-test/authenticated");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_IsRejected()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "not-a-valid-jwt");

        var response = await client.GetAsync(
            "/api/authorization-test/authenticated");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ExcessiveLoginRequests_ReturnTooManyRequests()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();
        HttpResponseMessage? response = null;

        for (var requestNumber = 1;
             requestNumber <= 11;
             requestNumber++)
        {
            response = await LoginAsync(
                client,
                "rate-limit-unknown@resolvehub.test",
                ValidPassword);
        }

        Assert.NotNull(response);
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            response.StatusCode);
        Assert.True(response.Headers.RetryAfter is not null);
        Assert.Equal(
            "Too many requests. Please try again later.",
            await ReadMessageAsync(response));
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        return client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password
            });
    }

    private static async Task<string> GetTokenAsync(
        HttpClient client,
        string email)
    {
        var response = await LoginAsync(
            client,
            email,
            ValidPassword);
        var body =
            await response.Content.ReadFromJsonAsync<LoginResponse>();

        return body?.AccessToken
            ?? throw new InvalidOperationException(
                "The login response did not contain a token.");
    }

    private static async Task<string?> ReadMessageAsync(
        HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        return document.RootElement
            .GetProperty("message")
            .GetString();
    }

    private static string CreateExpiredToken()
    {
        var key = new SymmetricSecurityKey(
            Convert.FromBase64String(
                ResolveHubApiFactory.JwtKey));
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: ResolveHubApiFactory.JwtIssuer,
            audience: ResolveHubApiFactory.JwtAudience,
            claims:
            [
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    "123"),
                new Claim(
                    ClaimTypes.Role,
                    RoleNames.Employee)
            ],
            notBefore: now.AddHours(-2),
            expires: now.AddHours(-1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
