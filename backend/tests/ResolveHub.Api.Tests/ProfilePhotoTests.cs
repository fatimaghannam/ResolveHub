using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Profile;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class ProfilePhotoTests
{
    private const string Password = "ValidPassword1!";
    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    [Theory]
    [InlineData(RoleNames.Employee)]
    [InlineData(RoleNames.ITSupportAgent)]
    [InlineData(RoleNames.Manager)]
    [InlineData(RoleNames.Admin)]
    public async Task AuthenticatedUser_CanUploadReplaceAndRemoveOwnPhoto(string role)
    {
        await using var factory = new ResolveHubApiFactory();
        var user = await factory.CreateUserAsync(
            $"photo-{role.Replace(" ", "-").ToLowerInvariant()}@resolvehub.test",
            Password, role);
        using var client = await LoginAsync(factory, user.Email!);

        var first = await UploadAsync(client, Png, "first.png", "image/png");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<ProfilePhotoResponse>();
        Assert.StartsWith("/profile-images/profile_", firstBody!.ProfileImagePath);
        Assert.EndsWith(".png", firstBody.ProfileImagePath);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync(firstBody.ProfileImagePath)).StatusCode);

        var second = await UploadAsync(client, Png, "second.png", "image/png");
        var secondBody = await second.Content.ReadFromJsonAsync<ProfilePhotoResponse>();
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(firstBody.ProfileImagePath, secondBody!.ProfileImagePath);

        using var freshClient = factory.CreateHttpsClient();
        var loginResponse = await freshClient.PostAsJsonAsync(
            "/api/auth/login", new { email = user.Email, password = Password });
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal(secondBody.ProfileImagePath, login!.User.ProfileImagePath);

        var remove = await client.DeleteAsync("/api/profile/photo");
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        Assert.Null((await remove.Content.ReadFromJsonAsync<ProfilePhotoResponse>())!.ProfileImagePath);

        using var scope = factory.Services.CreateScope();
        var storedUser = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
        Assert.Null(storedUser.ProfileImagePath);
    }

    [Fact]
    public async Task Upload_RejectsInvalidSignatureAndOversizedFile_WithoutChangingPhoto()
    {
        await using var factory = new ResolveHubApiFactory();
        var user = await factory.CreateUserAsync("invalid-photo@resolvehub.test", Password);
        using var client = await LoginAsync(factory, user.Email!);

        var invalid = await UploadAsync(client, [1, 2, 3, 4], "fake.png", "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("JPG, PNG, or WebP", await invalid.Content.ReadAsStringAsync());

        var oversized = new byte[5 * 1024 * 1024 + 1];
        Array.Copy(Png, oversized, Png.Length);
        var tooLarge = await UploadAsync(client, oversized, "large.png", "image/png");
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
        Assert.Contains("smaller than 5 MB", await tooLarge.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        Assert.Null((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Users.AsNoTracking().SingleAsync(item => item.Id == user.Id)).ProfileImagePath);
    }

    [Fact]
    public async Task PhotoEndpoints_RequireAuthentication_AndAcceptNoUserId()
    {
        await using var factory = new ResolveHubApiFactory();
        using var client = factory.CreateHttpsClient();
        var response = await UploadAsync(client, Png, "photo.png", "image/png");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.DeleteAsync("/api/profile/photo")).StatusCode);
    }

    private static async Task<HttpClient> LoginAsync(
        ResolveHubApiFactory factory, string email)
    {
        var client = factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = Password });
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client, byte[] bytes, string fileName, string contentType)
    {
        var multipart = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(content, "photo", fileName);
        return client.PostAsync("/api/profile/photo", multipart);
    }
}
