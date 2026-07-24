using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Options;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Settings;

namespace ResolveHub.Api.Services.Implementations;

public sealed class SmtpPasswordResetEmailSender(
    IOptions<EmailSettings> emailOptions,
    IOptions<PasswordResetSettings> passwordResetOptions)
    : IPasswordResetEmailSender
{
    private readonly EmailSettings _emailSettings =
        emailOptions.Value;

    private readonly PasswordResetSettings _passwordResetSettings =
        passwordResetOptions.Value;

    public async Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        CancellationToken cancellationToken)
    {
        if (!_emailSettings.Enabled)
        {
            throw new InvalidOperationException(
                "Password-reset email delivery is not enabled.");
        }

        var encodedName = WebUtility.HtmlEncode(recipientName);
        var encodedResetUrl = WebUtility.HtmlEncode(resetUrl);
        var expirationMinutes =
            _passwordResetSettings.TokenLifetimeMinutes;

        var plainText =
            $"Hello {recipientName},{Environment.NewLine}{Environment.NewLine}" +
            "We received a request to reset your ResolveHub password." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"Reset your password: {resetUrl}{Environment.NewLine}" +
            $"This link expires in {expirationMinutes} minutes." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "If you did not request this change, you can ignore this email.";

        var html =
            $"""
            <p>Hello {encodedName},</p>
            <p>We received a request to reset your ResolveHub password.</p>
            <p><a href="{encodedResetUrl}">Reset your ResolveHub password</a></p>
            <p>This link expires in {expirationMinutes} minutes.</p>
            <p>If you did not request this change, you can ignore this email.</p>
            """;

        using var message = new MailMessage
        {
            From = new MailAddress(
                _emailSettings.FromAddress,
                _emailSettings.FromName),
            Subject = "Reset your ResolveHub password"
        };

        message.To.Add(recipientEmail);
        message.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(
                plainText,
                null,
                MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(
                html,
                null,
                MediaTypeNames.Text.Html));

        using var smtpClient = new SmtpClient(
            _emailSettings.Host,
            _emailSettings.Port)
        {
            EnableSsl = _emailSettings.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_emailSettings.Username))
        {
            smtpClient.Credentials = new NetworkCredential(
                _emailSettings.Username,
                _emailSettings.Password);
        }

        await smtpClient.SendMailAsync(
            message,
            cancellationToken);
    }
}
