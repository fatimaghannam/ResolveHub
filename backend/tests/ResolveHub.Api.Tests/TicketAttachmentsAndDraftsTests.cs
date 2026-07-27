using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Tickets;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class TicketAttachmentsAndDraftsTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task AttachmentUploadDownloadAndOwnership_AreEnforced()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var owner = await factory.CreateUserAsync("file-owner@resolvehub.test", Password);
        var other = await factory.CreateUserAsync("file-other@resolvehub.test", Password);
        using var ownerClient = await LoginAsync(factory, owner.Email!);
        using var otherClient = await LoginAsync(factory, other.Email!);
        var ticket = await CreateTicketAsync(factory, ownerClient);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("safe log content"))
        {
            Headers = { ContentType = new MediaTypeHeaderValue("text/plain") }
        }, "file", "diagnostic.log");

        var upload = await ownerClient.PostAsync(
            $"/api/tickets/{ticket.Id}/attachments", content);
        var attachment = await upload.Content.ReadFromJsonAsync<TicketAttachmentDto>();
        var download = await ownerClient.GetAsync(
            $"/api/tickets/{ticket.Id}/attachments/{attachment!.Id}/download");
        var forbidden = await otherClient.GetAsync(
            $"/api/tickets/{ticket.Id}/attachments/{attachment.Id}/download");

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    [Theory]
    [InlineData("malware.exe", "application/octet-stream", 20)]
    [InlineData("large.txt", "text/plain", 10485761)]
    public async Task InvalidAttachment_IsRejected(
        string fileName, string contentType, int size)
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var owner = await factory.CreateUserAsync(
            $"invalid-{Guid.NewGuid():N}@resolvehub.test", Password);
        using var client = await LoginAsync(factory, owner.Email!);
        var ticket = await CreateTicketAsync(factory, client);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[size])
        {
            Headers = { ContentType = new MediaTypeHeaderValue(contentType) }
        }, "file", fileName);

        var response = await client.PostAsync(
            $"/api/tickets/{ticket.Id}/attachments", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Draft_AllowsIncompleteData_IsOwnerOnly_AndDoesNotAffectCounts()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var owner = await factory.CreateUserAsync("draft-owner@resolvehub.test", Password);
        var other = await factory.CreateUserAsync("draft-other@resolvehub.test", Password);
        using var ownerClient = await LoginAsync(factory, owner.Email!);
        using var otherClient = await LoginAsync(factory, other.Email!);

        var save = await ownerClient.PostAsJsonAsync(
            "/api/ticket-drafts", new { title = "Incomplete draft" });
        var draft = await save.Content.ReadFromJsonAsync<TicketDraftDto>();
        var otherRead = await otherClient.GetAsync($"/api/ticket-drafts/{draft!.Id}");
        var dashboard = await ownerClient.GetFromJsonAsync<TicketDashboardSummaryDto>(
            "/api/employee/dashboard");

        Assert.Equal(HttpStatusCode.Created, save.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherRead.StatusCode);
        Assert.Equal(0, dashboard!.TotalTickets);
    }

    [Fact]
    public async Task GetDrafts_ReturnsEmptyArrayForNewEmployee()
    {
        await using var factory = new ResolveHubApiFactory();
        var owner = await factory.CreateUserAsync(
            "draft-empty@resolvehub.test", Password);
        using var client = await LoginAsync(factory, owner.Email!);

        var response = await client.GetAsync("/api/ticket-drafts");
        var drafts = await response.Content
            .ReadFromJsonAsync<IReadOnlyCollection<TicketDraftDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(drafts);
        Assert.Empty(drafts);
    }

    [Fact]
    public async Task GetDrafts_ReturnsOnlyAuthenticatedEmployeesDrafts()
    {
        await using var factory = new ResolveHubApiFactory();
        var owner = await factory.CreateUserAsync(
            "draft-list-owner@resolvehub.test", Password);
        var other = await factory.CreateUserAsync(
            "draft-list-other@resolvehub.test", Password);
        using var ownerClient = await LoginAsync(factory, owner.Email!);
        using var otherClient = await LoginAsync(factory, other.Email!);

        await ownerClient.PostAsJsonAsync(
            "/api/ticket-drafts", new { title = "Owner draft" });
        await otherClient.PostAsJsonAsync(
            "/api/ticket-drafts", new { title = "Other draft" });

        var response = await ownerClient.GetAsync("/api/ticket-drafts");
        var drafts = await response.Content
            .ReadFromJsonAsync<IReadOnlyCollection<TicketDraftDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = Assert.Single(drafts!);
        Assert.Equal("Owner draft", draft.Title);
    }

    [Fact]
    public async Task DraftEndpoints_RequireEmployeeAuthentication()
    {
        await using var factory = new ResolveHubApiFactory();
        using var anonymousClient = factory.CreateHttpsClient();
        var manager = await factory.CreateUserAsync(
            "draft-manager@resolvehub.test", Password, RoleNames.Manager);
        using var managerClient = await LoginAsync(factory, manager.Email!);

        var anonymousResponse = await anonymousClient.GetAsync(
            "/api/ticket-drafts");
        var managerResponse = await managerClient.GetAsync(
            "/api/ticket-drafts");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDeleteDraft_ChangeOnlyTheOwnedDraftRecord()
    {
        await using var factory = new ResolveHubApiFactory();
        var owner = await factory.CreateUserAsync(
            "draft-update@resolvehub.test", Password);
        using var client = await LoginAsync(factory, owner.Email!);
        var save = await client.PostAsJsonAsync(
            "/api/ticket-drafts", new { title = "Before update" });
        var draft = await save.Content.ReadFromJsonAsync<TicketDraftDto>();

        var update = await client.PutAsJsonAsync(
            $"/api/ticket-drafts/{draft!.Id}",
            new { title = "After update", description = "Saved details" });
        var updated = await update.Content.ReadFromJsonAsync<TicketDraftDto>();
        var delete = await client.DeleteAsync(
            $"/api/ticket-drafts/{draft.Id}");
        var missing = await client.GetAsync(
            $"/api/ticket-drafts/{draft.Id}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("After update", updated!.Title);
        Assert.Equal("Saved details", updated.Description);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task SubmittingValidDraft_CreatesOpenTicketAndRemovesDraft()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var owner = await factory.CreateUserAsync("draft-submit@resolvehub.test", Password);
        using var client = await LoginAsync(factory, owner.Email!);
        var lookups = await factory.GetTicketLookupIdsAsync();
        var save = await client.PostAsJsonAsync("/api/ticket-drafts", new
        {
            title = "Complete draft ticket",
            description = "This draft contains enough information to submit.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        var draft = await save.Content.ReadFromJsonAsync<TicketDraftDto>();

        var submit = await client.PostAsync(
            $"/api/ticket-drafts/{draft!.Id}/submit", null);
        var ticket = await submit.Content.ReadFromJsonAsync<TicketDetailsDto>();
        var missingDraft = await client.GetAsync($"/api/ticket-drafts/{draft.Id}");

        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal("Open", ticket!.StatusName);
        Assert.Equal(HttpStatusCode.NotFound, missingDraft.StatusCode);
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

    private static async Task<TicketDetailsDto> CreateTicketAsync(
        ResolveHubApiFactory factory, HttpClient client)
    {
        var lookups = await factory.GetTicketLookupIdsAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Attachment integration ticket",
            description = "This ticket is used for secure attachment integration tests.",
            ticketCategoryId = lookups.CategoryId,
            ticketPriorityId = lookups.PriorityId
        });
        return (await response.Content.ReadFromJsonAsync<TicketDetailsDto>())!;
    }
}
