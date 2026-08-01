namespace ResolveHub.Api.Services.Models;

public enum TicketOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict,
    Invalid
}

public sealed record TicketServiceResult<T>(
    TicketOperationStatus Status,
    T? Value = default,
    string? Message = null);
