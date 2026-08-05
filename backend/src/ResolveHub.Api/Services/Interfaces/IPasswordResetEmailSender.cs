namespace ResolveHub.Api.Services.Interfaces;

public interface IPasswordResetEmailSender
{
    Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        CancellationToken cancellationToken);

    Task SendAccountInvitationEmailAsync(
        string recipientEmail,
        string recipientName,
        string role,
        string? department,
        string setupUrl,
        CancellationToken cancellationToken);
}
