using ResolveHub.Api.DTOs.Common;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAgentTicketService
{
    Task<AgentDashboardDto> GetDashboardAsync(int agentId, CancellationToken token);
    Task<PagedResultDto<AgentTicketListItemDto>> GetTicketsAsync(
        int agentId, AgentTicketFilterDto filter, CancellationToken token);
    Task<PagedResultDto<AgentTicketListItemDto>> GetOpenTicketsAsync(
        int agentId, AgentTicketFilterDto filter, CancellationToken token);
    Task<PagedResultDto<AgentTicketListItemDto>> GetHistoryTicketsAsync(
        int agentId, AgentTicketFilterDto filter, CancellationToken token);
    Task<AgentTicketDetailsDto?> GetTicketAsync(
        int agentId, string ticketReference, CancellationToken token);
    Task<TicketServiceResult<AgentTicketDetailsDto>> UpdateStatusAsync(
        int agentId, string ticketReference,
        UpdateAgentTicketStatusRequestDto request, CancellationToken token);
    Task<TicketServiceResult<AgentTicketWorkflowResultDto>> MarkPendingAsync(
        int agentId, string ticketReference,
        MarkTicketPendingRequestDto request, CancellationToken token);
    Task<TicketServiceResult<AgentTicketWorkflowResultDto>> ResumeWorkAsync(
        int agentId, string ticketReference, CancellationToken token);
    Task<TicketServiceResult<AgentTicketDetailsDto>> ResolveAsync(
        int agentId, string ticketReference,
        ResolveTicketRequestDto request, CancellationToken token);
    Task<TicketServiceResult<AgentTicketDetailsDto>> CloseAsync(
        int agentId, string ticketReference,
        CloseTicketRequestDto request, CancellationToken token);
    Task<IReadOnlyCollection<TicketCommentDto>?> GetCommentsAsync(
        int agentId, string ticketReference, CancellationToken token);
    Task<TicketServiceResult<TicketCommentDto>> AddCommentAsync(
        int agentId, string ticketReference, AddTicketCommentRequestDto request,
        CancellationToken token);
    Task<IReadOnlyCollection<TicketHistoryDto>?> GetHistoryAsync(
        int agentId, string ticketReference, CancellationToken token);
    Task<IReadOnlyCollection<TicketCommentDto>?> GetEmployeeCommentsAsync(
        int employeeId, int ticketId, CancellationToken token);
    Task<TicketServiceResult<TicketCommentDto>> AddEmployeeCommentAsync(
        int employeeId, int ticketId, AddTicketCommentRequestDto request,
        CancellationToken token);
    Task<TicketServiceResult<TicketAssignmentRequestDto>> RequestAssignmentAsync(
        int agentId, string ticketReference, CancellationToken token);
}
