using System.Data;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Settings;

namespace ResolveHub.Api.Data.Seed;

public static class PresentationDemoDataSeeder
{
    private const string DraftTitle = "New employee unable to access HR portal";
    private const string DraftDescription =
        "A recently onboarded employee receives an access denied message when attempting to open the HR portal.";

    private static readonly DemoTicketDefinition[] Definitions =
    [
        new("wifi", Utc(2026, 8, 18, 20, 45), "Laptop cannot connect to office Wi-Fi",
            "The laptop detects the corporate wireless network but cannot complete authentication, even after restarting and forgetting the network.",
            "Network", "High", TicketStatusNames.Open, "daniel.brooks@resolvehub.test", null),
        new("finance-drive", Utc(2026, 8, 18, 19, 28), "Unable to access Finance shared drive",
            "Access to the Finance shared drive returns an access denied message although the requester previously had permission.",
            "Access Request", "Medium", TicketStatusNames.Assigned, "olivia.bennett@resolvehub.test", "emily.carter@resolvehub.test"),
        new("payroll", Utc(2026, 8, 18, 14, 10), "Payroll application crashes on launch",
            "The payroll desktop application closes immediately after the splash screen and prevents payroll processing.",
            "Software", "Critical", TicketStatusNames.InProgress, "sophia.mitchell@resolvehub.test", "michael.thompson@resolvehub.test"),
        new("outlook", Utc(2026, 8, 17, 16, 35), "Outlook not receiving new emails",
            "Outlook remains connected but has not downloaded new messages since the latest workstation update.",
            "Email", "High", TicketStatusNames.Pending, "daniel.brooks@resolvehub.test", "natalie.hayes@resolvehub.test"),
        new("battery", Utc(2026, 8, 16, 11, 20), "Laptop battery not charging",
            "The laptop works while connected to power, but the battery percentage no longer increases above twelve percent.",
            "Hardware", "Medium", TicketStatusNames.Resolved, "ava.collins@resolvehub.test", "emily.carter@resolvehub.test"),
        new("signin", Utc(2026, 8, 15, 9, 5), "Suspicious sign-in alert received",
            "A security notification reported a sign-in from an unfamiliar location and the employee needs the account reviewed.",
            "Security", "Critical", TicketStatusNames.Assigned, "daniel.brooks@resolvehub.test", "michael.thompson@resolvehub.test"),
        new("display", Utc(2026, 8, 14, 15, 50), "Meeting room display not detected",
            "The conference room computer no longer detects the wall display through the installed presentation controller.",
            "Other", "Low", TicketStatusNames.Open, "jessica.morgan@resolvehub.test", null),
        new("acrobat", Utc(2026, 8, 13, 13, 15), "Request Adobe Acrobat license",
            "A licensed PDF editing tool is required to prepare and approve customer contract documents.",
            "Access Request", "Low", TicketStatusNames.Closed, "daniel.brooks@resolvehub.test", "natalie.hayes@resolvehub.test"),
        new("printer", Utc(2026, 8, 12, 10, 40), "Printer produces blank pages",
            "The department printer accepts jobs but outputs blank pages; this matches an already reported hardware issue.",
            "Hardware", "Medium", TicketStatusNames.Duplicate, "hannah.foster@resolvehub.test", null),
        new("vpn", Utc(2026, 8, 10, 17, 30), "VPN disconnects every few minutes",
            "The remote-access VPN disconnects repeatedly on a stable home connection and interrupts access to internal systems.",
            "Network", "High", TicketStatusNames.Cancelled, "daniel.brooks@resolvehub.test", null),
        new("teams-mic", Utc(2026, 8, 7, 12, 25), "Teams microphone not detected",
            "Microsoft Teams cannot detect the built-in microphone although the device appears normally in system settings.",
            "Software", "Medium", TicketStatusNames.Resolved, "ryan.cooper@resolvehub.test", "natalie.hayes@resolvehub.test"),
        new("signature", Utc(2026, 8, 3, 9, 10), "Email signature disappeared",
            "The standard company email signature is missing from new messages and replies in Outlook.",
            "Email", "Low", TicketStatusNames.Closed, "daniel.brooks@resolvehub.test", "emily.carter@resolvehub.test"),
        new("usb", Utc(2026, 7, 28, 14, 45), "Unknown USB device blocked by security policy",
            "An approved encrypted USB device is blocked by endpoint security and is needed to transfer project documentation.",
            "Security", "High", TicketStatusNames.Pending, "brandon.turner@resolvehub.test", "michael.thompson@resolvehub.test"),
        new("monitor", Utc(2026, 7, 12, 11, 0), "Monitor flickers after connecting to dock",
            "The external monitor flickers every few seconds when connected through the company-issued docking station.",
            "Hardware", "Low", TicketStatusNames.Assigned, "daniel.brooks@resolvehub.test", "natalie.hayes@resolvehub.test")
    ];

