namespace ResolveHub.Api.Entities;

public sealed class TicketCommentAttachment
{
    public int ID { get; set; }
    public int TicketCommentID { get; set; }
    public int UploadedByUserAccountID { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    public TicketComment TicketComment { get; set; } = null!;
    public UserAccount UploadedByUserAccount { get; set; } = null!;
}
