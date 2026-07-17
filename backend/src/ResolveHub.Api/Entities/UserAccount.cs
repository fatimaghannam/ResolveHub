using Microsoft.AspNetCore.Identity;

namespace ResolveHub.Api.Entities;

public sealed class UserAccount : IdentityUser<int>
{
    public int? DepartmentID { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? JobTitle { get; set; }

    public string? ProfileImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedDate { get; set; }

    public Department? Department { get; set; }

    public ICollection<UserAccountRole> UserAccountRoles { get; set; }
        = new List<UserAccountRole>();

    public ICollection<UserAccountRole> RoleAssignmentsMade { get; set; }
        = new List<UserAccountRole>();
}