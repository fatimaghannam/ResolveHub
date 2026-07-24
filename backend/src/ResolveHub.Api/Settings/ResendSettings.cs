namespace ResolveHub.Api.Settings;

public sealed class ResendSettings
{
    public const string SectionName = "Resend";

    public string ApiToken { get; init; } = string.Empty;

    public string FromEmail { get; init; } =
        "onboarding@resend.dev";

    public string FromName { get; init; } = "ResolveHub";
}
