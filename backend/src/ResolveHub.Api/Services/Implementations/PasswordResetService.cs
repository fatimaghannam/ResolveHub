using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;
using ResolveHub.Api.Settings;

namespace ResolveHub.Api.Services.Implementations;

public sealed class PasswordResetService
    : IPasswordResetService
{
    private static readonly HashSet<string> PasswordPolicyErrorCodes =
    [
        nameof(IdentityErrorDescriber.PasswordTooShort),
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric),
        nameof(IdentityErrorDescriber.PasswordRequiresDigit),
        nameof(IdentityErrorDescriber.PasswordRequiresLower),
        nameof(IdentityErrorDescriber.PasswordRequiresUpper),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars)
    ];

    private readonly UserManager<UserAccount> _userManager;
    private readonly IPasswordResetEmailSender _emailSender;
    private readonly FrontendSettings _frontendSettings;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        UserManager<UserAccount> userManager,
        IPasswordResetEmailSender emailSender,
        IOptions<FrontendSettings> frontendOptions,
        ILogger<PasswordResetService> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _frontendSettings = frontendOptions.Value;
        _logger = logger;
    }

    public async Task RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed ||
            !await _userManager.HasPasswordAsync(user) ||
            string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogInformation(
                "Password-reset request accepted.");
            return;
        }

        var token =
            await _userManager
                .GeneratePasswordResetTokenAsync(user);

        var encodedToken = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(token));

        var resetUrl = BuildResetUrl(
            user.Email,
            encodedToken);

        _logger.LogInformation(
            "Password-reset email dispatch attempted for user {UserId}.",
            user.Id);

        try
        {
            await _emailSender.SendPasswordResetEmailAsync(
                user.Email,
                $"{user.FirstName} {user.LastName}".Trim(),
                resetUrl,
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Password-reset email dispatch failed for user {UserId}.",
                user.Id);
        }

        _logger.LogInformation(
            "Password-reset request accepted.");
    }

    public async Task<PasswordResetServiceResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null ||
            !user.IsActive ||
            !user.EmailConfirmed)
        {
            _logger.LogWarning(
                "Password reset rejected because the link was invalid or expired.");

            return InvalidTokenResult();
        }

        string decodedToken;

        try
        {
            decodedToken = Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            _logger.LogWarning(
                "Password reset rejected because the link was malformed.");

            return InvalidTokenResult();
        }

        var resetResult =
            await _userManager.ResetPasswordAsync(
                user,
                decodedToken,
                request.NewPassword);

        if (!resetResult.Succeeded)
        {
            var passwordPolicyErrors =
                resetResult.Errors
                    .Where(error =>
                        PasswordPolicyErrorCodes.Contains(error.Code))
                    .Select(error => error.Description)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

            if (passwordPolicyErrors.Length > 0)
            {
                return new PasswordResetServiceResult(
                    PasswordResetStatus.PasswordPolicyFailure,
                    passwordPolicyErrors);
            }

            _logger.LogWarning(
                "Password reset rejected for user {UserId} because the link was invalid or expired.",
                user.Id);

            return InvalidTokenResult();
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.UpdatedDate = DateTime.UtcNow;

        var updateResult =
            await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(
                "The password was reset but the account security state could not be updated.");
        }

        _logger.LogInformation(
            "Password reset succeeded for user {UserId}.",
            user.Id);

        return new PasswordResetServiceResult(
            PasswordResetStatus.Success);
    }

    private string BuildResetUrl(
        string email,
        string encodedToken)
    {
        var baseUri = new Uri(
            _frontendSettings.BaseUrl,
            UriKind.Absolute);

        var resetUri = new Uri(
            baseUri,
            "/reset-password");

        return QueryHelpers.AddQueryString(
            resetUri.ToString(),
            new Dictionary<string, string?>
            {
                ["email"] = email,
                ["token"] = encodedToken
            });
    }

    private static PasswordResetServiceResult InvalidTokenResult()
    {
        return new PasswordResetServiceResult(
            PasswordResetStatus.InvalidToken);
    }
}
