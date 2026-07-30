using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/ticket-drafts")]
[Authorize(Roles = RoleNames.Employee + "," + RoleNames.Admin)]
public sealed class TicketDraftsController(ITicketDraftService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<TicketDraftDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> All(CancellationToken token) =>
        Ok(await service.GetAllAsync(UserId, token));
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken token)
    {
        var draft = await service.GetAsync(UserId, id, token);
        return draft is null ? NotFound() : Ok(draft);
    }
    [HttpPost]
    public async Task<IActionResult> Create(
        SaveTicketDraftRequestDto request, CancellationToken token)
    {
        var result = await service.CreateAsync(UserId, request, token);
        return result.Status == TicketOperationStatus.Success
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new { message = result.Message });
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id, SaveTicketDraftRequestDto request, CancellationToken token)
    {
        var result = await service.UpdateAsync(UserId, id, request, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            _ => BadRequest(new { message = result.Message })
        };
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken token) =>
        await service.DeleteAsync(UserId, id, token) ? NoContent() : NotFound();
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> Submit(int id, CancellationToken token)
    {
        var result = await service.SubmitAsync(UserId, id, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            _ => BadRequest(new { message = result.Message })
        };
    }
    private int UserId
    {
        get
        {
            var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(value, out var userId))
                return userId;

            throw new InvalidOperationException(
                "The authenticated user identifier is invalid.");
        }
    }
}
