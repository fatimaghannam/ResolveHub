namespace ResolveHub.Api.Entities;

public sealed class Ticket
{
    public int ID { get; set; }
    public string TicketReferenceNumber { get; set; } = string.Empty;
    public int CreatedByUserAccountID { get; set; }
    public int? AssignedToUserAccountID { get; set; }
    public int TicketCategoryID { get; set; }
    public int TicketPriorityID { get; set; }
    public int TicketStatusID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public DateTime? AssignedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public string? CancelledReason { get; set; }
    public string? ResolutionSummary { get; set; }
    public int? ResolvedByUserAccountID { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public bool IsDeleted { get; set; }

    public UserAccount CreatedByUserAccount { get; set; } = null!;
    public UserAccount? AssignedToUserAccount { get; set; }
    public TicketCategory TicketCategory { get; set; } = null!;
    public TicketPriority TicketPriority { get; set; } = null!;
    public TicketStatus TicketStatus { get; set; } = null!;
    public UserAccount? ResolvedByUserAccount { get; set; }
    public ICollection<TicketAttachment> Attachments { get; set; } = [];
    public ICollection<TicketComment> Comments { get; set; } = [];
    public ICollection<TicketHistory> History { get; set; } = [];
    public ICollection<TicketAssignmentRequest> AssignmentRequests { get; set; } = [];
}
