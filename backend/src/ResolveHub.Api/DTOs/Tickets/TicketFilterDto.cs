using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Tickets;

public sealed class TicketFilterDto : IValidatableObject
{
    [StringLength(200)]
    public string? Search { get; init; }
    public int? StatusId { get; init; }
    public int? CategoryId { get; init; }
    public int? PriorityId { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtcExclusive { get; init; }
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;
    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
    public string? SortBy { get; init; } = "createdDate";
    public string? SortDirection { get; init; } = "desc";

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (FromUtc.HasValue != ToUtcExclusive.HasValue)
        {
            yield return new ValidationResult(
                "Both fromUtc and toUtcExclusive must be provided together.",
                [nameof(FromUtc), nameof(ToUtcExclusive)]);
        }
        else if (FromUtc.HasValue &&
                 ToUtcExclusive!.Value <= FromUtc.Value)
        {
            yield return new ValidationResult(
                "toUtcExclusive must be later than fromUtc.",
                [nameof(ToUtcExclusive)]);
        }
    }
}
