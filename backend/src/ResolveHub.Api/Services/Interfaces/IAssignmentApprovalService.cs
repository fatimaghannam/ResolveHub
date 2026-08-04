using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAssignmentApprovalService
{
    Task<TicketServiceResult<TicketAssignmentRequestDto>> CreateAsync(
        int managerId, string ticketReference, int agentUserId, CancellationToken token);
    Task<IReadOnlyCollection<TicketAssignmentRequestDto>> GetManagerRequestsAsync(
        int managerId, CancellationToken token);
    Task<IReadOnlyCollection<TicketAssignmentRequestDto>> GetPendingAdminRequestsAsync(
        CancellationToken token);
    Task<TicketServiceResult<bool>> ReviewAsync(
        int administratorId, int requestId, bool approve, string? reason,
        CancellationToken token);
}
