using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Tickets;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = RoleNames.Employee + "," + RoleNames.ITSupportAgent + "," +
    RoleNames.Manager + "," + RoleNames.Admin)]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserNotificationDto>>> Get(
        [FromQuery] int limit = 100, CancellationToken token = default) =>
        Ok(await service.GetAsync(UserId, limit, token));

    [HttpPatch("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId, CancellationToken token) =>
        await service.MarkReadAsync(UserId, notificationId, token) ? NoContent() : NotFound();

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken token)
    {
        await service.MarkAllReadAsync(UserId, token);
        return NoContent();
    }

    private int UserId
    {
        get
        {
            var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return int.TryParse(value, out var userId)
                ? userId
                : throw new InvalidOperationException(
                    "The authenticated user identifier is invalid.");
        }
    }
}
