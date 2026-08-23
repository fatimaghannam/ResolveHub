using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.AI;

public sealed class AnalyzeTicketRequest
{
    [Required, StringLength(200, MinimumLength = 5)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(5000, MinimumLength = 10)] public string Description { get; init; } = string.Empty;
}

public sealed record TicketAnalysisResponse(int SuggestedCategoryId, string SuggestedCategoryName,
    int SuggestedPriorityId, string SuggestedPriorityName, string? CategoryReason, string? PriorityReason);
public sealed record TicketSummaryResponse(string Summary);
public sealed record TroubleshootingResponse(string Overview, IReadOnlyCollection<string> Steps, bool EscalationRecommended);

public sealed class AiChatRequest
{
    [Required, MinLength(1), MaxLength(10)] public IReadOnlyCollection<AiChatMessage> Messages { get; init; } = [];
    public int? TicketId { get; init; }
    [StringLength(40), RegularExpression("^[a-z]+(?:-[a-z]+)*$")]
    public string? PageContext { get; init; }
}
public sealed class AiChatMessage
{
    [Required, RegularExpression("^(user|assistant)$")] public string Role { get; init; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 1)] public string Content { get; init; } = string.Empty;
}
public sealed record AiTicketLookupItem(
    int TicketId,
    string TicketNumber,
    string Title,
    string Category,
    string Priority,
    string Status,
    string? AssignedAgentName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? LatestUpdate);

public sealed record AiTicketLookupResult(
    IReadOnlyCollection<AiTicketLookupItem> Tickets,
    int TotalCount);

public sealed record AiChatAction(string Type, int TicketId, string TicketNumber);

public sealed record AiChatResponse(
    string Message,
    AiChatAction? Action = null,
    AiTicketLookupResult? TicketLookup = null);
