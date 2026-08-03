namespace ResolveHub.Api.DTOs.Tickets;

public sealed record TicketActivityDto(
    int Id, string ActivityType, string? Description,
    int PerformerId, string PerformerFullName, string PerformerRole,
    DateTime OccurredAt, string? OldValue, string? NewValue,
    int? WorkDurationMinutes, bool IsInternal);

public sealed record AgentWorkTimeDto(
    int AgentId, string AgentName, int WorkMinutes, string FormattedWorkTime);

public sealed record TicketActivitySummaryDto(
    int TicketId, string TicketReferenceNumber, string TicketTitle,
    string? Department, string Category, string Priority,
    int CreatorId, string CreatorFullName, string CreatorRole,
    string? CreatorDepartment, DateTime CreatedAt,
    string CurrentStatus, int? AssignedAgentId, string? AssignedAgentName,
    DateTime? FirstWorkStartedAt, DateTime? ResolvedAt, DateTime? ClosedAt,
    int TotalWorkMinutes, decimal TotalWorkHours, string FormattedTotalWorkTime,
    bool IsWorkSessionActive, DateTime? CurrentSessionStartedAt,
    int AssignmentCount, int StatusChangeCount, int PublicCommentCount,
    int? PrivateCommentCount, int AttachmentCount, int ReopenCount,
    IReadOnlyCollection<AgentWorkTimeDto> WorkTimeByAgent,
    int TotalActivities,
    int PendingPeriodCount,
    int TotalWorkSessions,
    int TotalPendingMinutes,
    string? CurrentPendingReason,
    DateTime? CurrentPendingSince,
    DateTime? LatestWorkResumedAt);
