namespace ResolveHub.Api.Services.Models;

public enum PasswordResetStatus
{
    Success,
    InvalidToken,
    PasswordPolicyFailure
}

public sealed record PasswordResetServiceResult(
    PasswordResetStatus Status,
    IReadOnlyCollection<string>? Errors = null);
