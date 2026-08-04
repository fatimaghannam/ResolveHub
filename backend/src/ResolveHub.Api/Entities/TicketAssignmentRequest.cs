namespace ResolveHub.Api.Entities;

public sealed class TicketAssignmentRequest
{
    public int ID { get; set; }
    public int TicketID { get; set; }
    public int RequestedByUserAccountID { get; set; }
    public int? RequestedAgentUserAccountID { get; set; }
    public string Status { get; set; } = AssignmentRequestStatusNames.Pending;
    public DateTime RequestedDate { get; set; }
    public int? ReviewedByUserAccountID { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewReason { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public UserAccount RequestedByUserAccount { get; set; } = null!;
    public UserAccount? RequestedAgentUserAccount { get; set; }
    public UserAccount? ReviewedByUserAccount { get; set; }
}

public static class AssignmentRequestStatusNames
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
