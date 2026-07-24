namespace ResolveHub.Api.Services.Interfaces;

public interface IPasswordResetEmailSender
{
    Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        CancellationToken cancellationToken);
}
