using System.ComponentModel.DataAnnotations;
using ResolveHub.Api.Constants;

namespace ResolveHub.Api.DTOs.Tickets;

public sealed class AgentTicketFilterDto : IValidatableObject
{
    [StringLength(200)]
    public string? Search { get; init; }
    public int? StatusId { get; init; }
    [StringLength(20)]
    public string? Scope { get; init; }
    public int? CategoryId { get; init; }
    public int? PriorityId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtcExclusive { get; init; }
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
        if (FromUtc.HasValue != ToUtcExclusive.HasValue)
            yield return new("Both fromUtc and toUtcExclusive must be provided together.",
                [nameof(FromUtc), nameof(ToUtcExclusive)]);
        else if (FromUtc.HasValue &&
                 ToUtcExclusive!.Value <= FromUtc.Value)
            yield return new("toUtcExclusive must be later than fromUtc.",
                [nameof(ToUtcExclusive)]);
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
    string? AssignedAgentName,
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
    int Id, string AuthorName, string AuthorRole, string Content, DateTime CreatedDate,
    DateTime? UpdatedDate, bool IsEdited, string Visibility,
    int? ParentCommentId = null, bool IsDeleted = false,
    DateTime? DeletedDate = null, bool CanEdit = false, bool CanDelete = false,
    bool IsTicketCreator = false, bool IsAssignedAgent = false,
    int ReplyCount = 0, bool HasMoreReplies = false,
    IReadOnlyCollection<CommentAttachmentDto>? Attachments = null);

public sealed record CommentAttachmentDto(
    int Id, string FileName, string ContentType, long FileSizeBytes,
    DateTime UploadedDate);

public sealed record TicketCommentPageDto(
    IReadOnlyCollection<TicketCommentDto> Items,
    int Page,
    int PageSize,
    int TotalThreads,
    int TotalVisibleComments,
    int PublicCount,
    int PrivateCount,
    bool HasMore);

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
    string? AssignedAgentName,
    IReadOnlyCollection<TicketAttachmentDto> Attachments,
    IReadOnlyCollection<TicketCommentDto> Comments,
    IReadOnlyCollection<TicketHistoryDto> History,
    string? ResolutionSummary,
    IReadOnlyCollection<AllowedStatusTransitionDto> AllowedStatusTransitions,
    bool CanEdit,
    bool CanDelete,
    bool CanAssign,
    bool CanReassign,
    bool CanComment,
    bool CanChangeStatus,
    bool CanResolve,
    bool CanClose,
    bool CanRequestAssignment,
    string? AssignmentRequestStatus,
    string? OriginalTicketReference,
    string? OriginalTicketTitle)
{
    public CurrentTicketPendingDto? CurrentPending { get; init; }
    public TicketCancellationRequestDto? PendingCancellationRequest { get; init; }
}

public sealed record TicketCancellationRequestDto(
    int Id, int TicketId, string TicketReferenceNumber, string TicketTitle,
    int RequestedByAgentUserAccountId, string RequestedByAgentName,
    string CurrentTicketStatus, string Reason, string Status, DateTime RequestedDate,
    int? ReviewedByManagerUserAccountId, string? ReviewedByManagerName,
    DateTime? ReviewedDate, string? ReviewNote, string? Outcome);

public sealed class CreateTicketCancellationRequestDto
{
    [Required, StringLength(1000)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class ReviewTicketCancellationRequestDto
{
    [StringLength(500)]
    public string? ReviewNote { get; init; }
}

public sealed record CurrentTicketPendingDto(
    int Id, string ReasonCode, string ReasonText, string? AdditionalNote,
    int SetByUserAccountId, string SetByName, DateTime PendingSince);

public sealed record AgentTicketWorkflowResultDto(
    AgentTicketDetailsDto Ticket, TicketActivitySummaryDto ActivitySummary);

public sealed class MarkTicketPendingRequestDto
{
    [Required, StringLength(50)]
    public string ReasonCode { get; init; } = string.Empty;

    [StringLength(300)]
    public string? CustomReason { get; init; }

    [StringLength(1000)]
    public string? AdditionalNote { get; init; }
}

public sealed record TicketAssignmentRequestDto(
    int Id,
    int TicketId,
    string TicketReferenceNumber,
    string TicketTitle,
    int RequestedByUserAccountId,
    string RequestedByName,
    int? RequestedAgentUserAccountId,
    string? RequestedAgentName,
    int RequestedAgentActiveTicketCount,
    int RequestedAgentMaxActiveTickets,
    string Status,
    DateTime RequestedDate,
    int? ReviewedByUserAccountId,
    string? ReviewedByName,
    DateTime? ReviewedDate,
    string? ReviewReason);

public sealed class ReviewAssignmentRequestDto
{
    [StringLength(500)]
    public string? Reason { get; init; }
}

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

public sealed class CloseTicketRequestDto
{
    [StringLength(500)]
    public string? ClosingNote { get; init; }
}

public sealed class AddTicketCommentRequestDto
{
    [Required, StringLength(TicketCommentRules.MaximumMessageLength)]
    public string Message { get; init; } = string.Empty;

    public string? Visibility { get; init; }
}

public sealed class CreateTicketCommentFormRequest
{
    [Required, StringLength(TicketCommentRules.MaximumMessageLength)]
    public string Content { get; init; } = string.Empty;

    public string? Visibility { get; init; }

    public int? ParentCommentId { get; init; }

    public List<IFormFile> Attachments { get; init; } = [];
}

public sealed class EditTicketCommentRequestDto
{
    [Required, StringLength(TicketCommentRules.MaximumMessageLength)]
    public string Message { get; init; } = string.Empty;
}
