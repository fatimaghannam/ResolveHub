using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Admin;

public sealed class AdminCategoryFilterDto
{
    [StringLength(200)] public string? Search { get; init; }
    public string? Status { get; init; }
}

public sealed record AdminCategoryDto(
    int Id, string Name, string Description, int ActiveTickets, bool IsActive);

public sealed class SaveAdminCategoryRequestDto
{
    [Required, StringLength(100)] public string Name { get; init; } = string.Empty;
    [Required, StringLength(500)] public string Description { get; init; } = string.Empty;
}

public sealed record SetAdminCategoryStatusRequestDto(bool IsActive);
