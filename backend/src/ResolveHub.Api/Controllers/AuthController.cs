using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResolveHub.Api.Constants;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IPasswordResetService _passwordResetService;

    public AuthController(
        IAuthService authService,
        IPasswordResetService passwordResetService)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(SecurityPolicyNames.LoginRateLimit)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> LoginAsync(
        [FromBody] LoginRequest request)
    {
        PreventResponseCaching();

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
                        message =
                            "This account has been deactivated. Please contact your system administrator."
                    }),

            LoginStatus.MissingRole =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message = "Unable to complete authentication."
                    }),

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message = "Unable to complete authentication."
                    })
        };
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting(
        SecurityPolicyNames.ForgotPasswordRateLimit)]
    [ProducesResponseType(
        typeof(AuthMessageResponse),
        StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthMessageResponse>>
        ForgotPasswordAsync(
            [FromBody] ForgotPasswordRequest request,
            CancellationToken cancellationToken)
    {
        PreventResponseCaching();

        await _passwordResetService.RequestPasswordResetAsync(
            request,
            cancellationToken);

        return Accepted(
            new AuthMessageResponse(
                "If an eligible account exists for that email address, password reset instructions have been sent."));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [EnableRateLimiting(
        SecurityPolicyNames.ResetPasswordRateLimit)]
    [ProducesResponseType(
        typeof(AuthMessageResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthMessageResponse>>
        ResetPasswordAsync(
            [FromBody] ResetPasswordRequest request,
            CancellationToken cancellationToken)
    {
        PreventResponseCaching();

        var result =
            await _passwordResetService.ResetPasswordAsync(
                request,
                cancellationToken);

        return result.Status switch
        {
            PasswordResetStatus.Success =>
                Ok(
                    new AuthMessageResponse(
                        "Your password has been reset successfully. You can now sign in with your new password.")),

            PasswordResetStatus.PasswordPolicyFailure =>
                BadRequest(
                    new
                    {
                        message =
                            "The new password does not meet the password requirements.",
                        errors = result.Errors ?? []
                    }),

            _ =>
                BadRequest(
                    new AuthMessageResponse(
                        "The password reset link is invalid or has expired. Please request a new one."))
        };
    }

    private void PreventResponseCaching()
    {
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
    }
}
