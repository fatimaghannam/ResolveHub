namespace ResolveHub.Api.Entities;

public sealed class TicketDraft
{
    public int ID { get; set; }
    public int UserAccountID { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? TicketCategoryID { get; set; }
    public int? TicketPriorityID { get; set; }
    public int? AssetID { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    public UserAccount UserAccount { get; set; } = null!;
    public TicketCategory? TicketCategory { get; set; }
    public TicketPriority? TicketPriority { get; set; }
    public Asset? Asset { get; set; }
}
