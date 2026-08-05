using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminUsersController(IAdminUserService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AdminUserListItemDto>>> All(
        [FromQuery] AdminUserFilterDto filter, CancellationToken token)
    {
        var result = await service.GetUsersAsync(filter, token);
        return result.Status == TicketOperationStatus.Success
            ? Ok(result.Value)
            : BadRequest(new { message = result.Message });
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<AdminUserDetailsDto>> Get(int userId, CancellationToken token)
    {
        var user = await service.GetUserAsync(userId, token);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyCollection<AdminDepartmentDto>>> Departments(
        CancellationToken token) => Ok(await service.GetDepartmentsAsync(token));

    [HttpPost]
    public async Task<ActionResult<CreateAdminUserResultDto>> Create(
        CreateAdminUserRequestDto request, CancellationToken token)
    {
        var result = await service.CreateUserAsync(GetUserId(), request, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => CreatedAtAction(nameof(Get),
                new { userId = result.Value!.User.Id }, result.Value),
            TicketOperationStatus.NotFound => BadRequest(new { message = result.Message }),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpPost("{userId:int}/resend-invitation")]
    public async Task<IActionResult> ResendInvitation(int userId, CancellationToken token)
    {
        var result = await service.ResendInvitationAsync(GetUserId(), userId, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => Ok(new { message = "Invitation sent successfully." }),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpPatch("{userId:int}/status")]
    public async Task<IActionResult> Status(
        int userId, UpdateUserStatusRequestDto request, CancellationToken token)
    {
        var result = await service.SetActiveAsync(
            GetUserId(), userId, request.IsActive, token);
        return result.Status switch
        {
            TicketOperationStatus.Success => NoContent(),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }

    private int GetUserId()
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out var userId) ? userId :
            throw new InvalidOperationException(
                "The authenticated user identifier is invalid.");
    }
}
