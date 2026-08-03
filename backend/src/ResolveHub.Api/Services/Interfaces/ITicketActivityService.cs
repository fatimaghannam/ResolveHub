using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface ITicketActivityService
{
    Task<TicketServiceResult<IReadOnlyCollection<TicketActivityDto>>> GetTimelineAsync(
        int userId, string ticketReference, bool descending, CancellationToken token);
    Task<TicketServiceResult<TicketActivitySummaryDto>> GetSummaryAsync(
        int userId, string ticketReference, CancellationToken token);
}
