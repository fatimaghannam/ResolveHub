namespace ResolveHub.Api.DTOs.Tickets;

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
    string Name,
    string Email,
    int ActiveAssigned,
    int InProgress,
    int Pending,
    string Capacity);

public sealed record AdminAssignmentOverviewDto(
    IReadOnlyCollection<AdminUnassignedTicketDto> UnassignedTickets,
    IReadOnlyCollection<AdminAgentWorkloadDto> AgentWorkloads);

public sealed record AssignTicketRequestDto(int AgentUserId);

public sealed record AdminDashboardSummaryDto(
    int TotalUsers,
    int TotalTickets,
    int OpenTickets,
    int InProgress,
    int UnassignedTickets,
    int ResolvedThisMonth,
    IReadOnlyCollection<AdminUnassignedTicketDto> TicketsRequiringAssignment,
    IReadOnlyCollection<AdminAgentWorkloadDto> AgentWorkloads);
