using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Tickets;

public sealed class SaveTicketDraftRequestDto
{
    [StringLength(200)]
    public string? Title { get; init; }
    [StringLength(5000)]
    public string? Description { get; init; }
    public int? TicketCategoryId { get; init; }
    public int? TicketPriorityId { get; init; }
    public int? AssetId { get; init; }
}

public sealed record TicketDraftDto(
    int Id,
    string? Title,
    string? Description,
    int? TicketCategoryId,
    int? TicketPriorityId,
    int? AssetId,
    DateTime CreatedDate,
    DateTime UpdatedDate);
