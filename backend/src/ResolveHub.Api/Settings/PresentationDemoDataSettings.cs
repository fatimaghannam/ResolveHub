namespace ResolveHub.Api.Settings;

public sealed class PresentationDemoDataSettings
{
    public const string SectionName = "DemoData";

    public bool Enabled { get; init; }
    public bool Cleanup { get; init; }
}
