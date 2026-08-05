using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAdminCategoryService
{
    Task<TicketServiceResult<IReadOnlyCollection<AdminCategoryDto>>> GetAsync(
        AdminCategoryFilterDto filter, CancellationToken token);
    Task<TicketServiceResult<AdminCategoryDto>> CreateAsync(int administratorId,
        SaveAdminCategoryRequestDto request, CancellationToken token);
    Task<TicketServiceResult<AdminCategoryDto>> UpdateAsync(int administratorId,
        int categoryId, SaveAdminCategoryRequestDto request, CancellationToken token);
    Task<TicketServiceResult<AdminCategoryDto>> SetStatusAsync(int administratorId,
        int categoryId, bool isActive, CancellationToken token);
}
