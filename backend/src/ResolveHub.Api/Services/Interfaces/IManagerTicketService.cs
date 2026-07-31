using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IManagerTicketService
{
    Task<ManagerDashboardDto> GetDashboardAsync(CancellationToken token);
    Task<PagedResultDto<AdminTicketListItemDto>> GetTicketsAsync(
        AdminTicketFilterDto filter, CancellationToken token);
    Task<AdminTicketDetailsDto?> GetTicketAsync(
        string ticketReference, CancellationToken token);
    Task<AdminAssignmentOverviewDto> GetAssignmentsAsync(
        AdminTicketFilterDto filter, CancellationToken token);
    Task<IReadOnlyCollection<ManagerAgentWorkloadDto>> GetWorkloadAsync(
        CancellationToken token);
    Task<ManagerActivityResultDto> GetActivityAsync(CancellationToken token);
    Task<TicketServiceResult<bool>> AssignAsync(
        int managerId, string ticketReference, int agentUserId,
        CancellationToken token);
    Task<IReadOnlyCollection<TicketAssignmentRequestDto>> GetAssignmentRequestsAsync(
        CancellationToken token);
    Task<TicketServiceResult<bool>> ReviewAssignmentRequestAsync(
        int managerId, int requestId, bool approve, CancellationToken token);
    Task<TicketServiceResult<TicketCommentDto>> AddCommentAsync(
        int managerId, string ticketReference,
        AddTicketCommentRequestDto request, CancellationToken token);
    Task<TicketServiceResult<DuplicateReviewDto>> ReportDuplicateAsync(
        int managerId, string ticketReference,
        CreateDuplicateReviewRequestDto request, CancellationToken token);
    Task<IReadOnlyCollection<UserNotificationDto>> GetNotificationsAsync(
        int userId, CancellationToken token);
    Task<bool> MarkNotificationReadAsync(
        int userId, int notificationId, CancellationToken token);
    Task MarkAllNotificationsReadAsync(int userId, CancellationToken token);
}
