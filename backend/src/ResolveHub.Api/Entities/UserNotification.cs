namespace ResolveHub.Api.Entities;

public sealed class UserNotification
{
    public int ID { get; set; }
    public int UserAccountID { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TicketReferenceNumber { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedDate { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
}
