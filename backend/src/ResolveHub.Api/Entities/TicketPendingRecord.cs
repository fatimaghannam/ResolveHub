namespace ResolveHub.Api.Entities;

public sealed class TicketPendingRecord
{
    public int ID { get; set; }
    public int TicketID { get; set; }
    public int? WorkSessionID { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string ReasonText { get; set; } = string.Empty;
    public string? AdditionalNote { get; set; }
    public int CreatedByUserAccountID { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ResumedDate { get; set; }
    public int? ResumedByUserAccountID { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public TicketWorkSession? WorkSession { get; set; }
    public UserAccount CreatedByUserAccount { get; set; } = null!;
    public UserAccount? ResumedByUserAccount { get; set; }
}
