namespace ResolveHub.Api.Entities;

public sealed class Department
{
    public int ID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public ICollection<UserAccount> UserAccounts { get; set; }
        = new List<UserAccount>();
}