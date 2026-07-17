namespace ResolveHub.Api.Services.Models;

public sealed record AccessTokenResult(
    string Token,
    DateTimeOffset ExpiresAtUtc);