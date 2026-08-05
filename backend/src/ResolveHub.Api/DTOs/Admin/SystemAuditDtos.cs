using System.ComponentModel.DataAnnotations;
namespace ResolveHub.Api.DTOs.Admin;

public sealed class SystemAuditFilterDto
{
    public string? Search { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtcExclusive { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

public sealed record SystemAuditRecordDto(
    int Id,
    DateTime CreatedAt,
    int PerformedByUserId,
    string PerformedByName,
    string PerformedByEmail,
    string PerformerRole,
    string Action,
    string ActionCategory,
    string EntityType,
    string EntityId,
    string EntityDisplayName,
    string Description,
    string Result,
    string? OldValue,
    string? NewValue,
    string? RelatedUrl,
    string? FailureReason);

public sealed record SystemAuditPageDto(
    IReadOnlyCollection<SystemAuditRecordDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
