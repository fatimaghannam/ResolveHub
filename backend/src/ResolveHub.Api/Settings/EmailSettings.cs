namespace ResolveHub.Api.Settings;

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; }

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 587;

    public bool UseSsl { get; init; } = true;

    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "ResolveHub Support";

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
