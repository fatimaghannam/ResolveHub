using Microsoft.AspNetCore.Identity;

namespace ResolveHub.Api.Entities;

public sealed class UserAccountRole : IdentityUserRole<int>
{
    public int? AssignedByUserAccountID { get; set; }

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    public UserAccount UserAccount { get; set; } = null!;

    public Role Role { get; set; } = null!;

    public UserAccount? AssignedByUserAccount { get; set; }
}