using System.ComponentModel.DataAnnotations;

namespace ResolveHub.Api.DTOs.Auth;

public sealed class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; init; } = string.Empty;
}
