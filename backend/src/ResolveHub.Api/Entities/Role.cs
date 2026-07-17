using Microsoft.AspNetCore.Identity;

namespace ResolveHub.Api.Entities;

public sealed class Role : IdentityRole<int>
{
    public string? Description { get; set; }

    public bool IsSystemRole { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public ICollection<UserAccountRole> UserAccountRoles { get; set; }
        = new List<UserAccountRole>();
}