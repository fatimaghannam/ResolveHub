using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AiApplicationContextBuilder(ApplicationDbContext db) : IAiApplicationContextBuilder
{
    private static readonly IReadOnlyDictionary<string, string> Pages = new Dictionary<string, string>
    {
        ["dashboard"] = "Dashboard",
        ["my-tickets"] = "My Tickets",
        ["create-ticket"] = "Create Ticket. Visible fields: Title, Description, Category, Priority, and Attachments. Available actions: Analyze with AI, Apply Suggestions after analysis, Save as Draft, Cancel, and Submit Ticket.",
        ["ticket-details"] = "Ticket Details",
        ["assigned-tickets"] = "Assigned Tickets",
        ["open-tickets"] = "Open Tickets",
        ["all-tickets"] = "All Tickets",
        ["ticket-assignments"] = "Ticket Assignments",
        ["team-workload"] = "Team Workload",
        ["users"] = "Users",
        ["categories"] = "Ticket Categories",
        ["audit-log"] = "System Audit Log",
        ["notifications"] = "Notifications",
        ["profile"] = "Profile"
    };

    public async Task<string> BuildAsync(string role, string? pageContext, string? currentQuestion, CancellationToken token)
    {
        var question = currentQuestion ?? string.Empty;
        var asksAboutCreation = ContainsAny(question, "create a ticket", "create ticket", "submit a ticket", "save as draft", "new ticket", "report an issue");
        var asksAboutAssignment = ContainsAny(question, "assign", "assignment", "assigned to me", "get a ticket");
        var asksAboutCapabilities = ContainsAny(question, "what can i do", "what can managers do", "what can manager", "capabilities", "my role", "permissions");
        var asksAboutNavigation = ContainsAny(question, "where", "navigation", "menu", "dashboard", "this page");
        IReadOnlyCollection<string> categories = ContainsAny(question, "category", "categories")
            ? await db.TicketCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(token) : [];
        IReadOnlyCollection<string> priorities = ContainsAny(question, "priority", "priorities", "critical", "urgent")
            ? await db.TicketPriorities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(token) : [];
        IReadOnlyCollection<string> statuses = !asksAboutAssignment && ContainsAny(question, "status", "open", "assigned", "progress", "pending", "resolved", "closed", "cancelled", "duplicate")
            ? await db.TicketStatuses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(token) : [];
        var currentPage = asksAboutNavigation && pageContext is not null && Pages.TryGetValue(pageContext, out var page)
            ? page : null;

        return $"""
            RESOLVEHUB TRUSTED APPLICATION CONTEXT
            Application: ResolveHub is an IT help-desk and ticket management system.
            Authenticated role: {role}
            {OptionalLine("Ticket creation permission", asksAboutCreation ? TicketCreationPermissionFor(role) : null)}
            {OptionalLine("Relevant assignment workflow", asksAboutAssignment ? AssignmentFor(role) : null)}
            {OptionalLine("Role capabilities", asksAboutCapabilities ? CapabilitiesFor(role) : null)}
            {OptionalLine("Available navigation", asksAboutNavigation || asksAboutCapabilities || asksAboutCreation ? NavigationFor(role) : null)}
            {OptionalLine("Current page", currentPage)}
            {OptionalLine("Create Ticket fields", asksAboutCreation && role is RoleNames.Employee or RoleNames.Admin ? "Title, Description, Category, Priority, Attachments" : null)}
            {OptionalLookup("Active categories", categories)}
            {OptionalLookup("Active priorities", priorities)}
            {OptionalLookup("Active statuses", statuses)}
            """;
    }

    private static string NavigationFor(string role) => role switch
    {
        RoleNames.Employee => "Dashboard, My Tickets, Create Ticket, Notifications",
        RoleNames.ITSupportAgent => "Dashboard, Assigned Tickets, Open Tickets, Notifications",
        RoleNames.Manager => "Dashboard, All Tickets, Ticket Assignments, Team Workload, System Audit Log, Notifications",
        RoleNames.Admin => "Dashboard, All Tickets, My Tickets, Create Ticket, Ticket Assignments, Team Workload, Users, Categories, System Audit Log, Notifications",
        _ => "No role-specific navigation is available"
    };

    private static string TicketCreationPermissionFor(string role) => role switch
    {
        RoleNames.Employee or RoleNames.Admin => "Allowed. This role has Create Ticket navigation and may receive concise creation instructions.",
        RoleNames.Manager or RoleNames.ITSupportAgent => "Not allowed. This role has no Create Ticket navigation. Do not provide creation steps; explain that only Employees and Administrators can create tickets.",
        _ => "Not allowed."
    };

    private static string AssignmentFor(string role) => role switch
    {
        RoleNames.Manager => $"Go to All Tickets, find the open ticket, click Assign, select the desired IT Agent, and submit the assignment request. An Administrator approves or rejects it. If approved, the ticket becomes Assigned. Agent workload is limited to {TicketWorkloadRules.MaxActiveTicketsPerAgent} active tickets. The Manager UI action is named Assign; never call it Request Assignment.",
        RoleNames.Admin => $"From All Tickets, select an open ticket and directly assign it to an available IT Agent; no further approval is required. The ticket becomes Assigned. Agent workload is limited to {TicketWorkloadRules.MaxActiveTicketsPerAgent} active tickets.",
        RoleNames.ITSupportAgent => $"From Open Tickets, select an open ticket and request assignment to yourself. A Manager approves or rejects the request. Approval changes the ticket to Assigned. Agent workload is limited to {TicketWorkloadRules.MaxActiveTicketsPerAgent} active tickets.",
        RoleNames.Employee => "Employees cannot assign tickets.",
        _ => "No assignment capability is available."
    };

    private static string CapabilitiesFor(string role) => role switch
    {
        RoleNames.Employee => "Create tickets, save ticket drafts, view and follow their own tickets, add public comments, and receive notifications.",
        RoleNames.ITSupportAgent => "View open tickets and assigned tickets, request assignment, update permitted ticket workflow states, add permitted comments, resolve tickets, and request cancellation.",
        RoleNames.Manager => "View all tickets, oversee team workload, request assignments, review agent self-assignment and cancellation requests, use permitted duplicate workflows, generate reports, and view the system audit log.",
        RoleNames.Admin => "View all tickets, create and follow personal tickets, directly assign open tickets, approve manager assignment requests, manage users and categories, use duplicate workflows, generate reports, review team workload, and view the system audit log.",
        _ => "No capabilities are available."
    };

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string OptionalLookup(string label, IReadOnlyCollection<string> values) =>
        values.Count == 0 ? string.Empty : $"{label}: {string.Join(", ", values)}";

    private static string OptionalLine(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label}: {value}";
}
