using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.AI;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController, Route("api/ai"), Authorize, EnableRateLimiting(SecurityPolicyNames.AiRateLimit)]
public sealed class AiAssistantController(IAiAssistantService service, ILogger<AiAssistantController> logger) : ControllerBase
{
    [HttpPost("tickets/analyze")] public Task<ActionResult<TicketAnalysisResponse>> Analyze(AnalyzeTicketRequest request, CancellationToken token) => Execute(() => service.AnalyzeAsync(request, token));
    [HttpPost("tickets/{ticketId:int}/summary")] public Task<ActionResult<TicketSummaryResponse>> Summary(int ticketId, CancellationToken token) => ExecuteResult(() => service.SummarizeAsync(UserId(), Role(), ticketId, token));
    [HttpPost("tickets/{ticketId:int}/troubleshooting")] public Task<ActionResult<TroubleshootingResponse>> Troubleshooting(int ticketId, CancellationToken token) => ExecuteResult(() => service.TroubleshootAsync(UserId(), Role(), ticketId, token));
    [HttpPost("chat")] public Task<ActionResult<AiChatResponse>> Chat(AiChatRequest request, CancellationToken token) => ExecuteResult(() => service.ChatAsync(UserId(), Role(), request, token));
    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action) { try { return Ok(await action()); } catch (AiProviderException ex) { logger.LogWarning(ex, "AI request failed and will return a safe service-unavailable response."); return StatusCode(503, new { message = "AI Assistant is temporarily unavailable. Please try again later." }); } }
    private async Task<ActionResult<T>> ExecuteResult<T>(Func<Task<TicketServiceResult<T>>> action) { try { var r = await action(); return r.Status switch { TicketOperationStatus.Success => Ok(r.Value), TicketOperationStatus.Forbidden => StatusCode(403, new { message = r.Message }), _ => NotFound() }; } catch (AiProviderException) { return StatusCode(503, new { message = "AI Assistant is temporarily unavailable. Please try again later." }); } }
    private int UserId() => int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    private string Role() => RoleNames.All.First(User.IsInRole);
}
