using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AgentTicketService(
    ApplicationDbContext dbContext,
    ITicketActivityService activityService)
    : IAgentTicketService
{
    private static readonly string[] ActiveStatuses =
        [TicketStatusNames.Assigned, TicketStatusNames.InProgress, TicketStatusNames.Pending];
    private static readonly string[] FinishedStatuses =
        [TicketStatusNames.Resolved, TicketStatusNames.Closed,
            TicketStatusNames.Cancelled, TicketStatusNames.Duplicate];

    public async Task<AgentDashboardDto> GetDashboardAsync(
        int agentId, CancellationToken token)
    {
        var tickets = OwnedTickets(agentId);
        var monthStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);
        var counts = await tickets.GroupBy(_ => 1).Select(group => new
        {
            Active = group.Count(ticket => ActiveStatuses.Contains(ticket.TicketStatus.Name)),
            InProgress = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.InProgress),
            Pending = group.Count(ticket => ticket.TicketStatus.Name == TicketStatusNames.Pending),
            High = group.Count(ticket => ticket.TicketPriority.Name == "High" &&
                !FinishedStatuses.Contains(ticket.TicketStatus.Name)),
            Critical = group.Count(ticket => ticket.TicketPriority.Name == "Critical" &&
                !FinishedStatuses.Contains(ticket.TicketStatus.Name)),
            Resolved = group.Count(ticket => ticket.ResolvedDate >= monthStart &&
                ticket.ResolvedDate < nextMonth)
        }).SingleOrDefaultAsync(token);

        var attention = await ProjectList(tickets
                .Where(ticket =>
                    (ticket.TicketPriority.Name == "High" ||
                     ticket.TicketPriority.Name == "Critical") &&
                    !FinishedStatuses.Contains(ticket.TicketStatus.Name))
                .OrderByDescending(ticket => ticket.TicketPriority.Name == "Critical")
                .ThenBy(ticket => ticket.CreatedDate))
            .Take(3).ToListAsync(token);

        var recent = await ProjectList(tickets
                .OrderByDescending(ticket => ticket.AssignedDate ?? ticket.CreatedDate)
                .ThenByDescending(ticket => ticket.CreatedDate))
            .Take(5).ToListAsync(token);

        return new(
            counts?.Active ?? 0, counts?.InProgress ?? 0, counts?.Pending ?? 0,
            counts?.High ?? 0, counts?.Critical ?? 0, counts?.Resolved ?? 0,
            attention, recent);
    }

    public async Task<PagedResultDto<AgentTicketListItemDto>> GetTicketsAsync(
        int agentId, AgentTicketFilterDto filter, CancellationToken token)
        => await GetTicketsAsync(agentId, filter, "active", token);

    public async Task<PagedResultDto<AgentTicketListItemDto>> GetOpenTicketsAsync(
        int agentId, AgentTicketFilterDto filter, CancellationToken token)
        => await GetTicketsAsync(agentId, filter, "open", token);

    public async Task<PagedResultDto<AgentTicketListItemDto>> GetHistoryTicketsAsync(
        int agentId, AgentTicketFilterDto filter, CancellationToken token)
        => await GetTicketsAsync(agentId, filter, "history", token);

    private async Task<PagedResultDto<AgentTicketListItemDto>> GetTicketsAsync(
        int agentId, AgentTicketFilterDto filter, string scope, CancellationToken token)
    {
        var query = scope switch
        {
            "open" => OpenTickets(),
            "history" => OwnedTickets(agentId).Where(ticket =>
                FinishedStatuses.Contains(ticket.TicketStatus.Name)),
            _ => OwnedTickets(agentId).Where(ticket =>
                ActiveStatuses.Contains(ticket.TicketStatus.Name))
        };
        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.ToLower();
            query = query.Where(ticket =>
                ticket.TicketReferenceNumber.ToLower().Contains(normalizedSearch) ||
                ticket.Title.ToLower().Contains(normalizedSearch) ||
                ticket.CreatedByUserAccount.FirstName.ToLower().Contains(normalizedSearch) ||
                ticket.CreatedByUserAccount.LastName.ToLower().Contains(normalizedSearch) ||
                (ticket.CreatedByUserAccount.FirstName + " " +
                 ticket.CreatedByUserAccount.LastName).ToLower().Contains(normalizedSearch));
        }
        if (filter.StatusId.HasValue)
            query = query.Where(ticket => ticket.TicketStatusID == filter.StatusId);
        if (filter.CategoryId.HasValue)
            query = query.Where(ticket => ticket.TicketCategoryID == filter.CategoryId);
        if (filter.PriorityId.HasValue)
            query = query.Where(ticket => ticket.TicketPriorityID == filter.PriorityId);
        if (filter.FromUtc.HasValue)
        {
            var start = filter.FromUtc.Value.UtcDateTime;
            query = query.Where(ticket => ticket.CreatedDate >= start);
        }
        else if (filter.FromDate.HasValue)
        {
            var start = DateTime.SpecifyKind(filter.FromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(ticket => ticket.CreatedDate >= start);
        }
        if (filter.ToUtcExclusive.HasValue)
        {
            var endExclusive = filter.ToUtcExclusive.Value.UtcDateTime;
            query = query.Where(ticket => ticket.CreatedDate < endExclusive);
        }
        else if (filter.ToDate.HasValue)
        {
            var endExclusive = DateTime.SpecifyKind(
                filter.ToDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(ticket => ticket.CreatedDate < endExclusive);
        }

        var totalItems = await query.CountAsync(token);
        query = ApplySorting(query, filter.SortBy, filter.SortDirection);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var items = await ProjectList(query)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
        return new(items, page, pageSize, totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize));
    }

    public async Task<AgentTicketDetailsDto?> GetTicketAsync(
        int agentId, string ticketReference, CancellationToken token)
    {
        var details = await ProjectDetails(ReadableTickets(agentId)
                .Where(ticket => ticket.TicketReferenceNumber == ticketReference), agentId)
            .SingleOrDefaultAsync(token);
        if (details is null) return null;
        var currentPending = await dbContext.TicketPendingRecords.AsNoTracking()
            .Where(item => item.TicketID == details.Id && item.ResumedDate == null)
            .Select(item => new CurrentTicketPendingDto(
                item.ID, item.ReasonCode, item.ReasonText, item.AdditionalNote,
                item.CreatedByUserAccountID,
                item.CreatedByUserAccount.FirstName + " " +
                    item.CreatedByUserAccount.LastName,
                item.CreatedDate))
            .SingleOrDefaultAsync(token);
        var ownsAssignment = await dbContext.Tickets.AsNoTracking().AnyAsync(ticket =>
            ticket.ID == details.Id && ticket.AssignedToUserAccountID == agentId, token);
        var transitions = ownsAssignment
            ? await GetAllowedTransitionsAsync(details.StatusName, token)
            : [];
        var requestStatus = !ownsAssignment
            ? await dbContext.TicketAssignmentRequests.AsNoTracking()
                .Where(item => item.TicketID == details.Id &&
                    item.RequestedByUserAccountID == agentId)
                .OrderByDescending(item => item.RequestedDate)
                .Select(item => item.Status)
                .FirstOrDefaultAsync(token)
            : null;
        return details with
        {
            CurrentPending = currentPending,
            AllowedStatusTransitions = transitions,
            CanChangeStatus = transitions.Count > 0,
            CanComment = ownsAssignment && details.StatusName is not
                (TicketStatusNames.Closed or TicketStatusNames.Cancelled or
                 TicketStatusNames.Duplicate),
            CanResolve = ownsAssignment && details.StatusName == TicketStatusNames.InProgress,
            CanClose = ownsAssignment && details.StatusName == TicketStatusNames.Resolved,
            CanRequestAssignment = !ownsAssignment &&
                details.StatusName == TicketStatusNames.Open &&
                requestStatus != AssignmentRequestStatusNames.Pending,
            AssignmentRequestStatus = requestStatus
        };
    }

    public async Task<TicketServiceResult<TicketAssignmentRequestDto>>
        RequestAssignmentAsync(
            int agentId, string ticketReference, CancellationToken token)
    {
        var ticket = await dbContext.Tickets
            .Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.TicketReferenceNumber == ticketReference &&
                !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: DuplicateTicketRules.ReadOnlyMessage);
        if (ticket.AssignedToUserAccountID.HasValue ||
            ticket.TicketStatus.Name != TicketStatusNames.Open)
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket is no longer open for assignment.");
        var pending = await dbContext.TicketAssignmentRequests.AnyAsync(item =>
            item.TicketID == ticket.ID &&
            item.RequestedByUserAccountID == agentId &&
            item.Status == AssignmentRequestStatusNames.Pending, token);
        if (pending)
            return new(TicketOperationStatus.Conflict,
                Message: "You already requested assignment to this ticket.");

        var now = DateTime.UtcNow;
        var agentName = await GetAgentNameAsync(agentId, token);
        var request = new TicketAssignmentRequest
        {
            TicketID = ticket.ID,
            RequestedByUserAccountID = agentId,
            Status = AssignmentRequestStatusNames.Pending,
            RequestedDate = now
        };
        dbContext.TicketAssignmentRequests.Add(request);
        AddHistory(ticket.ID, agentId, TicketHistoryActionNames.AssignmentRequested,
            null, AssignmentRequestStatusNames.Pending,
            $"Assignment requested by {agentName}.", false, now);
        AddActivity(ticket, agentId, TicketHistoryActionNames.AssignmentRequested,
            null, AssignmentRequestStatusNames.Pending,
            $"Assignment requested by {agentName}.", now);
        await dbContext.SaveChangesAsync(token);
        return new(TicketOperationStatus.Success,
            new(request.ID, ticket.ID, ticket.TicketReferenceNumber, ticket.Title,
                agentId, agentName, null, null, 0,
                TicketWorkloadRules.MaxActiveTicketsPerAgent,
                request.Status, now, null, null, null, null));
    }

    public async Task<TicketServiceResult<AgentTicketDetailsDto>> UpdateStatusAsync(
        int agentId, string ticketReference,
        UpdateAgentTicketStatusRequestDto request, CancellationToken token)
    {
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.TicketReferenceNumber == ticketReference &&
                item.AssignedToUserAccountID == agentId && !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: DuplicateTicketRules.ReadOnlyMessage);
        var target = await dbContext.TicketStatuses.SingleOrDefaultAsync(
            status => status.ID == request.StatusId && status.IsActive, token);
        if (target is null)
            return new(TicketOperationStatus.Invalid, Message: "Select a valid active status.");
        if (!IsTransitionAllowed(ticket.TicketStatus.Name, target.Name))
            return new(TicketOperationStatus.Conflict,
                Message: $"A ticket cannot move from {ticket.TicketStatus.Name} to {target.Name}.");
        if ((ticket.TicketStatus.Name == TicketStatusNames.InProgress &&
             target.Name == TicketStatusNames.Pending) ||
            (ticket.TicketStatus.Name == TicketStatusNames.Pending &&
             target.Name == TicketStatusNames.InProgress))
            return new(TicketOperationStatus.Invalid,
                Message: "Use the dedicated pause or resume work action for this transition.");

        var oldStatus = ticket.TicketStatus.Name;
        var now = DateTime.UtcNow;
        ticket.TicketStatusID = target.ID;
        ticket.UpdatedDate = now;
        ticket.AssignedDate ??= now;
        var actorName = await GetAgentNameAsync(agentId, token);
        var workStarted = oldStatus == TicketStatusNames.Assigned &&
            target.Name == TicketStatusNames.InProgress;
        var workResumed = oldStatus == TicketStatusNames.Pending &&
            target.Name == TicketStatusNames.InProgress;
        var workPaused = oldStatus == TicketStatusNames.InProgress &&
            target.Name == TicketStatusNames.Pending;
        int? recordedMinutes = null;
        if (workStarted || workResumed)
        {
            var hasOpenSession = await dbContext.TicketWorkSessions.AnyAsync(
                item => item.TicketID == ticket.ID && item.EndedAt == null, token);
            if (hasOpenSession)
                return new(TicketOperationStatus.Conflict,
                    Message: "Work is already active for this ticket.");
            dbContext.TicketWorkSessions.Add(new TicketWorkSession
            {
                TicketID = ticket.ID,
                ITAgentUserAccountID = agentId,
                StartedAt = now,
                CreatedDate = now
            });
        }
        else if (workPaused)
        {
            recordedMinutes = await CloseOpenWorkSessionAsync(
                ticket.ID, now, TicketStatusNames.Pending, token);
        }
        var workAction = workStarted ? TicketHistoryActionNames.TicketWorkStarted
            : workResumed ? TicketHistoryActionNames.WorkResumed
            : workPaused ? TicketHistoryActionNames.WorkPaused
            : TicketHistoryActionNames.StatusChanged;
        AddHistory(ticket.ID, agentId,
            workAction,
            oldStatus, target.Name,
            workStarted
                ? $"Ticket work started by {actorName}."
                : NullIfWhiteSpace(request.Reason),
            false, now);
        if (recordedMinutes.HasValue)
            dbContext.ChangeTracker.Entries<TicketHistory>().Last().Entity.WorkDurationMinutes = recordedMinutes;
        AddActivity(ticket, agentId,
            workAction,
            oldStatus, target.Name,
            workStarted
                ? $"Ticket work started by {actorName}."
                : $"Ticket status changed from {oldStatus} to {target.Name}.",
            now);

        var result = await SaveWorkflowAsync(token);
        if (result is not null) return result;
        return new(TicketOperationStatus.Success,
            await GetTicketAsync(agentId, ticketReference, token));
    }

    public async Task<TicketServiceResult<AgentTicketWorkflowResultDto>> MarkPendingAsync(
        int agentId, string ticketReference, MarkTicketPendingRequestDto request,
        CancellationToken token)
    {
        var code = request.ReasonCode?.Trim();
        var customReason = NullIfWhiteSpace(request.CustomReason);
        var note = NullIfWhiteSpace(request.AdditionalNote);
        if (!TicketPendingReasons.TryGetLabel(code, out var label))
            return new(TicketOperationStatus.Invalid,
                Message: "Select a valid pending reason.");
        if (code == TicketPendingReasons.Other)
        {
            if (customReason is null)
                return new(TicketOperationStatus.Invalid,
                    Message: "Enter a reason when Other is selected.");
            if (customReason.Length > 300)
                return new(TicketOperationStatus.Invalid,
                    Message: "The custom pending reason cannot exceed 300 characters.");
            label = customReason;
        }
        if (note?.Length > 1000)
            return new(TicketOperationStatus.Invalid,
                Message: "The additional note cannot exceed 1000 characters.");

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(token)
            : null;
        try
        {
            var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
                .SingleOrDefaultAsync(item =>
                    item.TicketReferenceNumber == ticketReference && !item.IsDeleted, token);
            if (ticket is null) return new(TicketOperationStatus.NotFound);
            if (ticket.AssignedToUserAccountID != agentId)
                return new(TicketOperationStatus.Forbidden,
                    Message: "Only the assigned IT Support Agent may pause work.");
            if (ticket.TicketStatus.Name == TicketStatusNames.Pending)
                return new(TicketOperationStatus.Conflict,
                    Message: "This ticket is already pending.");
            if (ticket.TicketStatus.Name != TicketStatusNames.InProgress)
                return new(TicketOperationStatus.Invalid,
                    Message: "Only an In Progress ticket can be marked as Pending.");

            var session = await dbContext.TicketWorkSessions.SingleOrDefaultAsync(
                item => item.TicketID == ticket.ID && item.EndedAt == null, token);
            if (session is null)
                return new(TicketOperationStatus.Conflict,
                    Message: "No active work session exists for this ticket.");
            if (await dbContext.TicketPendingRecords.AnyAsync(
                    item => item.TicketID == ticket.ID && item.ResumedDate == null, token))
                return new(TicketOperationStatus.Conflict,
                    Message: "An unresolved pending period already exists.");

            var pendingStatusId = await dbContext.TicketStatuses
                .Where(item => item.Name == TicketStatusNames.Pending && item.IsActive)
                .Select(item => item.ID).SingleAsync(token);
            var now = DateTime.UtcNow;
            var duration = Math.Max(0,
                (int)Math.Floor((now - session.StartedAt).TotalMinutes));
            session.EndedAt = now;
            session.DurationMinutes = duration;
            session.EndedReason = TicketStatusNames.Pending;
            var sessionNumber = await dbContext.TicketWorkSessions.CountAsync(
                item => item.TicketID == ticket.ID, token);
            dbContext.TicketPendingRecords.Add(new TicketPendingRecord
            {
                TicketID = ticket.ID,
                WorkSessionID = session.ID,
                ReasonCode = code!,
                ReasonText = label,
                AdditionalNote = note,
                CreatedByUserAccountID = agentId,
                CreatedDate = now
            });
            ticket.TicketStatusID = pendingStatusId;
            ticket.UpdatedDate = now;
            var description = $"Ticket moved to Pending. Reason: {label}. " +
                $"Session #{sessionNumber} paused after {duration} minute(s)." +
                (note is null ? string.Empty : $" Note: {note}");
            AddHistory(ticket.ID, agentId, TicketHistoryActionNames.WorkPaused,
                TicketStatusNames.InProgress, TicketStatusNames.Pending,
                description, false, now);
            dbContext.ChangeTracker.Entries<TicketHistory>().Last()
                .Entity.WorkDurationMinutes = duration;
            AddActivity(ticket, agentId, TicketHistoryActionNames.WorkPaused,
                TicketStatusNames.InProgress, TicketStatusNames.Pending,
                description, now);
            await dbContext.SaveChangesAsync(token);
            if (transaction is not null) await transaction.CommitAsync(token);
            var details = await GetTicketAsync(agentId, ticketReference, token);
            var summary = await activityService.GetSummaryAsync(
                agentId, ticketReference, token);
            return new(TicketOperationStatus.Success,
                new(details!, summary.Value!));
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(token);
            return new(TicketOperationStatus.Conflict,
                Message: "The ticket changed while work was being paused. Reload and try again.");
        }
        catch (DbUpdateException)
        {
            if (transaction is not null) await transaction.RollbackAsync(token);
            return new(TicketOperationStatus.Conflict,
                Message: "The pending action conflicts with another ticket update.");
        }
    }

    public async Task<TicketServiceResult<AgentTicketWorkflowResultDto>> ResumeWorkAsync(
        int agentId, string ticketReference, CancellationToken token)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(token)
            : null;
        try
        {
            var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
                .SingleOrDefaultAsync(item =>
                    item.TicketReferenceNumber == ticketReference && !item.IsDeleted, token);
            if (ticket is null) return new(TicketOperationStatus.NotFound);
            if (ticket.AssignedToUserAccountID != agentId)
                return new(TicketOperationStatus.Forbidden,
                    Message: "Only the assigned IT Support Agent may resume work.");
            if (ticket.TicketStatus.Name == TicketStatusNames.InProgress)
                return new(TicketOperationStatus.Conflict,
                    Message: "Work is already active for this ticket.");
            if (ticket.TicketStatus.Name != TicketStatusNames.Pending)
                return new(TicketOperationStatus.Invalid,
                    Message: "Only a Pending ticket can resume work.");
            if (await dbContext.TicketWorkSessions.AnyAsync(
                    item => item.TicketID == ticket.ID && item.EndedAt == null, token))
                return new(TicketOperationStatus.Conflict,
                    Message: "An active work session already exists.");
            var pending = await dbContext.TicketPendingRecords
                .Where(item => item.TicketID == ticket.ID && item.ResumedDate == null)
                .OrderByDescending(item => item.CreatedDate)
                .SingleOrDefaultAsync(token);
            if (pending is null)
                return new(TicketOperationStatus.Conflict,
                    Message: "No unresolved pending period exists for this ticket.");

            var inProgressStatusId = await dbContext.TicketStatuses
                .Where(item => item.Name == TicketStatusNames.InProgress && item.IsActive)
                .Select(item => item.ID).SingleAsync(token);
            var now = DateTime.UtcNow;
            pending.ResumedDate = now;
            pending.ResumedByUserAccountID = agentId;
            var sessionNumber = await dbContext.TicketWorkSessions.CountAsync(
                item => item.TicketID == ticket.ID, token) + 1;
            dbContext.TicketWorkSessions.Add(new TicketWorkSession
            {
                TicketID = ticket.ID,
                ITAgentUserAccountID = agentId,
                StartedAt = now,
                CreatedDate = now
            });
            ticket.TicketStatusID = inProgressStatusId;
            ticket.UpdatedDate = now;
            var actorName = await GetAgentNameAsync(agentId, token);
            var description = $"Work resumed by {actorName}. Session #{sessionNumber} started.";
            AddHistory(ticket.ID, agentId, TicketHistoryActionNames.WorkResumed,
                TicketStatusNames.Pending, TicketStatusNames.InProgress,
                description, false, now);
            AddActivity(ticket, agentId, TicketHistoryActionNames.WorkResumed,
                TicketStatusNames.Pending, TicketStatusNames.InProgress,
                description, now);
            await dbContext.SaveChangesAsync(token);
            if (transaction is not null) await transaction.CommitAsync(token);
            var details = await GetTicketAsync(agentId, ticketReference, token);
            var summary = await activityService.GetSummaryAsync(
                agentId, ticketReference, token);
            return new(TicketOperationStatus.Success,
                new(details!, summary.Value!));
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(token);
            return new(TicketOperationStatus.Conflict,
                Message: "The ticket changed while work was being resumed. Reload and try again.");
        }
        catch (DbUpdateException)
        {
            if (transaction is not null) await transaction.RollbackAsync(token);
            return new(TicketOperationStatus.Conflict,
                Message: "The resume action conflicts with another ticket update.");
        }
    }

    public async Task<TicketServiceResult<AgentTicketDetailsDto>> ResolveAsync(
        int agentId, string ticketReference,
        ResolveTicketRequestDto request, CancellationToken token)
    {
        var summary = request.ResolutionSummary?.Trim();
        if (summary is null || summary.Length is < 10 or > 5000)
            return new(TicketOperationStatus.Invalid,
                Message: "Resolution summary must be between 10 and 5000 characters.");
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.TicketReferenceNumber == ticketReference &&
                item.AssignedToUserAccountID == agentId && !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: DuplicateTicketRules.ReadOnlyMessage);
        if (ticket.TicketStatus.Name != TicketStatusNames.InProgress)
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket cannot be resolved from its current status.");
        var resolvedStatusId = await dbContext.TicketStatuses
            .Where(status => status.Name == TicketStatusNames.Resolved && status.IsActive)
            .Select(status => status.ID).SingleAsync(token);
        var now = DateTime.UtcNow;
        var oldStatus = ticket.TicketStatus.Name;
        ticket.TicketStatusID = resolvedStatusId;
        ticket.ResolutionSummary = summary;
        ticket.ResolvedByUserAccountID = agentId;
        ticket.ResolvedDate = now;
        ticket.UpdatedDate = now;
        var actorName = await GetAgentNameAsync(agentId, token);
        var recordedMinutes = await CloseOpenWorkSessionAsync(
            ticket.ID, now, TicketStatusNames.Resolved, token);
        AddHistory(ticket.ID, agentId, TicketHistoryActionNames.TicketResolved,
            oldStatus, TicketStatusNames.Resolved, $"Ticket resolved by {actorName}.",
            false, now);
        dbContext.ChangeTracker.Entries<TicketHistory>().Last().Entity.WorkDurationMinutes = recordedMinutes;
        AddActivity(ticket, agentId, TicketHistoryActionNames.TicketResolved,
            oldStatus, TicketStatusNames.Resolved,
            $"Ticket resolved by {actorName}.", now);
        var result = await SaveWorkflowAsync(token);
        if (result is not null) return result;
        return new(TicketOperationStatus.Success,
            await GetTicketAsync(agentId, ticketReference, token));
    }

    public async Task<TicketServiceResult<AgentTicketDetailsDto>> CloseAsync(
        int agentId, string ticketReference,
        CloseTicketRequestDto request, CancellationToken token)
    {
        var closingNote = NullIfWhiteSpace(request.ClosingNote);
        if (closingNote?.Length > 500)
            return new(TicketOperationStatus.Invalid,
                Message: "The closing note cannot exceed 500 characters.");
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
                item.TicketReferenceNumber == ticketReference &&
                item.AssignedToUserAccountID == agentId &&
                !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: DuplicateTicketRules.ReadOnlyMessage);
        if (ticket.TicketStatus.Name == TicketStatusNames.Closed)
            return new(TicketOperationStatus.Conflict,
                Message: "This ticket is already closed.");
        if (ticket.TicketStatus.Name != TicketStatusNames.Resolved)
            return new(TicketOperationStatus.Conflict,
                Message: "The ticket must be resolved before it can be closed.");

        var closedStatusId = await dbContext.TicketStatuses
            .Where(status =>
                status.Name == TicketStatusNames.Closed &&
                status.IsActive)
            .Select(status => status.ID)
            .SingleAsync(token);
        var now = DateTime.UtcNow;
        var actorName = await GetAgentNameAsync(agentId, token);
        ticket.TicketStatusID = closedStatusId;
        ticket.ClosedDate = now;
        ticket.UpdatedDate = now;
        var description = $"Ticket closed by {actorName}." +
            (closingNote is null ? string.Empty : $" Note: {closingNote}");
        AddHistory(ticket.ID, agentId, TicketHistoryActionNames.TicketClosed,
            TicketStatusNames.Resolved, TicketStatusNames.Closed,
            description, false, now);
        AddActivity(ticket, agentId, TicketHistoryActionNames.TicketClosed,
            TicketStatusNames.Resolved, TicketStatusNames.Closed,
            description, now);

        var result = await SaveWorkflowAsync(token);
        if (result is not null) return result;
        return new(TicketOperationStatus.Success,
            await GetTicketAsync(agentId, ticketReference, token));
    }

    public async Task<IReadOnlyCollection<TicketCommentDto>?> GetCommentsAsync(
        int agentId, string ticketReference, CancellationToken token)
    {
        var ticketId = await OwnedTickets(agentId)
            .Where(ticket => ticket.TicketReferenceNumber == ticketReference)
            .Select(ticket => (int?)ticket.ID).SingleOrDefaultAsync(token);
        return ticketId is null ? null : await ProjectComments(ticketId.Value)
            .ToListAsync(token);
    }

    public async Task<TicketServiceResult<TicketCommentDto>> AddCommentAsync(
        int agentId, string ticketReference, AddTicketCommentRequestDto request,
        CancellationToken token)
    {
        var content = request.Message?.Trim();
        if (!TicketCommentRules.TryParseVisibility(
                request.Visibility, out var visibility))
            return new(TicketOperationStatus.Invalid,
                Message: "Visibility must be Public or Private.");
        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > TicketCommentRules.MaximumMessageLength)
            return new(TicketOperationStatus.Invalid,
                Message: "Comment content is required and cannot exceed 5000 characters.");
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
            item.TicketReferenceNumber == ticketReference &&
            item.AssignedToUserAccountID == agentId && !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (TicketCommentRules.IsReadOnly(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name)
                    ? DuplicateTicketRules.ReadOnlyMessage
                    : "Comments cannot be added to a closed or cancelled ticket.");
        var now = DateTime.UtcNow;
        var comment = new TicketComment
        {
            TicketID = ticket.ID, AuthorUserAccountID = agentId,
            Content = content, Visibility = visibility, CreatedDate = now
        };
        dbContext.TicketComments.Add(comment);
        ticket.UpdatedDate = now;
        AddHistory(ticket.ID, agentId,
            TicketHistoryActionNames.CommentAdded,
            null, visibility.ToString(),
            TicketCommentRules.HistoryDescription(visibility),
            visibility == CommentVisibility.Private, now);
        await dbContext.SaveChangesAsync(token);
        var authorName = await dbContext.Users.Where(user => user.Id == agentId)
            .Select(user => user.FirstName + " " + user.LastName).SingleAsync(token);
        return new(TicketOperationStatus.Success,
            new(comment.ID, authorName,
                await GetUserRoleAsync(agentId, token),
                comment.Content, comment.CreatedDate,
                null, false, visibility.ToString()));
    }

    public async Task<IReadOnlyCollection<TicketHistoryDto>?> GetHistoryAsync(
        int agentId, string ticketReference, CancellationToken token)
    {
        var ticketId = await OwnedTickets(agentId)
            .Where(ticket => ticket.TicketReferenceNumber == ticketReference)
            .Select(ticket => (int?)ticket.ID).SingleOrDefaultAsync(token);
        return ticketId is null ? null : await ProjectHistory(ticketId.Value)
            .ToListAsync(token);
    }

    public async Task<IReadOnlyCollection<TicketCommentDto>?> GetEmployeeCommentsAsync(
        int employeeId, int ticketId, CancellationToken token)
    {
        var ownsTicket = await dbContext.Tickets.AnyAsync(ticket =>
            ticket.ID == ticketId && ticket.CreatedByUserAccountID == employeeId &&
            !ticket.IsDeleted, token);
        return !ownsTicket ? null : await ProjectComments(ticketId).ToListAsync(token);
    }

    public async Task<TicketServiceResult<TicketCommentDto>> AddEmployeeCommentAsync(
        int employeeId, int ticketId, AddTicketCommentRequestDto request,
        CancellationToken token)
    {
        var content = request.Message?.Trim();
        if (!TicketCommentRules.TryParseVisibility(
                request.Visibility, out var visibility))
            return new(TicketOperationStatus.Invalid,
                Message: "Visibility must be Public or Private.");
        if (string.IsNullOrWhiteSpace(content) ||
            content.Length > TicketCommentRules.MaximumMessageLength)
            return new(TicketOperationStatus.Invalid,
                Message: "Comment message is required and cannot exceed 5000 characters.");
        var ticket = await dbContext.Tickets.Include(item => item.TicketStatus)
            .SingleOrDefaultAsync(item =>
            item.ID == ticketId && item.CreatedByUserAccountID == employeeId &&
            !item.IsDeleted, token);
        if (ticket is null) return new(TicketOperationStatus.NotFound);
        if (TicketCommentRules.IsReadOnly(ticket.TicketStatus.Name))
            return new(TicketOperationStatus.Conflict,
                Message: DuplicateTicketRules.IsDuplicate(ticket.TicketStatus.Name)
                    ? DuplicateTicketRules.ReadOnlyMessage
                    : "Comments cannot be added to a closed or cancelled ticket.");
        var now = DateTime.UtcNow;
        var comment = new TicketComment
        {
            TicketID = ticket.ID, AuthorUserAccountID = employeeId,
            Content = content, Visibility = visibility, CreatedDate = now
        };
        dbContext.TicketComments.Add(comment);
        AddHistory(ticket.ID, employeeId, TicketHistoryActionNames.CommentAdded,
            null, visibility.ToString(),
            TicketCommentRules.HistoryDescription(visibility),
            visibility == CommentVisibility.Private, now);
        await dbContext.SaveChangesAsync(token);
        var authorName = await dbContext.Users.Where(user => user.Id == employeeId)
            .Select(user => user.FirstName + " " + user.LastName).SingleAsync(token);
        return new(TicketOperationStatus.Success,
            new(comment.ID, authorName,
                await GetUserRoleAsync(employeeId, token),
                content, now, null, false,
                visibility.ToString()));
    }

    private IQueryable<Ticket> OwnedTickets(int agentId) =>
        dbContext.Tickets.AsNoTracking().Where(ticket =>
            ticket.AssignedToUserAccountID == agentId && !ticket.IsDeleted);

    private IQueryable<Ticket> OpenTickets() =>
        dbContext.Tickets.AsNoTracking().Where(ticket =>
            ticket.AssignedToUserAccountID == null &&
            ticket.TicketStatus.Name == TicketStatusNames.Open &&
            !ticket.IsDeleted);

    private IQueryable<Ticket> ReadableTickets(int agentId) =>
        dbContext.Tickets.AsNoTracking().Where(ticket =>
            !ticket.IsDeleted &&
            (ticket.AssignedToUserAccountID == agentId ||
             (ticket.AssignedToUserAccountID == null &&
              ticket.TicketStatus.Name == TicketStatusNames.Open)));

    private async Task<IReadOnlyCollection<AllowedStatusTransitionDto>>
        GetAllowedTransitionsAsync(string currentStatus, CancellationToken token)
    {
        var names = currentStatus switch
        {
            TicketStatusNames.Assigned => new[] { TicketStatusNames.InProgress },
            TicketStatusNames.InProgress => new[] { TicketStatusNames.Pending },
            TicketStatusNames.Pending => new[] { TicketStatusNames.InProgress },
            _ => []
        };
        return await dbContext.TicketStatuses.AsNoTracking()
            .Where(status => status.IsActive && names.Contains(status.Name))
            .OrderBy(status => status.SortOrder)
            .Select(status => new AllowedStatusTransitionDto(status.ID, status.Name))
            .ToListAsync(token);
    }

    private static bool IsTransitionAllowed(string from, string to) =>
        (from, to) switch
        {
            (TicketStatusNames.Assigned, TicketStatusNames.InProgress) => true,
            (TicketStatusNames.InProgress, TicketStatusNames.Pending) => true,
            (TicketStatusNames.Pending, TicketStatusNames.InProgress) => true,
            _ => false
        };

    private async Task<TicketServiceResult<AgentTicketDetailsDto>?> SaveWorkflowAsync(
        CancellationToken token)
    {
        try
        {
            await dbContext.SaveChangesAsync(token);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(TicketOperationStatus.Conflict,
                Message: "The ticket changed while this request was being processed. Reload and try again.");
        }
    }

    private void AddHistory(int ticketId, int actorId, string action,
        string? oldValue, string? newValue, string? description,
        bool isInternal, DateTime createdDate) =>
        dbContext.TicketHistory.Add(new TicketHistory
        {
            TicketID = ticketId, PerformedByUserAccountID = actorId,
            ActionType = action, OldValue = oldValue, NewValue = newValue,
            Description = description, IsInternal = isInternal, CreatedDate = createdDate
        });

    private void AddActivity(
        Ticket ticket,
        int actorId,
        string action,
        string? oldValue,
        string? newValue,
        string description,
        DateTime createdDate) =>
        dbContext.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = actorId,
            ActionType = action,
            EntityType = "Ticket",
            EntityID = ticket.TicketReferenceNumber,
            Description = description,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = createdDate
        });

    private Task<string> GetAgentNameAsync(int agentId, CancellationToken token) =>
        dbContext.Users.Where(user => user.Id == agentId)
            .Select(user => user.FirstName + " " + user.LastName)
            .SingleAsync(token);

    private async Task<string> GetUserRoleAsync(
        int userId, CancellationToken token) =>
        await dbContext.UserRoles.Where(assignment => assignment.UserId == userId)
            .Select(assignment => assignment.Role.Name!)
            .FirstOrDefaultAsync(token) ?? string.Empty;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IQueryable<Ticket> ApplySorting(
        IQueryable<Ticket> query, string? sortBy, string? direction)
    {
        var desc = !string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.ToLowerInvariant() switch
        {
            "createddate" => desc ? query.OrderByDescending(x => x.CreatedDate) : query.OrderBy(x => x.CreatedDate),
            "title" => desc ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            "status" => desc ? query.OrderByDescending(x => x.TicketStatus.SortOrder) : query.OrderBy(x => x.TicketStatus.SortOrder),
            "priority" => desc ? query.OrderByDescending(x => x.TicketPriority.SortOrder) : query.OrderBy(x => x.TicketPriority.SortOrder),
            _ => desc
                ? query.OrderByDescending(x => x.AssignedDate ?? x.CreatedDate).ThenByDescending(x => x.CreatedDate)
                : query.OrderBy(x => x.AssignedDate ?? x.CreatedDate).ThenBy(x => x.CreatedDate)
        };
    }

    private async Task<int?> CloseOpenWorkSessionAsync(
        int ticketId, DateTime endedAt, string reason, CancellationToken token)
    {
        var session = await dbContext.TicketWorkSessions.SingleOrDefaultAsync(
            item => item.TicketID == ticketId && item.EndedAt == null, token);
        if (session is null) return null;
        var elapsed = endedAt - session.StartedAt;
        var minutes = Math.Max(0, (int)Math.Floor(elapsed.TotalMinutes));
        session.EndedAt = endedAt;
        session.DurationMinutes = minutes;
        session.EndedReason = reason;
        return minutes;
    }

    private static IQueryable<AgentTicketListItemDto> ProjectList(IQueryable<Ticket> query) =>
        query.Select(ticket => new AgentTicketListItemDto(
            ticket.ID, ticket.TicketReferenceNumber, ticket.Title,
            ticket.CreatedByUserAccount.FirstName + " " + ticket.CreatedByUserAccount.LastName,
            ticket.CreatedByUserAccount.Department == null ? null :
                ticket.CreatedByUserAccount.Department.Name,
            ticket.TicketCategoryID, ticket.TicketCategory.Name,
            ticket.TicketPriorityID, ticket.TicketPriority.Name,
            ticket.TicketStatusID, ticket.TicketStatus.Name,
            ticket.AssignedToUserAccount == null ? null :
                ticket.AssignedToUserAccount.FirstName + " " +
                ticket.AssignedToUserAccount.LastName,
            ticket.CreatedDate, ticket.UpdatedDate, ticket.AssignedDate, ticket.ResolvedDate));

    private static IQueryable<AgentTicketDetailsDto> ProjectDetails(
        IQueryable<Ticket> query, int agentId) =>
        query.Select(ticket => new AgentTicketDetailsDto(
            ticket.ID, ticket.TicketReferenceNumber, ticket.Title, ticket.Description,
            ticket.CreatedByUserAccount.FirstName + " " + ticket.CreatedByUserAccount.LastName,
            ticket.CreatedByUserAccount.Email!,
            ticket.CreatedByUserAccount.Department == null ? null :
                ticket.CreatedByUserAccount.Department.Name,
            ticket.TicketCategoryID, ticket.TicketCategory.Name,
            ticket.TicketPriorityID, ticket.TicketPriority.Name,
            ticket.TicketStatusID, ticket.TicketStatus.Name,
            ticket.CreatedDate, ticket.UpdatedDate, ticket.AssignedDate,
            ticket.ResolvedDate, ticket.ClosedDate,
            ticket.AssignedToUserAccount == null ? null :
                ticket.AssignedToUserAccount.FirstName + " " +
                ticket.AssignedToUserAccount.LastName,
            ticket.Attachments.Where(item => !item.IsDeleted)
                .OrderByDescending(item => item.UploadedDate)
                .Select(item => new TicketAttachmentDto(
                    item.ID, item.FileName, item.ContentType, item.FileSizeBytes,
                    item.UploadedDate, false)).ToList(),
            ticket.Comments.Where(item => !item.IsDeleted &&
                    (item.Visibility == CommentVisibility.Public ||
                     ticket.AssignedToUserAccountID == agentId ||
                     ticket.CreatedByUserAccountID == agentId))
                .OrderBy(item => item.CreatedDate)
                .Select(item => new TicketCommentDto(
                    item.ID, item.AuthorUserAccount.FirstName + " " +
                        item.AuthorUserAccount.LastName,
                    item.AuthorUserAccount.UserAccountRoles
                        .Select(assignment => assignment.Role.Name!)
                        .FirstOrDefault() ?? string.Empty,
                    item.Content, item.CreatedDate, item.UpdatedDate, item.IsEdited,
                    item.Visibility.ToString())).ToList(),
            ticket.History
                .OrderByDescending(item => item.CreatedDate)
                .Select(item => new TicketHistoryDto(
                    item.ID, item.ActionType,
                    item.PerformedByUserAccount.FirstName + " " +
                        item.PerformedByUserAccount.LastName,
                    item.OldValue, item.NewValue, item.Description, item.CreatedDate)).ToList(),
            ticket.ResolutionSummary, Array.Empty<AllowedStatusTransitionDto>(),
            false, false, false, false, true, false, false, false,
            false, null,
            ticket.OriginalTicket == null ||
                ticket.OriginalTicket.AssignedToUserAccountID != agentId ? null :
                ticket.OriginalTicket.TicketReferenceNumber,
            ticket.OriginalTicket == null ||
                ticket.OriginalTicket.AssignedToUserAccountID != agentId ? null :
                ticket.OriginalTicket.Title));

    private IQueryable<TicketCommentDto> ProjectComments(int ticketId) =>
        dbContext.TicketComments.AsNoTracking()
            .Where(item => item.TicketID == ticketId && !item.IsDeleted)
            .OrderBy(item => item.CreatedDate)
            .Select(item => new TicketCommentDto(
                item.ID, item.AuthorUserAccount.FirstName + " " +
                    item.AuthorUserAccount.LastName,
                item.AuthorUserAccount.UserAccountRoles
                    .Select(assignment => assignment.Role.Name!)
                    .FirstOrDefault() ?? string.Empty,
                item.Content, item.CreatedDate, item.UpdatedDate, item.IsEdited,
                item.Visibility.ToString()));

    private IQueryable<TicketHistoryDto> ProjectHistory(int ticketId) =>
        dbContext.TicketHistory.AsNoTracking()
            .Where(item => item.TicketID == ticketId)
            .OrderByDescending(item => item.CreatedDate)
            .Select(item => new TicketHistoryDto(
                item.ID, item.ActionType,
                item.PerformedByUserAccount.FirstName + " " +
                    item.PerformedByUserAccount.LastName,
                item.OldValue, item.NewValue, item.Description, item.CreatedDate));
}
