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

public sealed class TicketCancellationRequestService(ApplicationDbContext dbContext)
    : ITicketCancellationRequestService
{
    private static readonly string[] EligibleStatuses =
        [TicketStatusNames.Assigned, TicketStatusNames.InProgress, TicketStatusNames.Pending];

    public async Task<TicketServiceResult<TicketCancellationRequestDto>> CreateAsync(
        int agentId, string ticketReference, string reason, CancellationToken token)
    {
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
            return new(TicketOperationStatus.Invalid,
                Message: "A cancellation reason is required.");

        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .Include(item => item.AssignedToUserAccount)
            .SingleOrDefaultAsync(item => item.TicketReferenceNumber == ticketReference &&
                !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (ticket.AssignedToUserAccountID != agentId)
            return new(TicketOperationStatus.Forbidden,
                Message: "You can only request cancellation for a ticket assigned to you.");
        if (!EligibleStatuses.Contains(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: "Cancellation cannot be requested for this ticket in its current status.");
        if (await dbContext.TicketCancellationRequests.AnyAsync(item =>
                item.TicketID == ticket.ID &&
                item.Status == CancellationRequestStatusNames.Pending, token))
            return new(TicketOperationStatus.Conflict,
                Message: "A cancellation request is already pending for this ticket.");

        var now = DateTime.UtcNow;
        var agentName = ticket.AssignedToUserAccount!.FirstName + " " +
            ticket.AssignedToUserAccount.LastName;
        var request = new TicketCancellationRequest
        {
            TicketID = ticket.ID,
            RequestedByAgentUserAccountID = agentId,
            Reason = normalizedReason,
            RequestedDate = now
        };
        dbContext.TicketCancellationRequests.Add(request);
        AddTracking(ticket, agentId, TicketHistoryActionNames.CancellationRequested,
            null, CancellationRequestStatusNames.Pending,
            $"{agentName} requested cancellation. Reason: {normalizedReason}", now);
        var managerIds = await dbContext.UserRoles.AsNoTracking()
            .Where(item => item.Role.Name == RoleNames.Manager && item.UserAccount.IsActive)
            .Select(item => item.UserId).Distinct().ToListAsync(token);
        foreach (var managerId in managerIds)
            Notify(managerId, ticket, "CancellationRequest", "Cancellation Request Pending",
                $"{agentName} requested cancellation of ticket {ticket.TicketReferenceNumber}.", now);
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success, ToDto(request, ticket, agentName, null));
    }

    public async Task<IReadOnlyCollection<TicketCancellationRequestDto>>
        GetManagerRequestsAsync(CancellationToken token) =>
        await Project(dbContext.TicketCancellationRequests.AsNoTracking()
                .OrderByDescending(item => item.RequestedDate).Take(100))
            .ToListAsync(token);

    public async Task<TicketServiceResult<bool>> ReviewAsync(int managerId, int requestId,
        string decision, string? reviewNote, CancellationToken token)
    {
        var normalizedDecision = decision?.Trim().ToLowerInvariant();
        if (normalizedDecision is not ("reject" or "cancel" or "reassign"))
            return new(TicketOperationStatus.Invalid, Message: "Select a valid review decision.");

        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
                transaction = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, token);
            var request = await dbContext.TicketCancellationRequests
                .Include(item => item.Ticket).ThenInclude(ticket => ticket.TicketStatus)
                .Include(item => item.RequestedByAgentUserAccount)
                .SingleOrDefaultAsync(item => item.ID == requestId, token);
            if (request is null) return new(TicketOperationStatus.NotFound);
            if (request.Status != CancellationRequestStatusNames.Pending)
                return new(TicketOperationStatus.Conflict,
                    Message: "This cancellation request has already been reviewed.");
            var ticket = request.Ticket;
            if (ticket.AssignedToUserAccountID != request.RequestedByAgentUserAccountID ||
                !EligibleStatuses.Contains(ticket.TicketStatus.Name))
                return new(TicketOperationStatus.Conflict,
                    Message: "The ticket changed while this request was pending and can no longer be reviewed.");

            var now = DateTime.UtcNow;
            request.ReviewedByManagerUserAccountID = managerId;
            request.ReviewedDate = now;
            request.ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
            if (normalizedDecision == "reject")
            {
                request.Status = CancellationRequestStatusNames.Rejected;
                AddTracking(ticket, managerId,
                    TicketHistoryActionNames.CancellationRequestRejected,
                    CancellationRequestStatusNames.Pending, request.Status,
                    request.ReviewNote is null ? "Manager rejected the cancellation request." :
                        $"Manager rejected the cancellation request. Note: {request.ReviewNote}", now);
                Notify(request.RequestedByAgentUserAccountID, ticket, "CancellationRequest",
                    "Cancellation Request Rejected",
                    $"Your cancellation request for {ticket.TicketReferenceNumber} was rejected.", now);
            }
            else
            {
                request.Status = CancellationRequestStatusNames.Approved;
                request.Outcome = normalizedDecision == "cancel"
                    ? CancellationRequestOutcomeNames.Cancelled
                    : CancellationRequestOutcomeNames.Reassign;
                AddTracking(ticket, managerId,
                    TicketHistoryActionNames.CancellationRequestApproved,
                    CancellationRequestStatusNames.Pending, request.Status,
                    $"Manager approved the cancellation request with outcome: {request.Outcome}.", now);
                await EndActiveStateAsync(ticket, managerId, request.Outcome, now, token);
                var oldStatus = ticket.TicketStatus.Name;
                ticket.AssignedToUserAccountID = null;
                ticket.AssignedDate = null;
                ticket.UpdatedDate = now;
                AddTracking(ticket, managerId, TicketHistoryActionNames.AgentReleased,
                    request.RequestedByAgentUserAccount.FirstName + " " +
                        request.RequestedByAgentUserAccount.LastName, null,
                    "IT Agent was released from the ticket after Manager approval.", now);
                if (normalizedDecision == "cancel")
                {
                    ticket.TicketStatusID = await StatusIdAsync(TicketStatusNames.Cancelled, token);
                    ticket.CancelledDate = now;
                    ticket.CancelledReason = request.Reason;
                    AddTracking(ticket, managerId, TicketHistoryActionNames.TicketCancelled,
                        oldStatus, TicketStatusNames.Cancelled,
                        "Ticket cancelled after approval of the assigned Agent's request.", now);
                    Notify(request.RequestedByAgentUserAccountID, ticket, "CancellationRequest",
                        "Cancellation Request Approved",
                        $"Your cancellation request for {ticket.TicketReferenceNumber} was approved and the ticket was cancelled.", now);
                }
                else
                {
                    ticket.TicketStatusID = await StatusIdAsync(TicketStatusNames.Open, token);
                    AddTracking(ticket, managerId, TicketHistoryActionNames.ReassignmentInitiated,
                        oldStatus, TicketStatusNames.Open,
                        "Ticket returned to the unassigned queue for a Manager assignment request and Administrator approval.", now);
                    Notify(request.RequestedByAgentUserAccountID, ticket, "CancellationRequest",
                        "Release Request Approved",
                        $"Your request for {ticket.TicketReferenceNumber} was approved. You have been released and the ticket will continue through reassignment.", now);
                }
            }
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

    private async Task EndActiveStateAsync(Ticket ticket, int managerId, string outcome,
        DateTime now, CancellationToken token)
    {
        var session = await dbContext.TicketWorkSessions.SingleOrDefaultAsync(item =>
            item.TicketID == ticket.ID && !item.EndedAt.HasValue, token);
        if (session is not null)
        {
            session.EndedAt = now;
            session.DurationMinutes = Math.Max(0,
                (int)Math.Floor((now - session.StartedAt).TotalMinutes));
            session.EndedReason = outcome == CancellationRequestOutcomeNames.Cancelled
                ? "Cancelled by Manager" : "Released for Reassignment";
        }
        var pending = await dbContext.TicketPendingRecords.SingleOrDefaultAsync(item =>
            item.TicketID == ticket.ID && !item.ResumedDate.HasValue, token);
        if (pending is not null)
        {
            pending.ResumedDate = now;
            pending.ResumedByUserAccountID = managerId;
        }
    }

    private Task<int> StatusIdAsync(string name, CancellationToken token) =>
        dbContext.TicketStatuses.Where(item => item.Name == name)
            .Select(item => item.ID).SingleAsync(token);

    private static IQueryable<TicketCancellationRequestDto> Project(
        IQueryable<TicketCancellationRequest> query) => query.Select(item =>
            new TicketCancellationRequestDto(item.ID, item.TicketID,
                item.Ticket.TicketReferenceNumber, item.Ticket.Title,
                item.RequestedByAgentUserAccountID,
                item.RequestedByAgentUserAccount.FirstName + " " + item.RequestedByAgentUserAccount.LastName,
                item.Ticket.TicketStatus.Name, item.Reason, item.Status, item.RequestedDate,
                item.ReviewedByManagerUserAccountID,
                item.ReviewedByManagerUserAccount == null ? null :
                    item.ReviewedByManagerUserAccount.FirstName + " " + item.ReviewedByManagerUserAccount.LastName,
                item.ReviewedDate, item.ReviewNote, item.Outcome));

    private static TicketCancellationRequestDto ToDto(TicketCancellationRequest item,
        Ticket ticket, string agentName, string? managerName) => new(item.ID, ticket.ID,
        ticket.TicketReferenceNumber, ticket.Title, item.RequestedByAgentUserAccountID,
        agentName, ticket.TicketStatus.Name, item.Reason, item.Status, item.RequestedDate,
        item.ReviewedByManagerUserAccountID, managerName, item.ReviewedDate,
        item.ReviewNote, item.Outcome);

    private void AddTracking(Ticket ticket, int actorId, string action, string? oldValue,
        string? newValue, string description, DateTime now)
    {
        dbContext.TicketHistory.Add(new TicketHistory { TicketID = ticket.ID,
            PerformedByUserAccountID = actorId, ActionType = action, OldValue = oldValue,
            NewValue = newValue, Description = description, CreatedDate = now });
        dbContext.ActivityLogs.Add(new ActivityLog { PerformedByUserAccountID = actorId,
            ActionType = action, EntityType = "Ticket", EntityID = ticket.TicketReferenceNumber,
            OldValue = oldValue, NewValue = newValue, Description = description,
            CreatedDate = now });
    }

    private void Notify(int userId, Ticket ticket, string type, string title,
        string message, DateTime now) => dbContext.UserNotifications.Add(new UserNotification
        { UserAccountID = userId, Type = type, Title = title, Message = message,
            TicketReferenceNumber = ticket.TicketReferenceNumber, CreatedDate = now });
}
