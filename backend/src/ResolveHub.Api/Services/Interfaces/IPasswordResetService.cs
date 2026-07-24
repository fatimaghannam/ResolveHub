using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Interfaces;

public interface IPasswordResetService
{
    Task RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken);

    Task<PasswordResetServiceResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken);
}
