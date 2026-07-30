namespace ResolveHub.Api.DTOs.Tickets;

public sealed record ManagerPriorityCountDto(string Name, int Value);

public sealed record ManagerActivityDto(
    int Id,
    string ActionType,
    string TicketReferenceNumber,
    string TicketTitle,
    string ActorName,
    string Description,
    DateTime CreatedDate);

public sealed record ManagerAgentWorkloadDto(
    int UserId,
    string Name,
    string Email,
    int ActiveTicketCount,
    int Assigned,
    int InProgress,
    int Pending,
    int ResolvedThisMonth,
    int CriticalAssigned,
    int MaxActiveTickets,
    int RemainingCapacity,
    string CapacityState,
    bool IsAtCapacity);

public sealed record ManagerDashboardDto(
    int TotalTickets,
    int OpenTickets,
    int InProgressTickets,
    int UnassignedTickets,
    int ResolvedThisMonth,
    int CriticalTickets,
    IReadOnlyCollection<AdminChartItemDto> TicketCountsByStatus,
    IReadOnlyCollection<ManagerPriorityCountDto> TicketCountsByPriority,
    IReadOnlyCollection<AdminUnassignedTicketDto> Unassigned,
    IReadOnlyCollection<ManagerAgentWorkloadDto> AgentWorkloads,
    IReadOnlyCollection<ManagerActivityDto> RecentActivity,
    IReadOnlyCollection<AdminTicketListItemDto> TicketsRequiringAttention);

public sealed record ManagerActivityResultDto(
    IReadOnlyCollection<ManagerActivityDto> Items);
