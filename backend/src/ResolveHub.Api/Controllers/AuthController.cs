using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> LoginAsync(
        [FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        return result.Status switch
        {
            LoginStatus.Success when result.Response is not null =>
                Ok(result.Response),

            LoginStatus.InvalidCredentials =>
                Unauthorized(new
                {
                    message = "Invalid email or password."
                }),

            LoginStatus.LockedOut =>
                StatusCode(
                    StatusCodes.Status423Locked,
                    new
                    {
                        message =
                            "The account is temporarily locked due to repeated failed login attempts.",
                        lockoutEndUtc = result.LockoutEndUtc
                    }),

            LoginStatus.Inactive =>
                StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "This account is inactive."
                    }),

            LoginStatus.MissingRole =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "The account does not have an assigned role."
                    }),

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message =
                            "An unexpected authentication error occurred."
                    })
        };
    }
}