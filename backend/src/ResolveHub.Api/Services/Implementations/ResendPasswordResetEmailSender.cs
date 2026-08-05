using System.Net;
using Microsoft.Extensions.Options;
using Resend;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Settings;

namespace ResolveHub.Api.Services.Implementations;

public sealed class ResendPasswordResetEmailSender(
    IResend resend,
    IOptions<ResendSettings> resendOptions,
    IOptions<PasswordResetSettings> passwordResetOptions)
    : IPasswordResetEmailSender
{
    private readonly ResendSettings _resendSettings =
        resendOptions.Value;

    private readonly PasswordResetSettings _passwordResetSettings =
        passwordResetOptions.Value;

    public async Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        CancellationToken cancellationToken)
    {
        var encodedName = WebUtility.HtmlEncode(recipientName);
        var encodedResetUrl = WebUtility.HtmlEncode(resetUrl);
        var expirationMinutes =
            _passwordResetSettings.TokenLifetimeMinutes;

        var message = new EmailMessage
        {
            From =
                $"{_resendSettings.FromName} <{_resendSettings.FromEmail}>",
            Subject = "Reset your ResolveHub password",
            TextBody =
                BuildPlainTextBody(
                    recipientName,
                    resetUrl,
                    expirationMinutes),
            HtmlBody =
                BuildHtmlBody(
                    encodedName,
                    encodedResetUrl,
                    expirationMinutes)
        };

        message.To.Add(recipientEmail);

        await resend.EmailSendAsync(
            message,
            cancellationToken);
    }

    public async Task SendAccountInvitationEmailAsync(
        string recipientEmail,
        string recipientName,
        string role,
        string? department,
        string setupUrl,
        CancellationToken cancellationToken)
    {
        var encodedName = WebUtility.HtmlEncode(recipientName);
        var encodedRole = WebUtility.HtmlEncode(role);
        var encodedDepartment = WebUtility.HtmlEncode(department);
        var encodedSetupUrl = WebUtility.HtmlEncode(setupUrl);
        var expirationMinutes = _passwordResetSettings.TokenLifetimeMinutes;
        var message = new EmailMessage
        {
            From = $"{_resendSettings.FromName} <{_resendSettings.FromEmail}>",
            Subject = "Welcome to ResolveHub — Set Up Your Account",
            TextBody =
                $"Hello {recipientName},{Environment.NewLine}{Environment.NewLine}" +
                "Your company Administrator created a ResolveHub account for you." +
                $"{Environment.NewLine}Assigned role: {role}" +
                (string.IsNullOrWhiteSpace(department) ? string.Empty :
                    $"{Environment.NewLine}Department: {department}") +
                $"{Environment.NewLine}{Environment.NewLine}Use the secure link below to create your password:" +
                $"{Environment.NewLine}{Environment.NewLine}{setupUrl}{Environment.NewLine}" +
                $"This link expires in {expirationMinutes} minutes.{Environment.NewLine}{Environment.NewLine}" +
                "If you did not expect this invitation, contact IT Support.",
            HtmlBody = $"""
                <!doctype html><html lang="en"><body style="margin:0;background:#f3f6fa;font-family:Arial,sans-serif;color:#172033;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="padding:32px 16px;"><tr><td align="center">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#fff;border:1px solid #dce5ef;border-radius:16px;"><tr><td style="padding:36px;">
                <p style="color:#1769c2;font-weight:700;letter-spacing:1px;text-transform:uppercase;">ResolveHub</p>
                <h1>Set up your account</h1><p>Hello {encodedName},</p><p>Your company Administrator created a ResolveHub account for you.</p>
                <p><strong>Assigned role:</strong> {encodedRole}</p>
                {(string.IsNullOrWhiteSpace(department) ? string.Empty : $"<p><strong>Department:</strong> {encodedDepartment}</p>")}
                <p>Create your password to finish setting up your account.</p>
                <p><a href="{encodedSetupUrl}" style="display:inline-block;padding:14px 24px;border-radius:8px;background:#1769c2;color:#fff;font-weight:700;text-decoration:none;">Create Password</a></p>
                <p>This link expires in {expirationMinutes} minutes.</p><p>If you did not expect this invitation, contact IT Support.</p>
                </td></tr></table></td></tr></table></body></html>
                """
        };
        message.To.Add(recipientEmail);
        await resend.EmailSendAsync(message, cancellationToken);
    }

    private static string BuildPlainTextBody(
        string recipientName,
        string resetUrl,
        int expirationMinutes)
    {
        return
            $"Hello {recipientName},{Environment.NewLine}{Environment.NewLine}" +
            "We received a request to reset your ResolveHub password." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"Reset your password: {resetUrl}{Environment.NewLine}" +
            $"This link expires in {expirationMinutes} minutes." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "If you did not request this password reset, you can safely ignore this email.";
    }

    private static string BuildHtmlBody(
        string encodedName,
        string encodedResetUrl,
        int expirationMinutes)
    {
        return
            $"""
            <!doctype html>
            <html lang="en">
            <body style="margin:0;background:#f3f6fa;font-family:Arial,sans-serif;color:#172033;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6fa;padding:32px 16px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#ffffff;border:1px solid #dce5ef;border-radius:16px;">
                      <tr>
                        <td style="padding:36px;">
                          <p style="margin:0 0 24px;color:#1769c2;font-size:14px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">ResolveHub</p>
                          <h1 style="margin:0 0 16px;font-size:28px;line-height:1.2;color:#13233a;">Reset your password</h1>
                          <p style="margin:0 0 16px;line-height:1.6;">Hello {encodedName},</p>
                          <p style="margin:0 0 24px;line-height:1.6;">We received a request to reset your ResolveHub password.</p>
                          <p style="margin:0 0 24px;">
                            <a href="{encodedResetUrl}" style="display:inline-block;padding:14px 24px;border-radius:8px;background:#1769c2;color:#ffffff;font-weight:700;text-decoration:none;">Reset Password</a>
                          </p>
                          <p style="margin:0 0 8px;line-height:1.6;">This link expires in {expirationMinutes} minutes.</p>
                          <p style="margin:0 0 8px;line-height:1.6;">If the button does not work, copy and paste this URL into your browser:</p>
                          <p style="margin:0 0 24px;overflow-wrap:anywhere;line-height:1.5;"><a href="{encodedResetUrl}" style="color:#1769c2;">{encodedResetUrl}</a></p>
                          <p style="margin:0;color:#65758b;line-height:1.6;">If you did not request this password reset, you can safely ignore this email.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
