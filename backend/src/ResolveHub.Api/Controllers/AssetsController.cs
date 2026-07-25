using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Services.Interfaces;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize(Roles = RoleNames.Employee)]
public sealed class AssetsController(IAssetService assetService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(
        [FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await assetService.GetMineAsync(
            GetUserId(), search, cancellationToken));

    private int GetUserId() =>
        int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}
