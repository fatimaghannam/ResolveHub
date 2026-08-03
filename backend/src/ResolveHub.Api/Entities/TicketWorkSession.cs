namespace ResolveHub.Api.Entities;

public sealed class TicketWorkSession
{
    public int ID { get; set; }
    public int TicketID { get; set; }
    public int ITAgentUserAccountID { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DurationMinutes { get; set; }
    public string? EndedReason { get; set; }
    public DateTime CreatedDate { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public UserAccount ITAgentUserAccount { get; set; } = null!;
}
