using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Auth;

public sealed class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(4096)]
    public string Token { get; init; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string NewPassword { get; init; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Compare(
        nameof(NewPassword),
        ErrorMessage =
            "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; init; } = string.Empty;
}
