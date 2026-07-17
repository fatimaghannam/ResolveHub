using ResolveHub.Api.DTOs.Auth;

namespace ResolveHub.Api.Services.Models;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    LockedOut,
    Inactive,
    MissingRole
}

public sealed record LoginServiceResult(
    LoginStatus Status,
    LoginResponse? Response = null,
    DateTimeOffset? LockoutEndUtc = null);