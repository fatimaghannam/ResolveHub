namespace ResolveHub.Api.Settings;

public sealed class PasswordResetSettings
{
    public const string SectionName = "PasswordReset";

    public int TokenLifetimeMinutes { get; init; } = 30;
}
