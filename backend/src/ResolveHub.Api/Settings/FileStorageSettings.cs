namespace ResolveHub.Api.Settings;

public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorage";
    public string UploadRoot { get; init; } = "App_Data/TicketUploads";
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
    public int MaxFilesPerTicket { get; init; } = 5;
}
