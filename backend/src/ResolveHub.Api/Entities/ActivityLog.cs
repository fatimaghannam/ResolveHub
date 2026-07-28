namespace ResolveHub.Api.Entities;

public sealed class ActivityLog
{
    public int ID { get; set; }
    public int PerformedByUserAccountID { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityID { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedDate { get; set; }
    public UserAccount PerformedByUserAccount { get; set; } = null!;
}
