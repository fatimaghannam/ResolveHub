using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AssignmentApprovalService(
    ApplicationDbContext dbContext,
    IAdminTicketService adminTicketService) : IAssignmentApprovalService
{
    public async Task<TicketServiceResult<TicketAssignmentRequestDto>> CreateAsync(
        int managerId, string ticketReference, int agentUserId, CancellationToken token)
    {
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item => item.TicketReferenceNumber == ticketReference &&
                !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (ticket.AssignedToUserAccountID.HasValue ||
            ticket.TicketStatus.Name != TicketStatusNames.Open)
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket is no longer open for assignment.");
        if (await dbContext.TicketAssignmentRequests.AnyAsync(item =>
                item.TicketID == ticket.ID &&
                item.Status == AssignmentRequestStatusNames.Pending, token))
            return new(TicketOperationStatus.Conflict,
                Message: "An assignment request is already pending for this ticket.");

        var agent = await dbContext.Users.AsNoTracking().Where(user =>
                user.Id == agentUserId && user.IsActive &&
                user.UserAccountRoles.Any(role =>
                    role.Role.Name == RoleNames.ITSupportAgent))
            .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName })
            .SingleOrDefaultAsync(token);
        if (agent is null)
            return new(TicketOperationStatus.Invalid,
                Message: "Select an active IT Support Agent.");
        var activeCount = await ActiveCountAsync(agentUserId, token);
        if (TicketWorkloadRules.IsAtCapacity(activeCount))
            return new(TicketOperationStatus.Conflict,
                Message: $"This IT Agent has reached the maximum workload of {TicketWorkloadRules.MaxActiveTicketsPerAgent} active tickets.");

        var managerName = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == managerId)
            .Select(user => user.FirstName + " " + user.LastName)
            .SingleAsync(token);
        var now = DateTime.UtcNow;
        var request = new TicketAssignmentRequest
        {
            TicketID = ticket.ID,
            RequestedByUserAccountID = managerId,
            RequestedAgentUserAccountID = agentUserId,
            RequestedDate = now
        };
        dbContext.TicketAssignmentRequests.Add(request);
        AddAudit(ticket, managerId, TicketHistoryActionNames.AssignmentRequested,
            null, agent.Name,
            $"{managerName} requested assignment to {agent.Name} for administrator approval.", now);
        var administratorIds = await dbContext.UserRoles.AsNoTracking()
            .Where(item => item.Role.Name == RoleNames.Admin && item.UserAccount.IsActive)
            .Select(item => item.UserId).Distinct().ToListAsync(token);
        foreach (var administratorId in administratorIds)
            Notify(administratorId, ticket, NotificationTypeNames.AssignmentRequestCreated,
                "Assignment Request Pending",
                $"{managerName} requested {ticket.TicketReferenceNumber} be assigned to {agent.Name}.", now);
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success,
            ToDto(request, ticket, managerName, agent.Name, activeCount, null));
    }

    public async Task<IReadOnlyCollection<TicketAssignmentRequestDto>>
        GetManagerRequestsAsync(int managerId, CancellationToken token) =>
        await Project(dbContext.TicketAssignmentRequests.AsNoTracking()
                .Where(item => item.RequestedByUserAccountID == managerId)
                .OrderByDescending(item => item.RequestedDate)
                .Take(100))
            .ToListAsync(token);

    public async Task<IReadOnlyCollection<TicketAssignmentRequestDto>>
        GetPendingAdminRequestsAsync(CancellationToken token) =>
        await Project(dbContext.TicketAssignmentRequests.AsNoTracking()
                .Where(item => item.Status == AssignmentRequestStatusNames.Pending &&
                    item.RequestedAgentUserAccountID.HasValue && !item.Ticket.IsDeleted &&
                    !item.Ticket.AssignedToUserAccountID.HasValue)
                .OrderBy(item => item.RequestedDate))
            .ToListAsync(token);

    public async Task<TicketServiceResult<bool>> ReviewAsync(
        int administratorId, int requestId, bool approve, string? reason,
        CancellationToken token)
    {
        var rejectionReason = reason?.Trim();
        if (!approve && string.IsNullOrWhiteSpace(rejectionReason))
            return new(TicketOperationStatus.Invalid,
                Message: "A rejection reason is required.");
        var request = await dbContext.TicketAssignmentRequests
            .Include(item => item.Ticket)
            .Include(item => item.RequestedByUserAccount)
            .Include(item => item.RequestedAgentUserAccount)
            .SingleOrDefaultAsync(item => item.ID == requestId, token);
        if (request is null) return new(TicketOperationStatus.NotFound);
        if (request.Status != AssignmentRequestStatusNames.Pending)
            return new(TicketOperationStatus.Conflict,
                Message: "This assignment request has already been reviewed.");
        if (!request.RequestedAgentUserAccountID.HasValue)
            return new(TicketOperationStatus.Conflict,
                Message: "This legacy request does not contain a requested agent.");

        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
                transaction = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, token);
            if (approve)
            {
                var assignment = await adminTicketService.AssignAsync(
                    administratorId, request.Ticket.TicketReferenceNumber,
                    request.RequestedAgentUserAccountID.Value, token);
                if (assignment.Status != TicketOperationStatus.Success)
                {
                    if (transaction is not null) await transaction.RollbackAsync(token);
                    return assignment;
                }
            }
            var now = DateTime.UtcNow;
            request.Status = approve ? AssignmentRequestStatusNames.Approved :
                AssignmentRequestStatusNames.Rejected;
            request.ReviewedByUserAccountID = administratorId;
            request.ReviewedDate = now;
            request.ReviewReason = approve ? null : rejectionReason;
            var agentName = request.RequestedAgentUserAccount!.FirstName + " " +
                request.RequestedAgentUserAccount.LastName;
            var action = approve ? TicketHistoryActionNames.AssignmentRequestApproved :
                TicketHistoryActionNames.AssignmentRequestRejected;
            var description = approve
                ? $"Administrator approved assignment to {agentName}."
                : $"Administrator rejected assignment to {agentName}. Reason: {rejectionReason}";
            AddAudit(request.Ticket, administratorId, action,
                AssignmentRequestStatusNames.Pending, request.Status, description, now);
            Notify(request.RequestedByUserAccountID, request.Ticket,
                approve ? NotificationTypeNames.AssignmentRequestApproved : NotificationTypeNames.AssignmentRequestRejected,
                approve ? "Assignment Request Approved" : "Assignment Request Rejected",
                approve
                    ? $"Your assignment request for {request.Ticket.TicketReferenceNumber} was approved."
                    : $"Your assignment request for {request.Ticket.TicketReferenceNumber} was rejected. Reason: {rejectionReason}", now);
            await dbContext.SaveChangesAsync(token);
            if (transaction is not null) await transaction.CommitAsync(token);
            return new(TicketOperationStatus.Success, true);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(token);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private Task<int> ActiveCountAsync(int agentId, CancellationToken token) =>
        dbContext.Tickets.CountAsync(item => !item.IsDeleted &&
            item.AssignedToUserAccountID == agentId &&
            TicketWorkloadRules.ActiveStatuses.Contains(item.TicketStatus.Name), token);

    private static IQueryable<TicketAssignmentRequestDto> Project(
        IQueryable<TicketAssignmentRequest> query) => query.Select(item =>
            new TicketAssignmentRequestDto(item.ID, item.TicketID,
                item.Ticket.TicketReferenceNumber, item.Ticket.Title,
                item.RequestedByUserAccountID,
                item.RequestedByUserAccount.FirstName + " " + item.RequestedByUserAccount.LastName,
                item.RequestedAgentUserAccountID,
                item.RequestedAgentUserAccount == null ? null :
                    item.RequestedAgentUserAccount.FirstName + " " + item.RequestedAgentUserAccount.LastName,
                item.RequestedAgentUserAccountID == null ? 0 :
                    item.RequestedAgentUserAccount!.AssignedTickets.Count(ticket => !ticket.IsDeleted &&
                        TicketWorkloadRules.ActiveStatuses.Contains(ticket.TicketStatus.Name)),
                TicketWorkloadRules.MaxActiveTicketsPerAgent,
                item.Status, item.RequestedDate, item.ReviewedByUserAccountID,
                item.ReviewedByUserAccount == null ? null :
                    item.ReviewedByUserAccount.FirstName + " " + item.ReviewedByUserAccount.LastName,
                item.ReviewedDate, item.ReviewReason));

    private static TicketAssignmentRequestDto ToDto(TicketAssignmentRequest request,
        Ticket ticket, string managerName, string agentName, int activeCount,
        string? reviewerName) => new(request.ID, ticket.ID,
            ticket.TicketReferenceNumber, ticket.Title,
            request.RequestedByUserAccountID, managerName,
            request.RequestedAgentUserAccountID, agentName, activeCount,
            TicketWorkloadRules.MaxActiveTicketsPerAgent, request.Status,
            request.RequestedDate, request.ReviewedByUserAccountID, reviewerName,
            request.ReviewedDate, request.ReviewReason);

    private void AddAudit(Ticket ticket, int actorId, string action,
        string? oldValue, string? newValue, string description, DateTime now)
    {
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = ticket.ID, PerformedByUserAccountID = actorId,
            ActionType = action, OldValue = oldValue, NewValue = newValue,
            Description = description, CreatedDate = now
        });
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = actorId, ActionType = action,
            EntityType = "Ticket", EntityID = ticket.TicketReferenceNumber,
            OldValue = oldValue, NewValue = newValue,
            Description = description, CreatedDate = now
        });
    }

    private void Notify(int userId, Ticket ticket, string type, string title,
        string message, DateTime now) => dbContext.UserNotifications.Add(
        new UserNotification
        {
            UserAccountID = userId, Type = type, Title = title, Message = message,
            TicketReferenceNumber = ticket.TicketReferenceNumber, CreatedDate = now
        });
}