    public static async Task RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken token = default)
    {
        var settings = configuration.GetSection(PresentationDemoDataSettings.SectionName)
            .Get<PresentationDemoDataSettings>() ?? new PresentationDemoDataSettings();
        if (!settings.Enabled && !settings.Cleanup)
            return;
        if (settings.Enabled && settings.Cleanup)
            throw new InvalidOperationException(
                "DemoData:Enabled and DemoData:Cleanup cannot both be true.");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token)
                    : null;
                try
                {
                    if (db.Database.IsSqlServer())
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            "EXEC sp_getapplock @Resource = 'ResolveHub.PresentationDemoData.v1', " +
                            "@LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000",
                            token);
                    }

                    if (settings.Cleanup)
                        await CleanupAsync(db, logger, token);
                    else
                        await SeedAsync(db, logger, token);

                    if (transaction is not null)
                        await transaction.CommitAsync(token);
                }
                catch
                {
                    if (transaction is not null)
                        await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Presentation demo data operation failed.");
            throw;
        }
    }

    private static async Task SeedAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken token)
    {
        logger.LogInformation("Presentation demo data seeding started.");
        ValidateDefinitionDistribution();
        var requiredEmails = Definitions
            .SelectMany(item => new[] { item.RequesterEmail, item.AgentEmail })
            .OfType<string>()
            .Append("lauren.prescott@resolvehub.test")
            .Append("ryan.whitmore@resolvehub.test")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var users = await db.Users.Where(user => user.Email != null && requiredEmails.Contains(user.Email))
            .ToDictionaryAsync(user => user.Email!, StringComparer.OrdinalIgnoreCase, token);
        var missingUsers = requiredEmails.Where(email => !users.ContainsKey(email)).ToArray();
        if (missingUsers.Length > 0)
            throw new InvalidOperationException(
                $"Presentation demo data requires these existing seeded users: {string.Join(", ", missingUsers)}.");

        var categories = await db.TicketCategories.Where(item => item.IsActive)
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase, token);
        var priorities = await db.TicketPriorities.Where(item => item.IsActive)
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase, token);
        var statuses = await db.TicketStatuses.Where(item => item.IsActive)
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase, token);
        EnsureLookups(categories.Keys, Definitions.Select(item => item.Category), "categories");
        EnsureLookups(priorities.Keys, Definitions.Select(item => item.Priority), "priorities");
        EnsureLookups(statuses.Keys, Definitions.Select(item => item.Status), "statuses");

        var titles = Definitions.Select(item => item.Title).ToArray();
        var descriptions = Definitions.Select(item => item.Description).ToArray();
        var agentIds = Definitions.Where(item => item.AgentEmail is not null)
            .Select(item => users[item.AgentEmail!].Id).Distinct().ToArray();
        var existingActiveCounts = await db.Tickets.AsNoTracking()
            .Where(ticket => !ticket.IsDeleted &&
                ticket.AssignedToUserAccountID.HasValue &&
                agentIds.Contains(ticket.AssignedToUserAccountID.Value) &&
                TicketWorkloadRules.ActiveStatuses.Contains(ticket.TicketStatus.Name) &&
                !(titles.Contains(ticket.Title) && descriptions.Contains(ticket.Description)))
            .GroupBy(ticket => ticket.AssignedToUserAccountID!.Value)
            .Select(group => new { AgentId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.AgentId, item => item.Count, token);
        foreach (var group in Definitions.Where(item => item.AgentEmail is not null &&
                     TicketWorkloadRules.ActiveStatuses.Contains(item.Status))
                     .GroupBy(item => users[item.AgentEmail!].Id))
        {
            var resultingCount = existingActiveCounts.GetValueOrDefault(group.Key) + group.Count();
            if (resultingCount > TicketWorkloadRules.MaxActiveTicketsPerAgent)
                throw new InvalidOperationException(
                    $"Presentation demo data would exceed the active-ticket capacity for {FullName(users.Values.Single(user => user.Id == group.Key))}.");
        }

        var existing = await db.Tickets.Where(ticket => titles.Contains(ticket.Title))
            .ToListAsync(token);
        var tickets = new Dictionary<string, Ticket>(StringComparer.OrdinalIgnoreCase);
        var createdCount = 0;

        foreach (var definition in Definitions)
        {
            var requesterId = users[definition.RequesterEmail].Id;
            var ticket = existing.SingleOrDefault(item =>
                item.Title == definition.Title &&
                item.Description == definition.Description &&
                item.CreatedByUserAccountID == requesterId &&
                item.CreatedDate == definition.CreatedDate);
            if (ticket is null)
            {
                var assignedDate = definition.AgentEmail is null
                    ? (DateTime?)null : definition.CreatedDate.AddMinutes(35);
                DateTime? resolvedDate = definition.Status is TicketStatusNames.Resolved or TicketStatusNames.Closed
                    ? definition.CreatedDate.AddHours(8) : null;
                DateTime? closedDate = definition.Status == TicketStatusNames.Closed
                    ? definition.CreatedDate.AddHours(30) : null;
                DateTime? cancelledDate = definition.Status == TicketStatusNames.Cancelled
                    ? definition.CreatedDate.AddHours(4) : null;
                ticket = new Ticket
                {
                    TicketReferenceNumber = $"PENDING-DEMO-{Guid.NewGuid():N}"[..32],
                    CreatedByUserAccountID = requesterId,
                    AssignedToUserAccountID = definition.AgentEmail is null
                        ? null : users[definition.AgentEmail!].Id,
                    TicketCategoryID = categories[definition.Category].ID,
                    TicketPriorityID = priorities[definition.Priority].ID,
                    TicketStatusID = statuses[definition.Status].ID,
                    Title = definition.Title,
                    Description = definition.Description,
                    CreatedDate = definition.CreatedDate,
                    UpdatedDate = closedDate ?? cancelledDate ?? resolvedDate ?? assignedDate ?? definition.CreatedDate,
                    AssignedDate = assignedDate,
                    ResolvedDate = resolvedDate,
                    ClosedDate = closedDate,
                    CancelledDate = cancelledDate,
                    CancelledReason = definition.Status == TicketStatusNames.Cancelled
                        ? "The requester confirmed the VPN access was no longer required." : null,
                    ResolutionSummary = resolvedDate.HasValue
                        ? ResolutionFor(definition.Key) : null,
                    ResolvedByUserAccountID = resolvedDate.HasValue && definition.AgentEmail is not null
                        ? users[definition.AgentEmail!].Id : null,
                    IsDeleted = false
                };
                db.Tickets.Add(ticket);
                createdCount++;
            }
            tickets[definition.Key] = ticket;
        }

        if (createdCount > 0)
        {
            await db.SaveChangesAsync(token);
            foreach (var definition in Definitions)
            {
                var ticket = tickets[definition.Key];
                if (ticket.TicketReferenceNumber.StartsWith("PENDING-DEMO-", StringComparison.Ordinal))
                    ticket.TicketReferenceNumber = $"RH-{ticket.CreatedDate.Year}-{ticket.ID:D4}";
            }
            await db.SaveChangesAsync(token);
        }

        tickets["printer"].OriginalTicketID = tickets["battery"].ID;
        await EnsureWorkflowRecordsAsync(db, tickets, users, token);
        await EnsureDraftAsync(db, users["daniel.brooks@resolvehub.test"].Id,
            categories["Access Request"].ID, priorities["Medium"].ID, token);

        var ticketIds = tickets.Values.Select(item => item.ID).ToArray();
        var ticketReferences = tickets.Values.Select(item => item.TicketReferenceNumber).ToArray();
        var commentRows = await db.TicketComments.AsNoTracking()
            .Where(item => ticketIds.Contains(item.TicketID))
            .Select(item => new { item.TicketID, item.Content }).ToListAsync(token);
        var historyRows = await db.TicketHistory.AsNoTracking()
            .Where(item => ticketIds.Contains(item.TicketID))
            .Select(item => new { item.TicketID, item.ActionType, item.Description }).ToListAsync(token);
        var activityRows = await db.ActivityLogs.AsNoTracking()
            .Where(item => item.EntityType == "Ticket" && ticketReferences.Contains(item.EntityID))
            .Select(item => new { item.EntityID, item.ActionType }).ToListAsync(token);
        var notificationRows = await db.UserNotifications.AsNoTracking()
            .Where(item => item.TicketReferenceNumber != null &&
                ticketReferences.Contains(item.TicketReferenceNumber))
            .Select(item => new
            {
                item.UserAccountID, item.TicketReferenceNumber, item.Type, item.Title
            }).ToListAsync(token);
        var commentKeys = commentRows.Select(item => (item.TicketID, item.Content)).ToHashSet();
        var historyKeys = historyRows.Select(item =>
            (item.TicketID, item.ActionType, item.Description ?? string.Empty)).ToHashSet();
        var activityKeys = activityRows.Select(item => (item.EntityID, item.ActionType)).ToHashSet();
        var notificationKeys = notificationRows.Select(item =>
            (item.UserAccountID, item.TicketReferenceNumber!, item.Type, item.Title)).ToHashSet();

        EnsureComments(db, tickets, users, commentKeys);
        EnsureHistoriesAndActivity(db, tickets, users, historyKeys, activityKeys);
        EnsureNotifications(db, tickets, users, notificationKeys);
        await db.SaveChangesAsync(token);
        await VerifySeededDataAsync(db, tickets, users, logger, token);

        logger.LogInformation(
            "Presentation demo data seeding completed. Created {CreatedCount} tickets; {TotalCount} deterministic demo tickets are present.",
            createdCount,
            tickets.Count);
    }

    private static async Task VerifySeededDataAsync(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, Ticket> tickets,
        IReadOnlyDictionary<string, UserAccount> users,
        ILogger logger,
        CancellationToken token)
    {
        var ticketIds = tickets.Values.Select(item => item.ID).ToArray();
        var demoCount = await db.Tickets.CountAsync(item => ticketIds.Contains(item.ID), token);
        var pendingAssignmentCount = await db.TicketAssignmentRequests.CountAsync(item =>
            item.TicketID == tickets["wifi"].ID &&
            item.RequestedByUserAccountID == users["michael.thompson@resolvehub.test"].Id &&
            item.Status == AssignmentRequestStatusNames.Pending, token);
        var pendingCancellationCount = await db.TicketCancellationRequests.CountAsync(item =>
            item.TicketID == tickets["finance-drive"].ID &&
            item.RequestedByAgentUserAccountID == users["emily.carter@resolvehub.test"].Id &&
            item.Status == CancellationRequestStatusNames.Pending, token);
        var draftCount = await db.TicketDrafts.CountAsync(item =>
            item.UserAccountID == users["daniel.brooks@resolvehub.test"].Id &&
            item.Title == DraftTitle && item.Description == DraftDescription, token);
        var workloads = await db.Tickets.AsNoTracking()
            .Where(item => ticketIds.Contains(item.ID) &&
                item.AssignedToUserAccountID.HasValue &&
                TicketWorkloadRules.ActiveStatuses.Contains(item.TicketStatus.Name))
            .GroupBy(item => item.AssignedToUserAccountID!.Value)
            .Select(group => new { AgentId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.AgentId, item => item.Count, token);

        if (demoCount != Definitions.Length || draftCount != 1 ||
            workloads.Values.Any(count =>
                count > TicketWorkloadRules.MaxActiveTicketsPerAgent))
        {
            throw new InvalidOperationException(
                "Presentation demo data verification failed; the transaction will be rolled back.");
        }

        logger.LogInformation(
            "Presentation demo verification passed: {TicketCount} tickets, assignment requests={AssignmentRequests}, cancellation requests={CancellationRequests}, drafts={Drafts}; active workload Emily={Emily}, Michael={Michael}, Natalie={Natalie}.",
            demoCount,
            pendingAssignmentCount,
            pendingCancellationCount,
            draftCount,
            workloads.GetValueOrDefault(users["emily.carter@resolvehub.test"].Id),
            workloads.GetValueOrDefault(users["michael.thompson@resolvehub.test"].Id),
            workloads.GetValueOrDefault(users["natalie.hayes@resolvehub.test"].Id));
    }

    private static void ValidateDefinitionDistribution()
    {
        static int Count(IEnumerable<DemoTicketDefinition> items, string value,
            Func<DemoTicketDefinition, string> selector) =>
            items.Count(item => selector(item) == value);

        if (Definitions.Length != 14 ||
            Count(Definitions, "Low", item => item.Priority) != 4 ||
            Count(Definitions, "Medium", item => item.Priority) != 4 ||
            Count(Definitions, "High", item => item.Priority) != 4 ||
            Count(Definitions, "Critical", item => item.Priority) != 2 ||
            TicketStatusNames.All.Any(status => !Definitions.Any(item => item.Status == status)) ||
            new[] { "Hardware", "Software", "Network", "Email", "Access Request", "Security", "Other" }
                .Any(category => !Definitions.Any(item => item.Category == category)))
        {
            throw new InvalidOperationException(
                "Presentation demo ticket definitions do not satisfy the required filter distribution.");
        }
    }

    private static async Task EnsureWorkflowRecordsAsync(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, Ticket> tickets,
        IReadOnlyDictionary<string, UserAccount> users,
        CancellationToken token)
    {
        var michael = users["michael.thompson@resolvehub.test"];
        var emily = users["emily.carter@resolvehub.test"];
        var lauren = users["lauren.prescott@resolvehub.test"];
        var ryan = users["ryan.whitmore@resolvehub.test"];
        var wifi = tickets["wifi"];
        if (!await db.TicketAssignmentRequests.AnyAsync(item =>
                item.TicketID == wifi.ID &&
                item.RequestedByUserAccountID == michael.Id, token))
        {
            db.TicketAssignmentRequests.Add(new TicketAssignmentRequest
            {
                TicketID = wifi.ID,
                RequestedByUserAccountID = michael.Id,
                RequestedAgentUserAccountID = null,
                Status = AssignmentRequestStatusNames.Pending,
                RequestedDate = wifi.CreatedDate.AddMinutes(25)
            });
        }

        var finance = tickets["finance-drive"];
        if (!await db.TicketCancellationRequests.AnyAsync(item =>
                item.TicketID == finance.ID &&
                item.RequestedByAgentUserAccountID == emily.Id, token))
        {
            db.TicketCancellationRequests.Add(new TicketCancellationRequest
            {
                TicketID = finance.ID,
                RequestedByAgentUserAccountID = emily.Id,
                Reason = "The requester confirmed that access is no longer required.",
                Status = CancellationRequestStatusNames.Pending,
                RequestedDate = finance.CreatedDate.AddHours(1)
            });
        }

        var printer = tickets["printer"];
        if (!await db.DuplicateReviews.AnyAsync(item =>
                item.TicketID == printer.ID &&
                item.Status == DuplicateReviewStatusNames.Approved, token))
        {
            db.DuplicateReviews.Add(new DuplicateReview
            {
                TicketID = printer.ID,
                SuggestedOriginalTicketID = tickets["battery"].ID,
                ReportedByUserAccountID = lauren.Id,
                Reason = "The symptoms and printer asset match the existing hardware incident.",
                Status = DuplicateReviewStatusNames.Approved,
                ReviewedByUserAccountID = ryan.Id,
                CreatedDate = printer.CreatedDate.AddMinutes(40),
                ReviewedDate = printer.CreatedDate.AddHours(2)
            });
        }

        foreach (var definition in Definitions.Where(item => item.Status == TicketStatusNames.InProgress))
        {
            var ticket = tickets[definition.Key];
            var agentId = users[definition.AgentEmail!].Id;
            if (!await db.TicketWorkSessions.AnyAsync(item =>
                    item.TicketID == ticket.ID && item.EndedAt == null, token))
            {
                db.TicketWorkSessions.Add(new TicketWorkSession
                {
                    TicketID = ticket.ID,
                    ITAgentUserAccountID = agentId,
                    StartedAt = ticket.CreatedDate.AddHours(1),
                    CreatedDate = ticket.CreatedDate.AddHours(1)
                });
            }
        }

        foreach (var definition in Definitions.Where(item => item.Status == TicketStatusNames.Pending))
        {
            var ticket = tickets[definition.Key];
            var agentId = users[definition.AgentEmail!].Id;
            var session = await db.TicketWorkSessions.SingleOrDefaultAsync(item =>
                item.TicketID == ticket.ID, token);
            if (session is null)
            {
                session = new TicketWorkSession
                {
                    TicketID = ticket.ID,
                    ITAgentUserAccountID = agentId,
                    StartedAt = ticket.CreatedDate.AddHours(1),
                    EndedAt = ticket.CreatedDate.AddHours(3),
                    DurationMinutes = 120,
                    EndedReason = TicketStatusNames.Pending,
                    CreatedDate = ticket.CreatedDate.AddHours(1)
                };
                db.TicketWorkSessions.Add(session);
                await db.SaveChangesAsync(token);
            }
            if (!await db.TicketPendingRecords.AnyAsync(item =>
                    item.TicketID == ticket.ID && item.ResumedDate == null, token))
            {
                db.TicketPendingRecords.Add(new TicketPendingRecord
                {
                    TicketID = ticket.ID,
                    WorkSessionID = session.ID,
                    ReasonCode = definition.Key == "usb"
                        ? TicketPendingReasons.ManagerApproval : TicketPendingReasons.EmployeeResponse,
                    ReasonText = definition.Key == "usb"
                        ? "Waiting for manager approval" : "Waiting for employee response",
                    AdditionalNote = definition.Key == "usb"
                        ? "Endpoint Security is validating the approved device exception."
                        : "Waiting for confirmation after the mailbox profile was refreshed.",
                    CreatedByUserAccountID = agentId,
                    CreatedDate = ticket.CreatedDate.AddHours(3)
                });
            }
        }
    }

    private static async Task EnsureDraftAsync(
        ApplicationDbContext db,
        int danielId,
        int categoryId,
        int priorityId,
        CancellationToken token)
    {
        if (await db.TicketDrafts.AnyAsync(item => item.UserAccountID == danielId &&
                item.Title == DraftTitle && item.Description == DraftDescription, token))
            return;
        db.TicketDrafts.Add(new TicketDraft
        {
            UserAccountID = danielId,
            Title = DraftTitle,
            Description = DraftDescription,
            TicketCategoryID = categoryId,
            TicketPriorityID = priorityId,
            CreatedDate = Utc(2026, 8, 18, 18, 5),
            UpdatedDate = Utc(2026, 8, 18, 18, 22)
        });
    }

    private static void EnsureComments(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, Ticket> tickets,
        IReadOnlyDictionary<string, UserAccount> users,
        ISet<(int TicketId, string Content)> existing)
    {
        AddComment(db, tickets["finance-drive"], users["emily.carter@resolvehub.test"].Id,
            "I reproduced the issue and am checking the user's access permissions.",
            CommentVisibility.Public, tickets["finance-drive"].CreatedDate.AddMinutes(50), existing);
        AddComment(db, tickets["payroll"], users["sophia.mitchell@resolvehub.test"].Id,
            "The crash still occurs after restarting, just after the payroll application displays its loading screen.",
            CommentVisibility.Public, tickets["payroll"].CreatedDate.AddHours(2), existing);
        AddComment(db, tickets["outlook"], users["natalie.hayes@resolvehub.test"].Id,
            "I refreshed the mailbox profile. Please confirm whether new messages now appear.",
            CommentVisibility.Public, tickets["outlook"].CreatedDate.AddHours(2), existing);
        AddComment(db, tickets["usb"], users["michael.thompson@resolvehub.test"].Id,
            "Checked internal logs; authentication failures started after the latest policy update.",
            CommentVisibility.Private, tickets["usb"].CreatedDate.AddHours(2), existing);
    }

    private static void AddComment(
        ApplicationDbContext db,
        Ticket ticket,
        int authorId,
        string content,
        CommentVisibility visibility,
        DateTime createdDate,
        ISet<(int TicketId, string Content)> existing)
    {
        if (!existing.Add((ticket.ID, content)))
            return;
        db.TicketComments.Add(new TicketComment
        {
            TicketID = ticket.ID,
            AuthorUserAccountID = authorId,
            Content = content,
            Visibility = visibility,
            CreatedDate = createdDate
        });
    }

    private static void EnsureHistoriesAndActivity(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, Ticket> tickets,
        IReadOnlyDictionary<string, UserAccount> users,
        ISet<(int TicketId, string Action, string Description)> existingHistories,
        ISet<(string Reference, string Action)> existingActivities)
    {
        foreach (var definition in Definitions)
        {
            var ticket = tickets[definition.Key];
            AddHistory(db, ticket, users[definition.RequesterEmail].Id,
                TicketHistoryActionNames.TicketCreated, null, ticket.TicketReferenceNumber,
                "Ticket created.", definition.CreatedDate, existingHistories);
            if (definition.AgentEmail is not null)
            {
                AddHistory(db, ticket, users["ryan.whitmore@resolvehub.test"].Id,
                    TicketHistoryActionNames.TicketAssigned, TicketStatusNames.Open,
                    TicketStatusNames.Assigned,
                    $"Ticket assigned to {FullName(users[definition.AgentEmail])}.",
                    definition.CreatedDate.AddMinutes(35), existingHistories);
                AddActivity(db, ticket, users["ryan.whitmore@resolvehub.test"].Id,
                    TicketHistoryActionNames.TicketAssigned, null,
                    users[definition.AgentEmail].Id.ToString(),
                    $"Ticket assigned to {FullName(users[definition.AgentEmail])}.",
                    definition.CreatedDate.AddMinutes(35), existingActivities);
            }
            if (definition.Status is TicketStatusNames.InProgress or TicketStatusNames.Pending)
                AddHistory(db, ticket, users[definition.AgentEmail!].Id,
                    TicketHistoryActionNames.StatusChanged, TicketStatusNames.Assigned,
                    definition.Status, $"Ticket status changed to {definition.Status}.",
                    definition.CreatedDate.AddHours(1), existingHistories);
            if (definition.Status is TicketStatusNames.Resolved or TicketStatusNames.Closed)
                AddHistory(db, ticket, users[definition.AgentEmail!].Id,
                    TicketHistoryActionNames.TicketResolved, TicketStatusNames.InProgress,
                    TicketStatusNames.Resolved, "Ticket resolved after successful troubleshooting.",
                    definition.CreatedDate.AddHours(8), existingHistories);
            if (definition.Status == TicketStatusNames.Closed)
                AddHistory(db, ticket, users[definition.AgentEmail!].Id,
                    TicketHistoryActionNames.TicketClosed, TicketStatusNames.Resolved,
                    TicketStatusNames.Closed, "Ticket closed after resolution was confirmed.",
                    definition.CreatedDate.AddHours(30), existingHistories);
        }

        AddHistory(db, tickets["wifi"], users["michael.thompson@resolvehub.test"].Id,
            TicketHistoryActionNames.AssignmentRequested, null,
            AssignmentRequestStatusNames.Pending,
            "Assignment requested by Michael Thompson.", tickets["wifi"].CreatedDate.AddMinutes(25), existingHistories);
        AddHistory(db, tickets["finance-drive"], users["emily.carter@resolvehub.test"].Id,
            TicketHistoryActionNames.CancellationRequested, null,
            CancellationRequestStatusNames.Pending,
            "Emily Carter requested cancellation because access is no longer required.",
            tickets["finance-drive"].CreatedDate.AddHours(1), existingHistories);
        AddHistory(db, tickets["printer"], users["lauren.prescott@resolvehub.test"].Id,
            TicketHistoryActionNames.DuplicateReviewReported, null,
            tickets["battery"].TicketReferenceNumber,
            "Lauren Prescott reported this ticket as a duplicate of an existing hardware issue.",
            tickets["printer"].CreatedDate.AddMinutes(40), existingHistories);
        AddHistory(db, tickets["printer"], users["ryan.whitmore@resolvehub.test"].Id,
            TicketHistoryActionNames.DuplicateMarked, TicketStatusNames.Open,
            TicketStatusNames.Duplicate,
            "Administrator marked the ticket as a duplicate.",
            tickets["printer"].CreatedDate.AddHours(2), existingHistories);
    }

    private static void EnsureNotifications(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, Ticket> tickets,
        IReadOnlyDictionary<string, UserAccount> users,
        ISet<(int UserId, string Reference, string Type, string Title)> existing)
    {
        var lauren = users["lauren.prescott@resolvehub.test"].Id;
        var michael = users["michael.thompson@resolvehub.test"].Id;
        var daniel = users["daniel.brooks@resolvehub.test"].Id;
        AddNotification(db, lauren, tickets["wifi"], NotificationTypeNames.AssignmentRequestCreated,
            "New Assignment Request", "Michael Thompson requested assignment to this ticket.", false,
            tickets["wifi"].CreatedDate.AddMinutes(25), existing);
        AddNotification(db, lauren, tickets["finance-drive"], NotificationTypeNames.CancellationRequestCreated,
            "Cancellation request received", "Emily Carter submitted a cancellation request for review.", false,
            tickets["finance-drive"].CreatedDate.AddHours(1), existing);
        AddNotification(db, michael, tickets["signin"], NotificationTypeNames.TicketAssigned,
            "Ticket Assigned", "A critical-priority security ticket has been assigned to you.", false,
            tickets["signin"].CreatedDate.AddMinutes(35), existing);
        AddNotification(db, michael, tickets["wifi"], NotificationTypeNames.AssignmentRequestCreated,
            "Assignment request submitted", "Your assignment request was submitted for Manager review.", true,
            tickets["wifi"].CreatedDate.AddMinutes(25), existing);
        AddNotification(db, michael, tickets["payroll"], NotificationTypeNames.PublicCommentAdded,
            "New comment", "A new comment was added to an assigned ticket.", false,
            tickets["payroll"].CreatedDate.AddHours(2), existing);
        AddNotification(db, daniel, tickets["outlook"], NotificationTypeNames.TicketAssigned,
            "Ticket assigned", "Your ticket was assigned to Natalie Hayes.", true,
            tickets["outlook"].CreatedDate.AddMinutes(35), existing);
        AddNotification(db, daniel, tickets["outlook"], NotificationTypeNames.PublicCommentAdded,
            "New comment", "Natalie Hayes added a new comment to your email ticket.", false,
            tickets["outlook"].CreatedDate.AddHours(2), existing);
        AddNotification(db, daniel, tickets["outlook"], NotificationTypeNames.TicketPending,
            "Ticket status changed", "Your email ticket is pending your confirmation.", false,
            tickets["outlook"].CreatedDate.AddHours(3), existing);
        AddNotification(db, daniel, tickets["acrobat"], NotificationTypeNames.TicketResolved,
            "Ticket resolved", "Your software license request was resolved.", true,
            tickets["acrobat"].CreatedDate.AddHours(8), existing);
    }

    private static void AddHistory(
        ApplicationDbContext db,
        Ticket ticket,
        int actorId,
        string action,
        string? oldValue,
        string? newValue,
        string description,
        DateTime date,
        ISet<(int TicketId, string Action, string Description)> existing)
    {
        if (!existing.Add((ticket.ID, action, description)))
            return;
        db.TicketHistory.Add(new TicketHistory
        {
            TicketID = ticket.ID,
            PerformedByUserAccountID = actorId,
            ActionType = action,
            OldValue = oldValue,
            NewValue = newValue,
            Description = description,
            CreatedDate = date
        });
    }

    private static void AddActivity(
        ApplicationDbContext db,
        Ticket ticket,
        int actorId,
        string action,
        string? oldValue,
        string? newValue,
        string description,
        DateTime date,
        ISet<(string Reference, string Action)> existing)
    {
        if (!existing.Add((ticket.TicketReferenceNumber, action)))
            return;
        db.ActivityLogs.Add(new ActivityLog
        {
            PerformedByUserAccountID = actorId,
            ActionType = action,
            EntityType = "Ticket",
            EntityID = ticket.TicketReferenceNumber,
            Description = description,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedDate = date
        });
    }

    private static void AddNotification(
        ApplicationDbContext db,
        int userId,
        Ticket ticket,
        string type,
        string title,
        string message,
        bool isRead,
        DateTime date,
        ISet<(int UserId, string Reference, string Type, string Title)> existing)
    {
        if (!existing.Add((userId, ticket.TicketReferenceNumber, type, title)))
            return;
        db.UserNotifications.Add(new UserNotification
        {
            UserAccountID = userId,
            Type = type,
            Title = title,
            Message = message,
            TicketReferenceNumber = ticket.TicketReferenceNumber,
            IsRead = isRead,
            CreatedDate = date
        });
    }

    private static async Task CleanupAsync(
        ApplicationDbContext db,
        ILogger logger,
        CancellationToken token)
    {
        logger.LogInformation("Presentation demo data cleanup started.");
        var titles = Definitions.Select(item => item.Title).ToArray();
        var descriptions = Definitions.Select(item => item.Description).ToArray();
        var demoTickets = db.Tickets.Where(ticket =>
            titles.Contains(ticket.Title) && descriptions.Contains(ticket.Description));
        var ticketIds = demoTickets.Select(ticket => ticket.ID);
        var references = demoTickets.Select(ticket => ticket.TicketReferenceNumber);
        var nonDemoReferences = await db.Tickets.CountAsync(ticket =>
            !ticketIds.Contains(ticket.ID) && ticket.OriginalTicketID.HasValue &&
            ticketIds.Contains(ticket.OriginalTicketID.Value), token);
        if (nonDemoReferences > 0)
            throw new InvalidOperationException(
                "Cleanup stopped because a non-demo ticket references a presentation demo ticket.");

        var commentIds = db.TicketComments.Where(item => ticketIds.Contains(item.TicketID))
            .Select(item => item.ID);
        await db.TicketCommentAttachments.Where(item => commentIds.Contains(item.TicketCommentID))
            .ExecuteDeleteAsync(token);
        await db.TicketPendingRecords.Where(item => ticketIds.Contains(item.TicketID))
            .ExecuteDeleteAsync(token);
        await db.TicketWorkSessions.Where(item => ticketIds.Contains(item.TicketID))
            .ExecuteDeleteAsync(token);
        await db.TicketComments.Where(item => ticketIds.Contains(item.TicketID))
            .ExecuteDeleteAsync(token);
        await db.TicketAttachments.Where(item => ticketIds.Contains(item.TicketID))
            .ExecuteDeleteAsync(token);
        await db.TicketHistory.Where(item => ticketIds.Contains(item.TicketID))
            .ExecuteDeleteAsync(token);
        await db.TicketAssignmentRequests.Where(item => ticketIds.Contains(item.TicketID))
            .ExecuteDeleteAsync(token);
        await db.TicketCancellationRequests.Where(item => ticketIds.Contains(item.TicketID))
            .ExecuteDeleteAsync(token);
        await db.DuplicateReviews.Where(item => ticketIds.Contains(item.TicketID) ||
                ticketIds.Contains(item.SuggestedOriginalTicketID))
            .ExecuteDeleteAsync(token);
        await db.UserNotifications.Where(item => item.TicketReferenceNumber != null &&
                references.Contains(item.TicketReferenceNumber))
            .ExecuteDeleteAsync(token);
        await db.ActivityLogs.Where(item => item.EntityType == "Ticket" &&
                references.Contains(item.EntityID))
            .ExecuteDeleteAsync(token);
        await demoTickets.ExecuteUpdateAsync(setters =>
            setters.SetProperty(ticket => ticket.OriginalTicketID, (int?)null), token);
        var removedTickets = await demoTickets.ExecuteDeleteAsync(token);

        var danielId = await db.Users.Where(user => user.Email == "daniel.brooks@resolvehub.test")
            .Select(user => (int?)user.Id).SingleOrDefaultAsync(token);
        var removedDrafts = danielId.HasValue
            ? await db.TicketDrafts.Where(item => item.UserAccountID == danielId.Value &&
                    item.Title == DraftTitle && item.Description == DraftDescription)
                .ExecuteDeleteAsync(token)
            : 0;
        logger.LogInformation(
            "Presentation demo data cleanup completed. Removed {TicketCount} tickets and {DraftCount} drafts.",
            removedTickets,
            removedDrafts);
    }

    private static void EnsureLookups(
        IEnumerable<string> available,
        IEnumerable<string> required,
        string lookupType)
    {
        var availableSet = available.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(item => !availableSet.Contains(item)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(
                $"Presentation demo data requires these active {lookupType}: {string.Join(", ", missing)}.");
    }

    private static string ResolutionFor(string key) => key switch
    {
        "battery" => "Replaced the faulty power adapter and confirmed that the battery charges normally.",
        "teams-mic" => "Reset the audio device permissions and confirmed microphone input in a test call.",
        "acrobat" => "Assigned the approved license and verified that Acrobat activated successfully.",
        "signature" => "Restored the managed Outlook signature template and confirmed it appears in new messages.",
        _ => "The issue was resolved and verified with the requester."
    };

    private static string FullName(UserAccount user) =>
        $"{user.FirstName} {user.LastName}".Trim();

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private sealed record DemoTicketDefinition(
        string Key,
        DateTime CreatedDate,
        string Title,
        string Description,
        string Category,
        string Priority,
        string Status,
        string RequesterEmail,
        string? AgentEmail);
}
