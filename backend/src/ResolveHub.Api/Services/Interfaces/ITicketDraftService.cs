using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface ITicketDraftService
{
    Task<IReadOnlyCollection<TicketDraftDto>> GetAllAsync(int userId, CancellationToken token);
    Task<TicketDraftDto?> GetAsync(int userId, int id, CancellationToken token);
    Task<TicketServiceResult<TicketDraftDto>> CreateAsync(
        int userId, SaveTicketDraftRequestDto request, CancellationToken token);
    Task<TicketServiceResult<TicketDraftDto>> UpdateAsync(
        int userId, int id, SaveTicketDraftRequestDto request, CancellationToken token);
    Task<bool> DeleteAsync(int userId, int id, CancellationToken token);
    Task<TicketServiceResult<TicketDetailsDto>> SubmitAsync(
        int userId, int id, CancellationToken token);
}
