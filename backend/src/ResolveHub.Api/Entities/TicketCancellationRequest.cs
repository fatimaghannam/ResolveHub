namespace ResolveHub.Api.Entities;

public sealed class TicketCancellationRequest
{
    public int ID { get; set; }
    public int TicketID { get; set; }
    public int RequestedByAgentUserAccountID { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = CancellationRequestStatusNames.Pending;
    public DateTime RequestedDate { get; set; }
    public int? ReviewedByManagerUserAccountID { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewNote { get; set; }
    public string? Outcome { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public UserAccount RequestedByAgentUserAccount { get; set; } = null!;
    public UserAccount? ReviewedByManagerUserAccount { get; set; }
}

public static class CancellationRequestStatusNames
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class CancellationRequestOutcomeNames
{
    public const string Cancelled = "Cancelled";
    public const string Reassign = "Reassign";
}
