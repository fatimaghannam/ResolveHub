namespace ResolveHub.Api.DTOs.Tickets;

using System.ComponentModel.DataAnnotations;
using ResolveHub.Api.DTOs.Common;

public sealed record AdminUnassignedTicketDto(
    int Id,
    string TicketReferenceNumber,
    string Title,
    string RequesterName,
    string CategoryName,
    string PriorityName,
    DateTime CreatedDate);

public sealed record AdminAgentWorkloadDto(
    int UserId,
    string FirstName,
    string LastName,
    string Name,
    string Email,
    int ActiveTicketCount,
    int Assigned,
    int InProgress,
    int Pending,
    int MaxActiveTickets,
    int RemainingCapacity,
    string CapacityState,
    bool IsAtCapacity);

public sealed record AdminAssignmentOverviewDto(
    IReadOnlyCollection<AdminUnassignedTicketDto> UnassignedTickets,
    IReadOnlyCollection<AdminAgentWorkloadDto> AgentWorkloads);

public sealed record AssignTicketRequestDto(int AgentUserId);

public sealed class AdminTicketFilterDto
{
    [StringLength(200)] public string? Search { get; init; }
    public int? StatusId { get; init; }
    public int? CategoryId { get; init; }
    public int? PriorityId { get; init; }
    public int? AgentUserId { get; init; }
    public int? RequesterId { get; init; }
    public bool? UnassignedOnly { get; init; }
    public bool? AssignedOnly { get; init; }
    public bool? ActiveWorkloadOnly { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtcExclusive { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 10;
    public string? SortBy { get; init; } = "createdDate";
    public string? SortDirection { get; init; } = "desc";
}

public sealed record AdminTicketListItemDto(
    int Id, string TicketReferenceNumber, string Title,
    int RequesterId, string RequesterName,
    int CategoryId, string CategoryName,
    int PriorityId, string PriorityName,
    int StatusId, string StatusName,
    int? AssignedAgentId, string? AssignedAgentName,
    DateTime CreatedDate, DateTime UpdatedDate,
    string? OriginalTicketReference);

public sealed record AdminTicketDetailsDto(
    int Id, string TicketReferenceNumber, string Title, string Description,
    int RequesterId, string RequesterName, string RequesterEmail,
    int CategoryId, string CategoryName,
    int PriorityId, string PriorityName,
    int StatusId, string StatusName,
    int? AssignedAgentId, string? AssignedAgentName,
    DateTime CreatedDate, DateTime UpdatedDate, DateTime? AssignedDate,
    DateTime? ResolvedDate, DateTime? ClosedDate,
    IReadOnlyCollection<TicketAttachmentDto> Attachments,
    IReadOnlyCollection<TicketCommentDto> Comments,
    IReadOnlyCollection<TicketHistoryDto> History,
    int? OriginalTicketId,
    string? OriginalTicketReference,
    string? OriginalTicketTitle,
    DateTime? DuplicateApprovedDate,
    string? DuplicateApprovedByName,
    DuplicateReviewDto? PendingDuplicateReview);

public sealed record UpdateTicketAssignmentDto(int? AgentUserId);

public sealed class CreateDuplicateReviewRequestDto
{
    [Required, StringLength(32)]
    public string SuggestedOriginalTicketReference { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Reason { get; init; }
}

public sealed class ReviewDuplicateRequestDto
{
    [StringLength(1000)]
    public string? InternalNote { get; init; }
}

public sealed class MarkDuplicateRequestDto
{
    [Required, StringLength(32)]
    public string OriginalTicketReference { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Reason { get; init; }

    public bool Confirmed { get; init; }
}

public sealed record DuplicateReviewDto(
    int Id,
    string ReportedTicketReference,
    string ReportedTicketTitle,
    string ReportedTicketStatus,
    string ReportedTicketPriority,
    DateTime ReportedTicketCreatedDate,
    string ReportedRequesterName,
    string ReportedCategoryName,
    string SuggestedOriginalTicketReference,
    string SuggestedOriginalTicketTitle,
    string SuggestedOriginalTicketStatus,
    string SuggestedOriginalTicketPriority,
    DateTime SuggestedOriginalTicketCreatedDate,
    string SuggestedOriginalRequesterName,
    string SuggestedOriginalCategoryName,
    string ReportedByName,
    bool ReportedByAdministrator,
    string? Reason,
    string Status,
    DateTime CreatedDate);

public sealed record UserNotificationDto(
    int Id, string Type, string Title, string Message,
    string? TicketReferenceNumber, bool IsRead, DateTime CreatedDate);

public sealed record AdminChartItemDto(string Name, int Value);
public sealed record AdminMonthlyTrendDto(string Month, int Created, int Resolved);

public sealed record AdminDashboardSummaryDto(
    int TotalUsers,
    int TotalTickets,
    int OpenTickets,
    int InProgress,
    int UnassignedTickets,
    int ResolvedThisMonth,
    IReadOnlyCollection<AdminChartItemDto> TicketCountsByStatus,
    IReadOnlyCollection<AdminMonthlyTrendDto> MonthlyTrend,
    IReadOnlyCollection<AdminChartItemDto> TicketsByCategory,
    IReadOnlyCollection<AdminUnassignedTicketDto> TicketsRequiringAssignment,
    IReadOnlyCollection<AdminAgentWorkloadDto> AgentWorkloads);
