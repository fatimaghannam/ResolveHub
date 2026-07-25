namespace ResolveHub.Api.Entities;

public sealed class Asset
{
    public int ID { get; set; }
    public int? DepartmentID { get; set; }
    public int? AssignedToUserAccountID { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
    public string AssetStatus { get; set; } = "Active";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public Department? Department { get; set; }
    public UserAccount? AssignedToUserAccount { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<TicketDraft> TicketDrafts { get; set; } = [];
}
