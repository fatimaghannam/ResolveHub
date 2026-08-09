using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.DTOs.Profile;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController(IProfilePhotoService service) : ControllerBase
{
    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ProfilePhotoResponse>> UploadPhoto(
        IFormFile photo, CancellationToken cancellationToken)
    {
        var result = await service.UploadAsync(GetUserId(), photo, cancellationToken);
        return result.Status switch
        {
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            _ => BadRequest(new { message = result.Message })
        };
    }

    [HttpDelete("photo")]
    public async Task<ActionResult<ProfilePhotoResponse>> RemovePhoto(
        CancellationToken cancellationToken)
    {
        var result = await service.RemoveAsync(GetUserId(), cancellationToken);
        return result.Status == TicketOperationStatus.Success
            ? Ok(result.Value)
            : NotFound();
    }

    private int GetUserId()
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out var userId) ? userId :
            throw new InvalidOperationException(
                "The authenticated user identifier is invalid.");
    }
}
