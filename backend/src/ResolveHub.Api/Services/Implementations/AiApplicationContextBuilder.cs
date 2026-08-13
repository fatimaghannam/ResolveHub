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

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> PagesByRole =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [RoleNames.Employee] = new HashSet<string> { "dashboard", "my-tickets", "create-ticket", "ticket-details", "notifications", "profile" },
            [RoleNames.ITSupportAgent] = new HashSet<string> { "dashboard", "assigned-tickets", "open-tickets", "ticket-details", "notifications", "profile" },
            [RoleNames.Manager] = new HashSet<string> { "dashboard", "all-tickets", "ticket-details", "ticket-assignments", "team-workload", "audit-log", "notifications", "profile" },
            [RoleNames.Admin] = new HashSet<string> { "dashboard", "all-tickets", "my-tickets", "create-ticket", "ticket-details", "ticket-assignments", "team-workload", "users", "categories", "audit-log", "notifications", "profile" }
        };

    public async Task<string> BuildAsync(string role, string? pageContext, string? currentQuestion, CancellationToken token)
    {
        var question = currentQuestion ?? string.Empty;
        var asksAboutCreation = ContainsAny(question, "create a ticket", "create ticket", "tickets can i create", "ticket can i create", "submit a ticket", "save as draft", "new ticket", "report an issue");
        var asksAboutCategories = ContainsAny(question, "category", "categories", "ticket type", "ticket types", "types of tickets", "kind of ticket");
        var asksAboutAssignment = ContainsAny(question, "assign", "assignment", "assigned to me", "get a ticket");
        var asksAboutWorkload = IsWorkloadQuestion(question);
        var asksAboutTicketViewing = ContainsAny(question, "inspect a ticket", "inspect ticket", "view a ticket", "view ticket", "ticket details", "open a ticket to view", "look at a ticket");
        var asksAboutStatusChecking = ContainsAny(question, "view the ticket status", "view ticket status", "check the ticket status", "check ticket status", "status of one", "specific ticket", "find all open tickets", "tickets with a specific status", "where do i view", "where can i view");
        var asksAboutRoles = IsRoleQuestion(question);
        var asksAboutResolveHubPurpose = IsResolveHubPurposeQuestion(question);
        var asksAboutCapabilities = ContainsAny(question, "what can i do", "what can managers do", "what can manager", "capabilities", "my role", "permissions");
        var asksAboutNavigation = ContainsAny(question, "where", "navigation", "menu", "dashboard", "this page");
        IReadOnlyCollection<string> categories = asksAboutCategories
            ? await db.TicketCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(token) : [];
        IReadOnlyCollection<string> priorities = ContainsAny(question, "priority", "priorities", "critical", "urgent")
            ? await db.TicketPriorities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(token) : [];
        IReadOnlyCollection<string> statuses = !asksAboutAssignment && ContainsAny(question, "status", "open", "assigned", "progress", "pending", "resolved", "closed", "cancelled", "duplicate")
            ? await db.TicketStatuses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(token) : [];
        var currentPage = PageFor(role, pageContext);

        return $"""
            BEGIN TRUSTED RESOLVEHUB APPLICATION CONTEXT
            Application: ResolveHub is an IT help-desk and ticket management system.
            Authenticated role: {role}
            {OptionalLine("Ticket creation permission", asksAboutCreation ? TicketCreationPermissionFor(role) : null)}
            {OptionalLine("Authoritative assignment workflow and exact UI labels", asksAboutAssignment ? AssignmentFor(role, pageContext) : null)}
            {OptionalLine("Authoritative ticket viewing workflow", asksAboutTicketViewing ? TicketViewingFor(role, currentPage) : null)}
            {OptionalLine("Authoritative ticket status checking", asksAboutStatusChecking ? TicketStatusCheckingFor(role) : null)}
            {OptionalLine("Authoritative ResolveHub roles", asksAboutRoles ? RoleKnowledge : null)}
            {OptionalLine("Authoritative ResolveHub purpose and capabilities", asksAboutResolveHubPurpose ? ResolveHubPurposeKnowledge : null)}
            {OptionalLine("Authoritative IT Agent workload guidance", asksAboutWorkload ? WorkloadFor(role, question, currentPage) : null)}
            {OptionalLine("Role capabilities", asksAboutCapabilities ? CapabilitiesFor(role) : null)}
            {OptionalLine("Available navigation", asksAboutNavigation || asksAboutCapabilities || asksAboutCreation ? NavigationFor(role) : null)}
            {OptionalLine("Current page", currentPage)}
            {OptionalLine("Create Ticket fields", asksAboutCreation && role is RoleNames.Employee or RoleNames.Admin ? "Title, Description, Category, Priority, Attachments" : null)}
            {OptionalLookup("Active categories", categories)}
            {OptionalLookup("Active priorities", priorities)}
            {OptionalLookup("Active statuses", statuses)}
            END TRUSTED RESOLVEHUB APPLICATION CONTEXT
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

    private static string AssignmentFor(string role, string? pageContext) => role switch
    {
        RoleNames.Manager => $"{(pageContext == "all-tickets" ? "Find the open ticket" : "Go to All Tickets and find the open ticket")}, then click the exact row action Assign. Select the desired IT Agent and submit the assignment request. An Administrator—not the requesting Manager—approves or rejects it. If approved, the ticket becomes Assigned. Maximum active workload is {TicketWorkloadRules.MaxActiveTicketsPerAgent} tickets per agent. Never call the Manager action Request Assignment.",
        RoleNames.Admin => $"{(pageContext == "all-tickets" ? "Find the open ticket" : "Go to All Tickets and find the open ticket")}, then click the exact row action Assign. Select the IT Agent and confirm the assignment. It is assigned directly with no additional approval. Maximum active workload is {TicketWorkloadRules.MaxActiveTicketsPerAgent} tickets per agent.",
        RoleNames.ITSupportAgent => $"From Open Tickets, open an unassigned ticket and click the exact ticket-details action Request Assignment. Submit the self-assignment request. A Manager approves or rejects it; approval changes the ticket to Assigned. Maximum active workload is {TicketWorkloadRules.MaxActiveTicketsPerAgent} tickets per agent.",
        RoleNames.Employee => "Employees cannot assign tickets.",
        _ => "No assignment capability is available."
    };

    private static string TicketViewingFor(string role, string? currentPage) => role switch
    {
        RoleNames.Manager => $"Managers browse organizational tickets from All Tickets. {(currentPage == "All Tickets" ? "Find the ticket" : "Go to All Tickets and find the ticket")}, then click the exact row action View. This opens Ticket Details. Ticket Assignments is for assignment workflows, not general ticket inspection. Do not infer or state additional editing restrictions.",
        _ => "Use only the role's confirmed ticket navigation and exact controls from trusted context; do not infer a viewing workflow."
    };

    private static string TicketStatusCheckingFor(string role) => role switch
    {
        RoleNames.Manager => "All Tickets displays each ticket's status and has the exact filter Status for viewing tickets with a selected status. To check one specific ticket, find it in All Tickets and click the exact row action View; Ticket Details shows that ticket's current status.",
        _ => "Use only status controls explicitly confirmed for the authenticated role; do not infer controls or pages."
    };

    private const string RoleKnowledge = "ResolveHub has exactly four product-facing roles: Employee, IT Agent, Manager, and Admin. Employee creates and tracks their tickets. IT Agent handles assigned IT issues. Manager oversees organizational ticket workflows and approvals. Admin manages users and has broader system and ticket management permissions. End User, Supervisor, Technician, System Administrator, and other generic help-desk labels are not ResolveHub roles. Always use the exact product-facing names Employee, IT Agent, Manager, and Admin when explaining roles; authenticated claim names remain authoritative for authorization.";
    private const string ResolveHubPurposeKnowledge = "ResolveHub is an IT Help Desk and Ticketing Management System that centralizes IT support workflows. Confirmed capabilities: ticket creation and tracking; category and priority selection; AI category and priority recommendations that users may apply; assignment to IT Agents; status tracking; ticket comments and attachments; notifications; assignment approval, cancellation request, and duplicate ticket workflows; Manager and Admin reporting with PDF and Excel export; and role-based access for Employee, IT Agent, Manager, and Admin. ResolveHub supports people through issue resolution workflows but does not automatically repair IT problems. Do not describe customers, stakeholders, support staff, technicians, supervisors, or other generic actors as ResolveHub roles.";

    private static string WorkloadFor(string role, string question, string? currentPage)
    {
        var asksForLiveAvailability = ContainsAny(question, "which agent", "which it agent", "available right now", "currently available");
        var access = role is RoleNames.Manager or RoleNames.Admin
            ? "Team Workload is the only confirmed page for monitoring IT Agent workload and capacity. It displays the availability/capacity badge, active ticket count, maximum capacity, remaining slots, Assigned count, In Progress count, Pending count, and View tickets action for each IT Agent."
            : "Managers and Administrators use Team Workload to monitor IT Agent capacity.";
        var currentPageGuidance = role is RoleNames.Manager or RoleNames.Admin
            ? currentPage == "Team Workload"
                ? "Confirmed presentation context: the user is already on Team Workload."
                : "Confirmed presentation context: the user is not on Team Workload; direct them to open Team Workload from the sidebar without saying they are already there."
            : string.Empty;
        var inventedFeatureBoundary = ContainsAny(question, "agent availability", "availability report")
            ? "ResolveHub has no confirmed Agent Availability page or report; direct the user to Team Workload and do not describe report filters."
            : string.Empty;
        var liveDataBoundary = asksForLiveAvailability
            ? "Current Team Workload data was not supplied to the assistant, so do not identify an agent or claim how many agents are available."
            : "Do not claim any specific agent is currently available without trusted live Team Workload data.";
        return $"{access} Maximum active tickets per IT Agent is {TicketWorkloadRules.MaxActiveTicketsPerAgent}. Fewer than {TicketWorkloadRules.MaxActiveTicketsPerAgent} active tickets means capacity remains. Capacity labels are Available (0–3), Near Capacity (4), Full (5), and Over Capacity (more than 5). {currentPageGuidance} {inventedFeatureBoundary} {liveDataBoundary}";
    }

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

    private static bool IsWorkloadQuestion(string value) => ContainsAny(value,
        "available it agent", "available agent", "agent availability", "availability report", "which agent",
        "which it agent", "agent workload", "team workload", "agent capacity", "active tickets", "how many tickets");

    private static bool IsRoleQuestion(string value) => ContainsAny(value,
        "users of the system", "users of this system", "system users", "what roles", "which roles", "roles are there", "system roles",
        "who uses resolvehub", "who are the users", "types of users", "about the roles", "users are in resolvehub",
        "user roles", "role responsibilities", "each role", "end user role", "end-user role", "system administrator", "about managers", "about manager", "what users");

    private static bool IsResolveHubPurposeQuestion(string value) => ContainsAny(value,
        "problems does resolvehub solve", "resolvehub used for", "resolvehub help with", "why do we use resolvehub",
        "purpose of resolvehub", "company use resolvehub", "what is resolvehub", "resolvehub capabilities",
        "resolvehub automatically solve", "resolvehub automatically repair");

    private static string? PageFor(string role, string? pageContext) =>
        pageContext is not null && PagesByRole.TryGetValue(role, out var allowedPages) && allowedPages.Contains(pageContext) &&
        Pages.TryGetValue(pageContext, out var page) ? page : null;

    private static string OptionalLookup(string label, IReadOnlyCollection<string> values) =>
        values.Count == 0 ? string.Empty : $"{label}: {string.Join(", ", values)}";

    private static string OptionalLine(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label}: {value}";
}
