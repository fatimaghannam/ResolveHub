namespace ResolveHub.Api.Settings;

public sealed class OllamaSettings
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "llama3.2";
    public int TimeoutSeconds { get; init; } = 120;
    public string? ApiKey { get; init; }
}