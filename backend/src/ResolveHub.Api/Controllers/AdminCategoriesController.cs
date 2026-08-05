using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Admin;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class AdminCategoriesController(IAdminCategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AdminCategoryFilterDto filter,
        CancellationToken token) => ToResult(await service.GetAsync(filter, token));

    [HttpPost]
    public async Task<IActionResult> Create(SaveAdminCategoryRequestDto request,
        CancellationToken token) => ToResult(await service.CreateAsync(UserId(), request, token), true);

    [HttpPut("{categoryId:int}")]
    public async Task<IActionResult> Update(int categoryId,
        SaveAdminCategoryRequestDto request, CancellationToken token) =>
        ToResult(await service.UpdateAsync(UserId(), categoryId, request, token));

    [HttpPatch("{categoryId:int}/status")]
    public async Task<IActionResult> Status(int categoryId,
        SetAdminCategoryStatusRequestDto request, CancellationToken token) =>
        ToResult(await service.SetStatusAsync(UserId(), categoryId, request.IsActive, token));

    private IActionResult ToResult<T>(TicketServiceResult<T> result, bool created = false) =>
        result.Status switch
        {
            TicketOperationStatus.Success when created => StatusCode(StatusCodes.Status201Created, result.Value),
            TicketOperationStatus.Success => Ok(result.Value),
            TicketOperationStatus.NotFound => NotFound(),
            TicketOperationStatus.Conflict => Conflict(new { message = result.Message }),
            TicketOperationStatus.Forbidden => Forbid(),
            _ => BadRequest(new { message = result.Message })
        };

    private int UserId() => int.TryParse(
        User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
        ? userId : throw new InvalidOperationException("Invalid authenticated user identifier.");
}
