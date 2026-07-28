using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAdminUserService
{
    Task<IReadOnlyCollection<AdminUserListItemDto>> GetUsersAsync(
        CancellationToken token);
    Task<TicketServiceResult<bool>> SetActiveAsync(
        int administratorId, int userId, bool isActive, CancellationToken token);
}
