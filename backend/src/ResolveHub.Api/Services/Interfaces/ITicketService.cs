using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface ITicketService
{
    Task<TicketDashboardSummaryDto> GetDashboardAsync(
        int userId, CancellationToken cancellationToken);
    Task<PagedResultDto<TicketListItemDto>> GetTicketsAsync(
        int userId, TicketFilterDto filter, CancellationToken cancellationToken);
    Task<TicketDetailsDto?> GetTicketAsync(
        int userId, int ticketId, CancellationToken cancellationToken);
    Task<TicketServiceResult<TicketDetailsDto>> CreateTicketAsync(
        int userId, CreateTicketRequestDto request, CancellationToken cancellationToken);
    Task<TicketServiceResult<TicketDetailsDto>> UpdateTicketAsync(
        int userId, int ticketId, UpdateTicketRequestDto request,
        CancellationToken cancellationToken);
    Task<TicketServiceResult<bool>> CancelTicketAsync(
        int userId, int ticketId, CancelTicketRequestDto request,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TicketLookupDto>> GetCategoriesAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TicketLookupDto>> GetPrioritiesAsync(
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TicketLookupDto>> GetStatusesAsync(
        CancellationToken cancellationToken);
}
