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
    int ActiveAssigned,
    int Open,
    int InProgress,
    int ResolvedThisMonth,
    int CriticalAssigned,
    string Capacity);

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
