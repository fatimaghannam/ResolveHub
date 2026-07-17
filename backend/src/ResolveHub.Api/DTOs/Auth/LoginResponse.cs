namespace ResolveHub.Api.DTOs.Auth;

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    int ExpiresInSeconds,
    AuthenticatedUserResponse User);
    