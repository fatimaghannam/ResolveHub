namespace ResolveHub.Api.DTOs.Admin;

public sealed record AdminUserListItemDto(
    int Id, string FirstName, string LastName, string Email,
    string Role, string? Department, string Status, DateTime CreatedDate);

public sealed record UpdateUserStatusRequestDto(bool IsActive);
