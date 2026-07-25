using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Tickets;

public sealed class CancelTicketRequestDto
{
    [StringLength(500)]
    public string? Reason { get; init; }
}
