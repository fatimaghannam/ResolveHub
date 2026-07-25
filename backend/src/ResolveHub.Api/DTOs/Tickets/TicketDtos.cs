namespace ResolveHub.Api.DTOs.Tickets;

public sealed record TicketListItemDto(
    int Id,
    string TicketReferenceNumber,
    string Title,
    string CategoryName,
    string PriorityName,
    string StatusName,
    string? AssignedToName,
    DateTime CreatedDate,
    bool CanEdit,
    bool CanDelete);

public sealed record TicketDetailsDto(
    int Id,
    string TicketReferenceNumber,
    string Title,
    string Description,
    int TicketCategoryId,
    string CategoryName,
    int TicketPriorityId,
    string PriorityName,
    int TicketStatusId,
    string StatusName,
    string CreatedByName,
    string? AssignedToName,
    DateTime CreatedDate,
    DateTime UpdatedDate,
    DateTime? AssignedDate,
    DateTime? ResolvedDate,
    DateTime? ClosedDate,
    DateTime? CancelledDate,
    string? CancelledReason,
    bool CanEdit,
    bool CanDelete);

public sealed record TicketDashboardSummaryDto(
    int TotalTickets,
    int OpenTickets,
    int InProgressTickets,
    int ResolvedTickets,
    IReadOnlyCollection<TicketListItemDto> RecentTickets);

public sealed record TicketLookupDto(int Id, string Name);
