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
    DateTime CreatedDate, DateTime UpdatedDate);

public sealed record AdminTicketDetailsDto(
    int Id, string TicketReferenceNumber, string Title, string Description,
    int RequesterId, string RequesterName, string RequesterEmail,
    int CategoryId, string CategoryName,
    int PriorityId, string PriorityName,
    int StatusId, string StatusName,
    int? AssignedAgentId, string? AssignedAgentName,
    DateTime CreatedDate, DateTime UpdatedDate, DateTime? AssignedDate,
    DateTime? ResolvedDate,
    IReadOnlyCollection<TicketAttachmentDto> Attachments,
    IReadOnlyCollection<TicketCommentDto> Comments,
    IReadOnlyCollection<TicketHistoryDto> History);

public sealed record UpdateTicketAssignmentDto(int? AgentUserId);

public sealed class RemoveDuplicateTicketRequestDto
{
    [Required, StringLength(32)]
    public string OriginalTicketReference { get; init; } = string.Empty;

    public bool Confirmed { get; init; }
}

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
