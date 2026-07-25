using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Tickets;

public sealed class UpdateTicketRequestDto
{
    [Required, StringLength(200, MinimumLength = 5)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(5000, MinimumLength = 10)]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TicketCategoryId { get; init; }

    [Range(1, int.MaxValue)]
    public int TicketPriorityId { get; init; }
}
