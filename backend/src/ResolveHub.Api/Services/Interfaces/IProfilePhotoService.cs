using ResolveHub.Api.DTOs.Profile;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IProfilePhotoService
{
    Task<TicketServiceResult<ProfilePhotoResponse>> UploadAsync(
        int userId, IFormFile photo, CancellationToken cancellationToken);

    Task<TicketServiceResult<ProfilePhotoResponse>> RemoveAsync(
        int userId, CancellationToken cancellationToken);
}
