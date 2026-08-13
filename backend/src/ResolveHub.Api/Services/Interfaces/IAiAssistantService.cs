using ResolveHub.Api.DTOs.AI;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IAiAssistantService
{
    Task<TicketAnalysisResponse> AnalyzeAsync(AnalyzeTicketRequest request, CancellationToken token);
    Task<TicketServiceResult<TicketSummaryResponse>> SummarizeAsync(int userId, string role, int ticketId, CancellationToken token);
    Task<TicketServiceResult<TroubleshootingResponse>> TroubleshootAsync(int userId, string role, int ticketId, CancellationToken token);
    Task<TicketServiceResult<AiChatResponse>> ChatAsync(int userId, string role, AiChatRequest request, CancellationToken token);
}

public sealed class AiProviderException(string message, Exception? inner = null) : Exception(message, inner);
