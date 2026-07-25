namespace ResolveHub.Api.Entities;

public sealed class TicketAttachment
{
    public int ID { get; set; }
    public int TicketID { get; set; }
    public int UploadedByUserAccountID { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool IsPrivate { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public UserAccount UploadedByUserAccount { get; set; } = null!;
}
