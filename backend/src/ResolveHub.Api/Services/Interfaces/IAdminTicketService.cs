using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAdminTicketService
{
    Task<AdminAssignmentOverviewDto> GetAssignmentsAsync(
        AdminTicketFilterDto filter, CancellationToken token);
    Task<AdminDashboardSummaryDto> GetDashboardAsync(CancellationToken token);
    Task<IReadOnlyCollection<AdminAgentWorkloadDto>> GetAgentsAsync(
        CancellationToken token);
    Task<PagedResultDto<AdminTicketListItemDto>> GetTicketsAsync(
        AdminTicketFilterDto filter, CancellationToken token);
    Task<AdminTicketDetailsDto?> GetTicketAsync(
        string ticketReference, CancellationToken token);
    Task<TicketServiceResult<bool>> AssignAsync(
        int administratorId,
        string ticketReference,
        int? agentUserId,
        CancellationToken token,
        int? preservedAgentRequestId = null);
    Task<IReadOnlyCollection<DuplicateReviewDto>> GetPendingDuplicateReviewsAsync(
        CancellationToken token);
    Task<TicketServiceResult<bool>> ReviewDuplicateAsync(
        int administratorId, int reviewId, bool approve,
        string? internalNote, CancellationToken token);
    Task<TicketServiceResult<bool>> MarkDuplicateAsync(
        int administratorId, string ticketReference,
        MarkDuplicateRequestDto request, CancellationToken token);
    Task<IReadOnlyCollection<UserNotificationDto>> GetNotificationsAsync(
        int userId, CancellationToken token);
    Task<bool> MarkNotificationReadAsync(
        int userId, int notificationId, CancellationToken token);
    Task MarkAllNotificationsReadAsync(int userId, CancellationToken token);
}
