using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Tickets;

public sealed class TicketFilterDto
{
    [StringLength(200)]
    public string? Search { get; init; }
    public int? StatusId { get; init; }
    public int? CategoryId { get; init; }
    public int? PriorityId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;
    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
    public string? SortBy { get; init; } = "createdDate";
    public string? SortDirection { get; init; } = "desc";
}
