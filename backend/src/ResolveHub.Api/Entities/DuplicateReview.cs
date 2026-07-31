namespace ResolveHub.Api.Entities;

public sealed class DuplicateReview
{
    public int ID { get; set; }
    public int TicketID { get; set; }
    public int SuggestedOriginalTicketID { get; set; }
    public int ReportedByUserAccountID { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ReviewedByUserAccountID { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public DateTime CreatedDate { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public Ticket SuggestedOriginalTicket { get; set; } = null!;
    public UserAccount ReportedByUserAccount { get; set; } = null!;
    public UserAccount? ReviewedByUserAccount { get; set; }
}
