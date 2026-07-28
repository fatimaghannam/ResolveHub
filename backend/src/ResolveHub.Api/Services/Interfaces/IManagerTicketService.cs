using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IManagerTicketService
{
    Task<ManagerDashboardDto> GetDashboardAsync(CancellationToken token);
    Task<PagedResultDto<AdminTicketListItemDto>> GetTicketsAsync(
        AdminTicketFilterDto filter, CancellationToken token);
    Task<AdminTicketDetailsDto?> GetTicketAsync(
        string ticketReference, CancellationToken token);
    Task<AdminAssignmentOverviewDto> GetAssignmentsAsync(CancellationToken token);
    Task<IReadOnlyCollection<ManagerAgentWorkloadDto>> GetWorkloadAsync(
        CancellationToken token);
    Task<ManagerActivityResultDto> GetActivityAsync(CancellationToken token);
    Task<TicketServiceResult<bool>> AssignAsync(
        int managerId, string ticketReference, int agentUserId,
        CancellationToken token);
}
