using ResolveHub.Api.DTOs.Auth;

namespace ResolveHub.Api.Services.Models;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    LockedOut,
    Inactive,
    PendingSetup,
    MissingRole
}

public sealed record LoginServiceResult(
    LoginStatus Status,
    LoginResponse? Response = null,
    DateTimeOffset? LockoutEndUtc = null);
