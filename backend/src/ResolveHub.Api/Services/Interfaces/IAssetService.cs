using ResolveHub.Api.DTOs.Tickets;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAssetService
{
    Task<IReadOnlyCollection<AssetLookupDto>> GetMineAsync(
        int userId, string? search, CancellationToken cancellationToken);
}
