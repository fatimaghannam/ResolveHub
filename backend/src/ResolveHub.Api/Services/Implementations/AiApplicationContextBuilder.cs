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
        ["create-ticket"] = "Create Ticket",
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
        var categories = ContainsAny(question, "category", "categories", "ticket type", "ticket types", "kind of ticket")
            ? await db.TicketCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(token)
            : [];
        var priorities = ContainsAny(question, "priority", "priorities", "critical", "urgent")
            ? await db.TicketPriorities.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(token)
            : [];
        var statuses = ContainsAny(question, "status", "open", "assigned", "progress", "pending", "resolved", "closed", "cancelled", "duplicate")
            ? await db.TicketStatuses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(token)
            : [];

        return $"""
            BEGIN TRUSTED LIVE RESOLVEHUB CONTEXT
            Authenticated backend role claim: {role}
            {OptionalLine("Validated current page", PageFor(role, pageContext))}
            {OptionalLookup("Current active categories", categories)}
            {OptionalLookup("Current active priorities", priorities)}
            {OptionalLookup("Current active statuses", statuses)}
            END TRUSTED LIVE RESOLVEHUB CONTEXT
            """;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string? PageFor(string role, string? pageContext) =>
        pageContext is not null && PagesByRole.TryGetValue(role, out var allowedPages) && allowedPages.Contains(pageContext) &&
        Pages.TryGetValue(pageContext, out var page) ? page : null;

    private static string OptionalLookup(string label, IReadOnlyCollection<string> values) =>
        values.Count == 0 ? string.Empty : $"{label}: {string.Join(", ", values)}";

    private static string OptionalLine(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label}: {value}";
}
