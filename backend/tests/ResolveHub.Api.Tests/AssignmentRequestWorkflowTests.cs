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
    public async Task ManagerRequest_AdminApproval_AssignsOnlyAfterApprovalAndRecordsAudit()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "request-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "request-owner@resolvehub.test", Password, RoleNames.Employee);
        var requestedAgent = await factory.CreateUserAsync(
            "request-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var admin = await factory.CreateUserAsync(
            "request-admin@resolvehub.test", Password, RoleNames.Admin);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var agentClient = await LoginAsync(factory, requestedAgent.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);

        var forbiddenAgentRequest = await agentClient.PostAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/assignment-requests", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAgentRequest.StatusCode);
        var requested = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/assignment-requests",
            new { agentUserId = requestedAgent.Id });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var request = (await requested.Content.ReadFromJsonAsync<TicketAssignmentRequestDto>())!;
        Assert.Equal(requestedAgent.Id, request.RequestedAgentUserAccountId);
        Assert.Equal(manager.Id, request.RequestedByUserAccountId);
        Assert.Equal(HttpStatusCode.Forbidden, (await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = requestedAgent.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/assignment-requests",
            new { agentUserId = requestedAgent.Id })).StatusCode);

        using (var beforeScope = factory.Services.CreateScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var storedTicket = await beforeDb.Tickets.SingleAsync(item => item.ID == ticket.Id);
            Assert.Null(storedTicket.AssignedToUserAccountID);
            Assert.Equal(TicketStatusNames.Open, (await beforeDb.TicketStatuses
                .SingleAsync(item => item.ID == storedTicket.TicketStatusID)).Name);
        }
        var requests = await adminClient.GetFromJsonAsync<
            IReadOnlyCollection<TicketAssignmentRequestDto>>(
                "/api/admin/assignment-requests");
        Assert.Equal(request.Id, Assert.Single(requests!).Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await managerClient.PostAsync(
            $"/api/admin/assignment-requests/{request.Id}/approve", null)).StatusCode);

        var approve = await adminClient.PostAsJsonAsync(
            $"/api/admin/assignment-requests/{request.Id}/approve", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agentClient.GetAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assigned = await db.Tickets.Include(item => item.TicketStatus)
            .SingleAsync(item => item.ID == ticket.Id);
        Assert.Equal(requestedAgent.Id, assigned.AssignedToUserAccountID);
        Assert.Equal(TicketStatusNames.Assigned, assigned.TicketStatus.Name);
        var actions = await db.TicketHistory
            .Where(item => item.TicketID == ticket.Id)
            .Select(item => item.ActionType).ToListAsync();
        Assert.Contains(TicketHistoryActionNames.AssignmentRequested, actions);
        Assert.Contains(TicketHistoryActionNames.AssignmentRequestApproved, actions);
        Assert.Contains(TicketHistoryActionNames.TicketAssigned, actions);
        Assert.True(await db.UserNotifications.AnyAsync(item =>
            item.UserAccountID == manager.Id && item.Title == "Assignment Request Approved"));
        Assert.True(await db.UserNotifications.AnyAsync(item =>
            item.UserAccountID == requestedAgent.Id && item.Title == "Ticket Assigned"));
    }

    [Fact]
    public async Task Approval_RechecksCapacity_AndRejectionPreservesUnassignedTicket()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "capacity-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "capacity-owner@resolvehub.test", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "capacity-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var admin = await factory.CreateUserAsync(
            "capacity-admin@resolvehub.test", Password, RoleNames.Admin);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        var requestedTicket = await CreateTicketAsync(factory, employeeClient);
        var requestResponse = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{requestedTicket.TicketReferenceNumber}/assignment-requests",
            new { agentUserId = agent.Id });
        requestResponse.EnsureSuccessStatusCode();
        var request = (await requestResponse.Content
            .ReadFromJsonAsync<TicketAssignmentRequestDto>())!;

        for (var index = 0; index < TicketWorkloadRules.MaxActiveTicketsPerAgent; index++)
        {
            var capacityTicket = await CreateTicketAsync(factory, employeeClient);
            Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync(
                $"/api/admin/tickets/{capacityTicket.TicketReferenceNumber}/assign",
                new { agentUserId = agent.Id })).StatusCode);
        }

        var approval = await adminClient.PostAsJsonAsync(
            $"/api/admin/assignment-requests/{request.Id}/approve",
            new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, approval.StatusCode);
        using (var pendingScope = factory.Services.CreateScope())
        {
            var pendingDb = pendingScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(AssignmentRequestStatusNames.Pending,
                (await pendingDb.TicketAssignmentRequests.SingleAsync(item => item.ID == request.Id)).Status);
            Assert.Null((await pendingDb.Tickets.SingleAsync(item =>
                item.ID == requestedTicket.Id)).AssignedToUserAccountID);
        }

        var rejection = await adminClient.PostAsJsonAsync(
            $"/api/admin/assignment-requests/{request.Id}/reject",
            new { reason = "Agent capacity changed before approval." });
        Assert.Equal(HttpStatusCode.NoContent, rejection.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rejected = await db.TicketAssignmentRequests.SingleAsync(item => item.ID == request.Id);
        Assert.Equal(AssignmentRequestStatusNames.Rejected, rejected.Status);
        Assert.Equal("Agent capacity changed before approval.", rejected.ReviewReason);
        Assert.Null((await db.Tickets.SingleAsync(item =>
            item.ID == requestedTicket.Id)).AssignedToUserAccountID);
        Assert.True(await db.UserNotifications.AnyAsync(item =>
            item.UserAccountID == manager.Id && item.Title == "Assignment Request Rejected" &&
            item.Message.Contains("Agent capacity changed")));
    }

    [Fact]
    public async Task CommentVisibility_IsEnforcedForEveryAuthorizedViewer()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var manager = await factory.CreateUserAsync(
            "comment-manager@resolvehub.test", Password, RoleNames.Manager);
        var employee = await factory.CreateUserAsync(
            "comment-owner@resolvehub.test", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "comment-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var otherEmployee = await factory.CreateUserAsync(
            "comment-other-employee@resolvehub.test", Password, RoleNames.Employee);
        var admin = await factory.CreateUserAsync(
            "comment-admin@resolvehub.test", Password, RoleNames.Admin);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        using var otherEmployeeClient = await LoginAsync(factory, otherEmployee.Email!);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);
        var statusChange = await agentClient.PatchAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/status",
            new { statusId = 999 });
        Assert.Equal(HttpStatusCode.NotFound, statusChange.StatusCode);
        var unassignedAgentComment = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Must not be accepted.", visibility = "Public" });
        Assert.Equal(HttpStatusCode.NotFound, unassignedAgentComment.StatusCode);

        var employeePublic = await employeeClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments",
            new { message = "Public employee update.", visibility = "Public" });
        var employeePrivate = await employeeClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments",
            new { message = "Private employee update.", visibility = "Private" });
        var comment = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Manager is coordinating this request.", visibility = "Public" });
        var adminComment = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Administrator public update.", visibility = "Public" });
        var managerPrivate = await managerClient.PostAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Manager private attempt.", visibility = "Private" });
        var adminPrivate = await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/comments",
            new { message = "Administrator private attempt.", visibility = "Private" });
        var emptyComment = await employeeClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments",
            new { message = "   ", visibility = "Public" });
        var invalidVisibility = await employeeClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments",
            new { message = "Invalid visibility.", visibility = "Internal" });
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
        Assert.Equal(HttpStatusCode.Created, adminComment.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, managerPrivate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminPrivate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, employeePublic.StatusCode);
        Assert.Equal(HttpStatusCode.Created, employeePrivate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyComment.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidVisibility.StatusCode);

        var managerDetails = await managerClient.GetFromJsonAsync<AdminTicketDetailsDto>(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}");
        var adminDetails = await adminClient.GetFromJsonAsync<AdminTicketDetailsDto>(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}");
        var employeeDetails = await employeeClient.GetFromJsonAsync<TicketDetailsDto>(
            $"/api/tickets/{ticket.Id}");
        var openAgentDetails = await agentClient.GetFromJsonAsync<AgentTicketDetailsDto>(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}");
        var otherEmployeeDetails = await otherEmployeeClient.GetAsync(
            $"/api/tickets/{ticket.Id}");

        Assert.DoesNotContain(managerDetails!.Comments,
            item => item.Content == "Private employee update.");
        Assert.DoesNotContain(adminDetails!.Comments,
            item => item.Content == "Private employee update.");
        Assert.DoesNotContain(openAgentDetails!.Comments,
            item => item.Content == "Private employee update.");
        Assert.Contains(employeeDetails!.Comments,
            item => item.Content == "Private employee update.");
        Assert.Contains(managerDetails.Comments,
            item => item.Content == "Manager is coordinating this request.");
        Assert.All(managerDetails.Comments,
            item => Assert.Equal(nameof(CommentVisibility.Public), item.Visibility));
        Assert.All(adminDetails.Comments,
            item => Assert.Equal(nameof(CommentVisibility.Public), item.Visibility));
        Assert.Contains(openAgentDetails.Comments,
            item => item.Content == "Public employee update.");
        Assert.DoesNotContain(openAgentDetails.History,
            item => item.NewValue == nameof(CommentVisibility.Private));
        Assert.Equal(HttpStatusCode.NotFound, otherEmployeeDetails.StatusCode);
        Assert.Contains(managerDetails.History,
            item => item.ActionType == TicketHistoryActionNames.CommentAdded);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var privateHistory = await db.TicketHistory.SingleAsync(item =>
            item.TicketID == ticket.Id && item.NewValue == "Private");
        Assert.True(privateHistory.IsInternal);
        Assert.DoesNotContain("Private employee update.", privateHistory.Description ?? "");
    }

    [Fact]
    public async Task ThreadedComments_EnforceVisibilityOwnershipDepthAndSoftDelete()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "thread-owner@resolvehub.test", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "thread-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var manager = await factory.CreateUserAsync(
            "thread-manager@resolvehub.test", Password, RoleNames.Manager);
        var admin = await factory.CreateUserAsync(
            "thread-admin@resolvehub.test", Password, RoleNames.Admin);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = agent.Id })).StatusCode);

        var parentResponse = await employeeClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments",
            new { message = "Private diagnostic context.", visibility = "Private" });
        Assert.Equal(HttpStatusCode.Created, parentResponse.StatusCode);
        var parent = (await parentResponse.Content.ReadFromJsonAsync<TicketCommentDto>())!;
        var replyResponse = await agentClient.PostAsJsonAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments/{parent.Id}/replies",
            new { message = "Private assigned-agent reply.", visibility = "Public" });
        Assert.Equal(HttpStatusCode.OK, replyResponse.StatusCode);
        var reply = (await replyResponse.Content.ReadFromJsonAsync<TicketCommentDto>())!;
        Assert.Equal(parent.Id, reply.ParentCommentId);
        Assert.Equal(nameof(CommentVisibility.Private), reply.Visibility);

        Assert.Equal(HttpStatusCode.BadRequest, (await employeeClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments/{reply.Id}/replies",
            new { message = "A forbidden second-level reply." })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await managerClient.PutAsJsonAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/comments/{parent.Id}",
            new { message = "Manager must not edit another user's comment." })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await managerClient.DeleteAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/comments/{parent.Id}"))
            .StatusCode);
        var edited = await employeeClient.PutAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments/{parent.Id}",
            new { message = "Updated private diagnostic context." });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.True((await edited.Content.ReadFromJsonAsync<TicketCommentDto>())!.IsEdited);

        var managerComments = (await managerClient.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/comments"))!.Items;
        var adminComments = (await adminClient.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/comments"))!.Items;
        Assert.Empty(managerComments);
        Assert.Empty(adminComments);

        var ownerCommentsBeforeDelete = (await employeeClient.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/tickets/{ticket.Id}/comments"))!.Items;
        Assert.False(Assert.Single(ownerCommentsBeforeDelete, item => item.Id == parent.Id).CanDelete);
        Assert.False(Assert.Single(ownerCommentsBeforeDelete, item => item.Id == reply.Id).CanDelete);
        var agentCommentsBeforeDelete = (await agentClient.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments"))!.Items;
        Assert.True(Assert.Single(agentCommentsBeforeDelete, item => item.Id == reply.Id).CanDelete);

        var rejectedDelete = await employeeClient.DeleteAsync(
            $"/api/tickets/{ticket.Id}/comments/{parent.Id}");
        var rejectedDeleteBody = await rejectedDelete.Content.ReadAsStringAsync();
        Assert.True(rejectedDelete.StatusCode == HttpStatusCode.Conflict,
            $"Expected 409 Conflict but received {(int)rejectedDelete.StatusCode}: {rejectedDeleteBody}");
        Assert.Contains("cannot be deleted because it has replies",
            rejectedDeleteBody,
            StringComparison.OrdinalIgnoreCase);
        var ownerComments = (await employeeClient.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/tickets/{ticket.Id}/comments"))!.Items;
        Assert.Equal(2, ownerComments.Count);
        Assert.False(Assert.Single(ownerComments, item => item.Id == parent.Id).IsDeleted);
        Assert.Contains(ownerComments, item => item.ParentCommentId == parent.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.TicketComments.SingleAsync(item => item.ID == parent.Id);
        Assert.False(stored.IsDeleted);
        Assert.Null(stored.DeletedDate);
        Assert.False((await db.TicketComments.SingleAsync(item => item.ID == reply.Id)).IsDeleted);
        Assert.False(await db.UserNotifications.AnyAsync(item =>
            (item.UserAccountID == manager.Id || item.UserAccountID == admin.Id) &&
            item.TicketReferenceNumber == ticket.TicketReferenceNumber));

        Assert.Equal(HttpStatusCode.NoContent, (await agentClient.DeleteAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments/{reply.Id}"))
            .StatusCode);

        var afterReplyDelete = (await employeeClient.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/tickets/{ticket.Id}/comments"))!.Items;
        Assert.True(Assert.Single(afterReplyDelete, item => item.Id == reply.Id).IsDeleted);
        Assert.True(Assert.Single(afterReplyDelete, item => item.Id == parent.Id).CanDelete);
        Assert.Equal(HttpStatusCode.NoContent, (await employeeClient.DeleteAsync(
            $"/api/tickets/{ticket.Id}/comments/{parent.Id}")).StatusCode);

        var publicResponse = await employeeClient.PostAsJsonAsync(
            $"/api/tickets/{ticket.Id}/comments",
            new { message = "Public comment without replies.", visibility = "Public" });
        Assert.Equal(HttpStatusCode.Created, publicResponse.StatusCode);
        var publicComment = (await publicResponse.Content.ReadFromJsonAsync<TicketCommentDto>())!;
        Assert.True(publicComment.CanDelete);
        Assert.Equal(HttpStatusCode.NoContent, (await employeeClient.DeleteAsync(
            $"/api/tickets/{ticket.Id}/comments/{publicComment.Id}")).StatusCode);

        var afterValidDeletes = (await employeeClient.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/tickets/{ticket.Id}/comments"))!.Items;
        Assert.Equal(3, afterValidDeletes.Count);
        Assert.All(afterValidDeletes, item => Assert.True(item.IsDeleted));
        db.ChangeTracker.Clear();
        Assert.True((await db.TicketComments.SingleAsync(item => item.ID == parent.Id)).IsDeleted);
        Assert.True((await db.TicketComments.SingleAsync(item => item.ID == reply.Id)).IsDeleted);
        Assert.True((await db.TicketComments.SingleAsync(item => item.ID == publicComment.Id)).IsDeleted);
    }

    [Fact]
    public async Task CommentAttachments_ValidateFilesAndEnforceCommentVisibility()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "attachment-owner@resolvehub.test", Password, RoleNames.Employee);
        var agent = await factory.CreateUserAsync(
            "attachment-agent@resolvehub.test", Password, RoleNames.ITSupportAgent);
        var manager = await factory.CreateUserAsync(
            "attachment-manager@resolvehub.test", Password, RoleNames.Manager);
        var admin = await factory.CreateUserAsync(
            "attachment-admin@resolvehub.test", Password, RoleNames.Admin);
        using var employeeClient = await LoginAsync(factory, employee.Email!);
        using var agentClient = await LoginAsync(factory, agent.Email!);
        using var managerClient = await LoginAsync(factory, manager.Email!);
        using var adminClient = await LoginAsync(factory, admin.Email!);
        var ticket = await CreateTicketAsync(factory, employeeClient);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/assign",
            new { agentUserId = agent.Id })).StatusCode);

        using var multipartComment = new MultipartFormDataContent();
        multipartComment.Add(new StringContent("Private attachment context."), "Content");
        multipartComment.Add(new StringContent("Private"), "Visibility");
        using var image = new ByteArrayContent(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipartComment.Add(image, "Attachments", "diagnostic.png");
        using var report = new ByteArrayContent("%PDF-1.7 private report"u8.ToArray());
        report.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipartComment.Add(report, "Attachments", "private-report.pdf");
        var privateResponse = await employeeClient.PostAsync(
            $"/api/tickets/{ticket.Id}/comments", multipartComment);
        privateResponse.EnsureSuccessStatusCode();
        var privateComment = (await privateResponse.Content.ReadFromJsonAsync<TicketCommentDto>())!;
        Assert.Equal(2, privateComment.Attachments!.Count);
        var attachment = privateComment.Attachments.Single(item =>
            item.FileName == "private-report.pdf");

        Assert.Equal(HttpStatusCode.OK, (await employeeClient.GetAsync(
            $"/api/tickets/{ticket.Id}/comments/{privateComment.Id}/attachments/{attachment.Id}"))
            .StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agentClient.GetAsync(
            $"/api/agent/tickets/{ticket.TicketReferenceNumber}/comments/{privateComment.Id}/attachments/{attachment.Id}"))
            .StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await managerClient.GetAsync(
            $"/api/manager/tickets/{ticket.TicketReferenceNumber}/comments/{privateComment.Id}/attachments/{attachment.Id}"))
            .StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync(
            $"/api/admin/tickets/{ticket.TicketReferenceNumber}/comments/{privateComment.Id}/attachments/{attachment.Id}"))
            .StatusCode);

        using var invalidUpload = new MultipartFormDataContent();
        using var executable = new ByteArrayContent("not executable"u8.ToArray());
        executable.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        invalidUpload.Add(executable, "file", "unsafe.exe");
        Assert.Equal(HttpStatusCode.BadRequest, (await employeeClient.PostAsync(
            $"/api/tickets/{ticket.Id}/comments/{privateComment.Id}/attachments", invalidUpload))
            .StatusCode);

        var refreshed = (await employeeClient.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/tickets/{ticket.Id}/comments"))!;
        var storedComment = Assert.Single(refreshed.Items, item => item.Id == privateComment.Id);
        Assert.Equal(2, storedComment.Attachments!.Count);
        Assert.Contains(storedComment.Attachments,
            item => item.FileName == "private-report.pdf");
        Assert.Contains(storedComment.Attachments,
            item => item.FileName == "diagnostic.png");
    }

    [Fact]
    public async Task Comments_PaginateVisibleTopLevelThreads_WithRepliesAndCounts()
    {
        await using var factory = new ResolveHubApiFactory();
        await factory.SeedTicketLookupsAsync();
        var employee = await factory.CreateUserAsync(
            "pagination-owner@resolvehub.test", Password, RoleNames.Employee);
        using var client = await LoginAsync(factory, employee.Email!);
        var ticket = await CreateTicketAsync(factory, client);
        TicketCommentDto? first = null;
        for (var index = 1; index <= 31; index++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/tickets/{ticket.Id}/comments",
                new { message = $"Timeline comment {index}.", visibility = "Public" });
            response.EnsureSuccessStatusCode();
            first ??= await response.Content.ReadFromJsonAsync<TicketCommentDto>();
        }
        for (var index = 1; index <= 3; index++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/tickets/{ticket.Id}/comments/{first!.Id}/replies",
                new { message = $"Attached reply {index}." });
            response.EnsureSuccessStatusCode();
        }

        var pageOne = (await client.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/tickets/{ticket.Id}/comments?page=1&pageSize=15"))!;
        var pageTwo = (await client.GetFromJsonAsync<TicketCommentPageDto>(
            $"/api/tickets/{ticket.Id}/comments?page=2&pageSize=15"))!;
        Assert.Equal(15, pageOne.Items.Count(item => item.ParentCommentId is null));
        Assert.Equal(3, pageOne.Items.Count(item => item.ParentCommentId == first!.Id));
        Assert.Equal(15, pageTwo.Items.Count(item => item.ParentCommentId is null));
        Assert.Equal(31, pageOne.TotalThreads);
        Assert.Equal(34, pageOne.TotalVisibleComments);
        Assert.Equal(34, pageOne.PublicCount);
        Assert.True(pageOne.HasMore);
        Assert.True(pageTwo.HasMore);
        Assert.Empty(pageOne.Items.Select(item => item.Id)
            .Intersect(pageTwo.Items.Select(item => item.Id)));
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
