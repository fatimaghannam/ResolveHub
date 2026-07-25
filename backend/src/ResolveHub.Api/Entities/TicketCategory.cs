namespace ResolveHub.Api.Entities;

public sealed class TicketCategory
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<TicketDraft> TicketDrafts { get; set; } = [];
}
