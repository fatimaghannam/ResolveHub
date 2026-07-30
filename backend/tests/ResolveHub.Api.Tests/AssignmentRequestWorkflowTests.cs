using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using Xunit;

namespace ResolveHub.Api.Tests;

public sealed class AssignmentRequestWorkflowTests
{
    private const string Password = "ValidPassword1!";

    [Fact]
    public async Task OpenTicket_RequestApproval_RestrictsVisibilityAndRecordsHistory()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "request-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "request-owner@resolvehub.test", Password, RoleNames.Employee);
        var requestingAgent = await factory.CreateUserAsync(
            "request-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var otherAgent = await factory.CreateUserAsync(
            "other-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var requestingClient = await LoginAsync(factory, requestingAgent.Email!);
        using var otherClient = await LoginAsync(factory, otherAgent.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);

        var before = await requestingClient.GetFromJsonAsync<
            PagedResultDto<AgentTicketListItemDto>>("/api/agent/tickets/open");
        Assert.Contains(before!.Items,
            item => item.TicketReferenceNumber == ticket.TicketReferenceNumber);

        var requested = await requestingClient.PostAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/assignment-requests",
            null);
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var requests = await managerClient.GetFromJsonAsync<
            IReadOnlyCollection<TicketAssignmentRequestDto>>(
                "/api/manager/assignment-requests");
        var request = Assert.Single(requests!);
        Assert.Equal(requestingAgent.Id, request.RequestedByUserAccountId);

        var approve = await managerClient.PostAsync(
            $"/api/manager/assignment-requests/{request.Id}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await requestingClient.GetAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actions = await db.TicketHistory
            .Where(item => item.TicketID == ticket.Id)
            .Select(item => item.ActionType).ToListAsync();
        Assert.Contains(TicketHistoryActionNames.AssignmentRequested, actions);
        Assert.Contains(TicketHistoryActionNames.AssignmentRequestApproved, actions);
        Assert.Contains(TicketHistoryActionNames.TicketAssigned, actions);
    }

    [Fact]
    public async Task Manager_CanCommentOnAnyTicket_AndAgentCannotWorkBeforeApproval()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "comment-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "comment-owner@resolvehub.test", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "comment-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);
        var statusChange = await agentClient.PatchAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/status",
            new { statusId = 999 });
        Assert.Equal(HttpStatusCode.NotFound, statusChange.StatusCode);

        var comment = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/comments",
            new { content = "Manager is coordinating this request." });
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
        var details = await managerClient.GetFromJsonAsync<AdminTicketDetailsDto>(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}");
        Assert.Contains(details!.Comments,
            item => item.Content == "Manager is coordinating this request.");
        Assert.Contains(details.History,
            item => item.ActionType == TicketHistoryActionNames.ManagerCommentAdded);
    }

    private static async Task<TicketDetailsDto> CreateTicketAsync(
        ResolveHubApiFactory factory, HttpClient client)
    {
        var lookups = await factory.GetTicketLookupIdsAsync();
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Assignment request workflow",
            description = "Validates secure Agent visibility and Manager approval.",
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
