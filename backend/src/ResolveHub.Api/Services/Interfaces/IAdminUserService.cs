using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAdminUserService
{
    Task<IReadOnlyCollection<AdminUserListItemDto>> GetUsersAsync(
        CancellationToken token);
    Task<TicketServiceResult<IReadOnlyCollection<AdminUserListItemDto>>> GetUsersAsync(
        AdminUserFilterDto filter, CancellationToken token);
    Task<AdminUserDetailsDto?> GetUserAsync(int userId, CancellationToken token);
    Task<IReadOnlyCollection<AdminDepartmentDto>> GetDepartmentsAsync(CancellationToken token);
    Task<TicketServiceResult<CreateAdminUserResultDto>> CreateUserAsync(
        int administratorId, CreateAdminUserRequestDto request, CancellationToken token);
    Task<TicketServiceResult<bool>> ResendInvitationAsync(
        int administratorId, int userId, CancellationToken token);
    Task<TicketServiceResult<bool>> SetActiveAsync(
        int administratorId, int userId, bool isActive, CancellationToken token);
}
