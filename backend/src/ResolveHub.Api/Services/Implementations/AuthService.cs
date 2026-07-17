using Microsoft.AspNetCore.Identity;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<UserAccount> _userManager;
    private readonly SignInManager<UserAccount> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthService(
        UserManager<UserAccount> userManager,
        SignInManager<UserAccount> signInManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    public async Task<LoginServiceResult> LoginAsync(
        LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email.Trim();

        var user =
            await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return new LoginServiceResult(
                LoginStatus.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return new LoginServiceResult(
                LoginStatus.Inactive);
        }

        var signInResult =
            await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            var lockoutEndUtc =
                await _userManager.GetLockoutEndDateAsync(user);

            return new LoginServiceResult(
                LoginStatus.LockedOut,
                LockoutEndUtc: lockoutEndUtc);
        }

        if (!signInResult.Succeeded)
        {
            return new LoginServiceResult(
                LoginStatus.InvalidCredentials);
        }

        var roles =
            await _userManager.GetRolesAsync(user);

        if (roles.Count == 0)
        {
            return new LoginServiceResult(
                LoginStatus.MissingRole);
        }

        var roleCollection = roles.ToArray();

        var accessToken =
            _tokenService.CreateAccessToken(
                user,
                roleCollection);

        var expiresInSeconds =
            Math.Max(
                0,
                (int)Math.Ceiling(
                    (accessToken.ExpiresAtUtc -
                     DateTimeOffset.UtcNow).TotalSeconds));

        var response = new LoginResponse(
            AccessToken: accessToken.Token,
            TokenType: "Bearer",
            ExpiresAtUtc: accessToken.ExpiresAtUtc,
            ExpiresInSeconds: expiresInSeconds,
            User: new AuthenticatedUserResponse(
                ID: user.Id,
                Email: user.Email
                    ?? throw new InvalidOperationException(
                        "The authenticated user has no email."),
                FirstName: user.FirstName,
                LastName: user.LastName,
                Roles: roleCollection));

        return new LoginServiceResult(
            LoginStatus.Success,
            Response: response);
    }
}