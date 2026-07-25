using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AssetService(ApplicationDbContext dbContext) : IAssetService
{
    public async Task<IReadOnlyCollection<AssetLookupDto>> GetMineAsync(
        int userId, string? search, CancellationToken cancellationToken)
    {
        var departmentId = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DepartmentID)
            .SingleAsync(cancellationToken);
        var query = dbContext.Assets.AsNoTracking()
            .Where(asset => asset.IsActive &&
                (asset.AssignedToUserAccountID == userId ||
                 (departmentId != null &&
                  asset.AssignedToUserAccountID == null &&
                  asset.DepartmentID == departmentId)));
        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(asset =>
                asset.AssetName.Contains(term) ||
                asset.AssetTag.Contains(term) ||
                (asset.SerialNumber != null && asset.SerialNumber.Contains(term)) ||
                (asset.Location != null && asset.Location.Contains(term)));

        return await query.OrderBy(asset => asset.AssetTag)
            .Take(20)
            .Select(asset => new AssetLookupDto(
                asset.ID, asset.AssetTag, asset.AssetName, asset.AssetType,
                asset.SerialNumber, asset.Location))
            .ToListAsync(cancellationToken);
    }
}
