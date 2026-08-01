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

    public ICollection<Ticket> CreatedTickets { get; set; } = [];

    public ICollection<Ticket> AssignedTickets { get; set; } = [];

    public ICollection<TicketAttachment> UploadedTicketAttachments { get; set; } = [];

    public ICollection<TicketDraft> TicketDrafts { get; set; } = [];

    public ICollection<Ticket> ResolvedTickets { get; set; } = [];

    public ICollection<TicketComment> TicketComments { get; set; } = [];
    public ICollection<TicketCommentAttachment> UploadedCommentAttachments { get; set; } = [];

    public ICollection<TicketHistory> TicketHistoryEntries { get; set; } = [];
    public ICollection<ActivityLog> ActivityLogs { get; set; } = [];
    public ICollection<TicketAssignmentRequest> AssignmentRequestsMade { get; set; } = [];
    public ICollection<TicketAssignmentRequest> AssignmentRequestsReviewed { get; set; } = [];
    public ICollection<DuplicateReview> DuplicateReviewsReported { get; set; } = [];
    public ICollection<DuplicateReview> DuplicateReviewsReviewed { get; set; } = [];
    public ICollection<UserNotification> Notifications { get; set; } = [];
}
