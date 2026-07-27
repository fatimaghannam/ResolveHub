using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Tickets;

public sealed class AgentTicketFilterDto : IValidatableObject
{
    [StringLength(200)]
    public string? Search { get; init; }
    public int? StatusId { get; init; }
    public int? CategoryId { get; init; }
    public int? PriorityId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;
    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
    public string? SortBy { get; init; } = "assignedDate";
    public string? SortDirection { get; init; } = "desc";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromDate.HasValue != ToDate.HasValue)
            yield return new("Both fromDate and toDate must be provided together.",
                [nameof(FromDate), nameof(ToDate)]);
        else if (FromDate.HasValue && ToDate!.Value.Date < FromDate.Value.Date)
            yield return new("toDate cannot be earlier than fromDate.", [nameof(ToDate)]);
    }
}

public sealed record AgentTicketListItemDto(
    int Id,
    string TicketReferenceNumber,
    string Title,
    string RequesterName,
    string? RequesterDepartment,
    int CategoryId,
    string CategoryName,
    int PriorityId,
    string PriorityName,
    int StatusId,
    string StatusName,
    string AssignedAgentName,
    DateTime CreatedDate,
    DateTime UpdatedDate,
    DateTime? AssignedDate,
    DateTime? ResolvedDate);

public sealed record AgentDashboardDto(
    int ActiveAssignedTickets,
    int InProgress,
    int Pending,
    int HighPriorityOpen,
    int CriticalOpen,
    int ResolvedThisMonth,
    IReadOnlyCollection<AgentTicketListItemDto> PriorityAttentionTickets,
    IReadOnlyCollection<AgentTicketListItemDto> RecentAssignedTickets);

public sealed record AllowedStatusTransitionDto(int StatusId, string StatusName);

public sealed record TicketCommentDto(
    int Id, string AuthorName, string Content, DateTime CreatedDate,
    DateTime? UpdatedDate, bool IsEdited);

public sealed record TicketHistoryDto(
    int Id, string ActionType, string PerformedByName, string? OldValue,
    string? NewValue, string? Description, DateTime CreatedDate);

public sealed record AgentTicketDetailsDto(
    int Id,
    string TicketReferenceNumber,
    string Title,
    string Description,
    string RequesterName,
    string RequesterEmail,
    string? RequesterDepartment,
    int CategoryId,
    string CategoryName,
    int PriorityId,
    string PriorityName,
    int StatusId,
    string StatusName,
    DateTime CreatedDate,
    DateTime UpdatedDate,
    DateTime? AssignedDate,
    DateTime? ResolvedDate,
    DateTime? ClosedDate,
    string AssignedAgentName,
    IReadOnlyCollection<TicketAttachmentDto> Attachments,
    IReadOnlyCollection<TicketCommentDto> Comments,
    IReadOnlyCollection<TicketCommentDto> InternalNotes,
    IReadOnlyCollection<TicketHistoryDto> History,
    string? ResolutionSummary,
    IReadOnlyCollection<AllowedStatusTransitionDto> AllowedStatusTransitions,
    bool CanEdit,
    bool CanDelete,
    bool CanAssign,
    bool CanReassign,
    bool CanComment,
    bool CanAddInternalNote,
    bool CanChangeStatus,
    bool CanResolve);

public sealed class UpdateAgentTicketStatusRequestDto
{
    [Range(1, int.MaxValue)]
    public int StatusId { get; init; }
    [StringLength(500)]
    public string? Reason { get; init; }
}

public sealed class ResolveTicketRequestDto
{
    [Required, StringLength(5000, MinimumLength = 10)]
    public string ResolutionSummary { get; init; } = string.Empty;
}

public sealed class AddTicketCommentRequestDto
{
    [Required, StringLength(5000)]
    public string Content { get; init; } = string.Empty;
}
