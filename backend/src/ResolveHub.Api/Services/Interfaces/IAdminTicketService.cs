using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAdminTicketService
{
    Task<AdminAssignmentOverviewDto> GetAssignmentsAsync(CancellationToken token);
    Task<AdminDashboardSummaryDto> GetDashboardAsync(CancellationToken token);
    Task<TicketServiceResult<bool>> AssignAsync(
        int administratorId,
        string ticketReference,
        int agentUserId,
        CancellationToken token);
}
