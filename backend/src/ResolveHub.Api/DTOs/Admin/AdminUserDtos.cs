using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Admin;

public sealed record AdminUserListItemDto(
    int Id, string FirstName, string LastName, string Email,
    string Role, string? Department, string Status, DateTime CreatedDate);

public sealed record AdminUserDetailsDto(
    int Id, string FirstName, string LastName, string Email,
    string Role, string? Department, string Status, DateTime CreatedDate,
    DateTime? LastLoginDate, string? ProfileImagePath);

public sealed record CreateAdminUserResultDto(
    AdminUserDetailsDto User, bool InvitationSent);

public sealed record AdminDepartmentDto(int Id, string Name);

public sealed record AdminUserFilterDto(
    string? Search, string? Role, string? Status,
    int? DepartmentId, bool UnassignedDepartment = false);

public sealed class CreateAdminUserRequestDto
{
    [Required, StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; init; } = string.Empty;

    public int? DepartmentId { get; init; }

    [Required]
    public string Role { get; init; } = string.Empty;
}

public sealed record UpdateUserStatusRequestDto(bool IsActive);
