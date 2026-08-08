using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface ITicketCancellationRequestService
{
    Task<TicketServiceResult<TicketCancellationRequestDto>> CreateAsync(
        int agentId, string ticketReference, string reason, CancellationToken token);
    Task<IReadOnlyCollection<TicketCancellationRequestDto>> GetManagerRequestsAsync(
        CancellationToken token);
    Task<TicketServiceResult<bool>> ReviewAsync(int managerId, int requestId,
        string decision, string? reviewNote, CancellationToken token);
}
