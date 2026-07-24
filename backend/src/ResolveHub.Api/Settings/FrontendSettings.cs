namespace ResolveHub.Api.Settings;

public sealed class FrontendSettings
{
    public const string SectionName = "Frontend";

    public string BaseUrl { get; init; } = string.Empty;
}
