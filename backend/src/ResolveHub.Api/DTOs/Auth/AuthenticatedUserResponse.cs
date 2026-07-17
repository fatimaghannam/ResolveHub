namespace ResolveHub.Api.DTOs.Auth;

public sealed record AuthenticatedUserResponse(
    int ID,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<string> Roles);