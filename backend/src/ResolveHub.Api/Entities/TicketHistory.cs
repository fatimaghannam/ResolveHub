namespace ResolveHub.Api.Entities;

public sealed class TicketHistory
{
    public int ID { get; set; }
    public int TicketID { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int PerformedByUserAccountID { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsInternal { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public UserAccount PerformedByUserAccount { get; set; } = null!;
}
