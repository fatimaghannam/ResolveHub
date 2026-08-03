using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ResolveHub.Api.Entities;

namespace ResolveHub.Api.Data;

public sealed class ApplicationDbContext
    : IdentityDbContext<
        UserAccount,
        Role,
        int,
        IdentityUserClaim<int>,
        UserAccountRole,
        IdentityUserLogin<int>,
        IdentityRoleClaim<int>,
        IdentityUserToken<int>>
{
    // SQL Server datetime2 preserves UTC clock ticks but not DateTime.Kind. Marking
    // values as UTC when materializing makes System.Text.Json emit an unambiguous Z
    // without altering historical values already stored as UTC.
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcDateTimeConverter =
        new(value => value, value => value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value);

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();
    public DbSet<TicketStatus> TicketStatuses => Set<TicketStatus>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketDraft> TicketDrafts => Set<TicketDraft>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketCommentAttachment> TicketCommentAttachments => Set<TicketCommentAttachment>();
    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();
    public DbSet<TicketWorkSession> TicketWorkSessions => Set<TicketWorkSession>();
    public DbSet<TicketPendingRecord> TicketPendingRecords => Set<TicketPendingRecord>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<TicketAssignmentRequest> TicketAssignmentRequests =>
        Set<TicketAssignmentRequest>();
    public DbSet<DuplicateReview> DuplicateReviews => Set<DuplicateReview>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureDepartment(builder);
        ConfigureUserAccount(builder);
        ConfigureRole(builder);
        ConfigureUserAccountRole(builder);
        ConfigureTicketCategory(builder);
        ConfigureTicketPriority(builder);
        ConfigureTicketStatus(builder);
        ConfigureTicket(builder);
        ConfigureTicketAttachment(builder);
        ConfigureTicketDraft(builder);
        ConfigureTicketComment(builder);
        ConfigureTicketCommentAttachment(builder);
        ConfigureTicketHistory(builder);
        ConfigureTicketWorkSession(builder);
        ConfigureTicketPendingRecord(builder);
        ConfigureActivityLog(builder);
        ConfigureTicketAssignmentRequest(builder);
        ConfigureDuplicateReview(builder);
        ConfigureUserNotification(builder);
        ConfigureIdentitySupportTables(builder);
    }

    private static void ConfigureDuplicateReview(ModelBuilder builder)
    {
        builder.Entity<DuplicateReview>(entity =>
        {
            entity.ToTable("DuplicateReview");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.ID).UseIdentityColumn();
            entity.Property(item => item.Reason).HasMaxLength(1000);
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.CreatedDate).HasColumnType("datetime2");
            entity.Property(item => item.ReviewedDate).HasColumnType("datetime2");
            entity.HasIndex(item => new { item.TicketID, item.Status });
            entity.HasIndex(item => new { item.Status, item.CreatedDate });
            entity.HasOne(item => item.Ticket).WithMany(ticket => ticket.DuplicateReviews)
                .HasForeignKey(item => item.TicketID).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SuggestedOriginalTicket)
                .WithMany(ticket => ticket.SuggestedDuplicateReviews)
                .HasForeignKey(item => item.SuggestedOriginalTicketID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReportedByUserAccount)
                .WithMany(user => user.DuplicateReviewsReported)
                .HasForeignKey(item => item.ReportedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReviewedByUserAccount)
                .WithMany(user => user.DuplicateReviewsReviewed)
                .HasForeignKey(item => item.ReviewedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUserNotification(ModelBuilder builder)
    {
        builder.Entity<UserNotification>(entity =>
        {
            entity.ToTable("UserNotification");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.ID).UseIdentityColumn();
            entity.Property(item => item.Type).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.TicketReferenceNumber).HasMaxLength(32);
            entity.Property(item => item.CreatedDate).HasColumnType("datetime2");
            entity.HasIndex(item => new { item.UserAccountID, item.IsRead, item.CreatedDate });
            entity.HasOne(item => item.UserAccount).WithMany(user => user.Notifications)
                .HasForeignKey(item => item.UserAccountID).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureTicketAssignmentRequest(ModelBuilder builder)
    {
        builder.Entity<TicketAssignmentRequest>(entity =>
        {
            entity.ToTable("TicketAssignmentRequest");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.ID).UseIdentityColumn();
            entity.Property(item => item.Status).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RequestedDate).HasColumnType("datetime2");
            entity.Property(item => item.ReviewedDate).HasColumnType("datetime2");
            entity.HasIndex(item => new
            {
                item.TicketID,
                item.RequestedByUserAccountID,
                item.Status
            });
            entity.HasIndex(item => new { item.Status, item.RequestedDate });
            entity.HasOne(item => item.Ticket)
                .WithMany(ticket => ticket.AssignmentRequests)
                .HasForeignKey(item => item.TicketID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RequestedByUserAccount)
                .WithMany(user => user.AssignmentRequestsMade)
                .HasForeignKey(item => item.RequestedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReviewedByUserAccount)
                .WithMany(user => user.AssignmentRequestsReviewed)
                .HasForeignKey(item => item.ReviewedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureActivityLog(ModelBuilder builder)
    {
        builder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("ActivityLog");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.ID).UseIdentityColumn();
            entity.Property(item => item.ActionType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.EntityID).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.OldValue).HasMaxLength(500);
            entity.Property(item => item.NewValue).HasMaxLength(500);
            entity.Property(item => item.CreatedDate).HasColumnType("datetime2");
            entity.HasIndex(item => item.CreatedDate);
            entity.HasIndex(item => new { item.EntityType, item.EntityID });
            entity.HasOne(item => item.PerformedByUserAccount)
                .WithMany(user => user.ActivityLogs)
                .HasForeignKey(item => item.PerformedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTicketCategory(ModelBuilder builder)
    {
        builder.Entity<TicketCategory>(entity =>
        {
            entity.ToTable("TicketCategory");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.ID).UseIdentityColumn();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.IsActive).HasDefaultValue(true);
            entity.HasIndex(item => item.Name).IsUnique();
        });
    }

    private static void ConfigureTicketPriority(ModelBuilder builder)
    {
        builder.Entity<TicketPriority>(entity =>
        {
            entity.ToTable("TicketPriority");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.ID).UseIdentityColumn();
            entity.Property(item => item.Name).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.IsActive).HasDefaultValue(true);
            entity.HasIndex(item => item.Name).IsUnique();
        });
    }

    private static void ConfigureTicketStatus(ModelBuilder builder)
    {
        builder.Entity<TicketStatus>(entity =>
        {
            entity.ToTable("TicketStatus");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.ID).UseIdentityColumn();
            entity.Property(item => item.Name).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.IsActive).HasDefaultValue(true);
            entity.HasIndex(item => item.Name).IsUnique();
        });
    }

    private static void ConfigureTicket(ModelBuilder builder)
    {
        builder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Ticket");
            entity.HasKey(ticket => ticket.ID);
            entity.Property(ticket => ticket.ID).UseIdentityColumn();
            entity.Property(ticket => ticket.TicketReferenceNumber)
                .HasMaxLength(32).IsRequired();
            entity.HasIndex(ticket => ticket.TicketReferenceNumber).IsUnique();
            entity.Property(ticket => ticket.Title).HasMaxLength(200).IsRequired();
            entity.Property(ticket => ticket.Description)
                .HasMaxLength(5000).IsRequired();
            entity.Property(ticket => ticket.CancelledReason).HasMaxLength(500);
            entity.Property(ticket => ticket.ResolutionSummary).HasMaxLength(5000);
            entity.Property(ticket => ticket.RowVersion).IsRowVersion();
            entity.Property(ticket => ticket.CreatedDate).HasColumnType("datetime2");
            entity.Property(ticket => ticket.UpdatedDate).HasColumnType("datetime2");
            entity.Property(ticket => ticket.AssignedDate).HasColumnType("datetime2");
            entity.Property(ticket => ticket.ResolvedDate).HasColumnType("datetime2");
            entity.Property(ticket => ticket.ClosedDate).HasColumnType("datetime2");
            entity.Property(ticket => ticket.CancelledDate).HasColumnType("datetime2");
            entity.Property(ticket => ticket.IsDeleted).HasDefaultValue(false);

            entity.HasIndex(ticket => ticket.CreatedByUserAccountID);
            entity.HasIndex(ticket => ticket.TicketStatusID);
            entity.HasIndex(ticket => ticket.TicketCategoryID);
            entity.HasIndex(ticket => ticket.TicketPriorityID);
            entity.HasIndex(ticket => ticket.CreatedDate);
            entity.HasIndex(ticket => ticket.AssignedToUserAccountID);
            entity.HasIndex(ticket => new
            {
                ticket.AssignedToUserAccountID,
                ticket.IsDeleted,
                ticket.TicketStatusID
            });
            entity.HasIndex(ticket => new
            {
                ticket.AssignedToUserAccountID,
                ticket.IsDeleted,
                ticket.AssignedDate
            });
            entity.HasIndex(ticket => ticket.ResolvedDate);
            entity.HasIndex(ticket => new
            {
                ticket.CreatedByUserAccountID,
                ticket.IsDeleted,
                ticket.CreatedDate
            });

            entity.HasOne(ticket => ticket.CreatedByUserAccount)
                .WithMany(user => user.CreatedTickets)
                .HasForeignKey(ticket => ticket.CreatedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.AssignedToUserAccount)
                .WithMany(user => user.AssignedTickets)
                .HasForeignKey(ticket => ticket.AssignedToUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.TicketCategory)
                .WithMany(category => category.Tickets)
                .HasForeignKey(ticket => ticket.TicketCategoryID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.TicketPriority)
                .WithMany(priority => priority.Tickets)
                .HasForeignKey(ticket => ticket.TicketPriorityID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.TicketStatus)
                .WithMany(status => status.Tickets)
                .HasForeignKey(ticket => ticket.TicketStatusID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.ResolvedByUserAccount)
                .WithMany(user => user.ResolvedTickets)
                .HasForeignKey(ticket => ticket.ResolvedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(ticket => ticket.OriginalTicket)
                .WithMany(ticket => ticket.DuplicateTickets)
                .HasForeignKey(ticket => ticket.OriginalTicketID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTicketComment(ModelBuilder builder)
    {
        builder.Entity<TicketComment>(entity =>
        {
            entity.ToTable("TicketComment");
            entity.HasKey(comment => comment.ID);
            entity.Property(comment => comment.ID).UseIdentityColumn();
            entity.Property(comment => comment.Content).HasMaxLength(5000).IsRequired();
            entity.Property(comment => comment.CreatedDate)
                .HasConversion(UtcDateTimeConverter).HasColumnType("datetime2");
            entity.Property(comment => comment.UpdatedDate)
                .HasConversion(NullableUtcDateTimeConverter).HasColumnType("datetime2");
            entity.Property(comment => comment.DeletedDate)
                .HasConversion(NullableUtcDateTimeConverter).HasColumnType("datetime2");
            entity.Property(comment => comment.Visibility)
                .HasConversion<string>().HasMaxLength(20);
            entity.Property(comment => comment.IsEdited).HasDefaultValue(false);
            entity.Property(comment => comment.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(comment => new
            {
                comment.TicketID,
                comment.Visibility,
                comment.IsDeleted,
                comment.CreatedDate
            });
            entity.HasIndex(comment => comment.AuthorUserAccountID);
            entity.HasIndex(comment => comment.ParentCommentID);
            entity.HasOne(comment => comment.Ticket)
                .WithMany(ticket => ticket.Comments)
                .HasForeignKey(comment => comment.TicketID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(comment => comment.AuthorUserAccount)
                .WithMany(user => user.TicketComments)
                .HasForeignKey(comment => comment.AuthorUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(comment => comment.ParentComment)
                .WithMany(comment => comment.Replies)
                .HasForeignKey(comment => comment.ParentCommentID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTicketCommentAttachment(ModelBuilder builder)
    {
        builder.Entity<TicketCommentAttachment>(entity =>
        {
            entity.ToTable("TicketCommentAttachment");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.FileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.StoredFileName).HasMaxLength(255).IsRequired();
            entity.Property(item => item.FilePath).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(150).IsRequired();
            entity.Property(item => item.UploadedDate)
                .HasConversion(UtcDateTimeConverter).HasColumnType("datetime2");
            entity.HasIndex(item => item.TicketCommentID);
            entity.HasOne(item => item.TicketComment).WithMany(comment => comment.Attachments)
                .HasForeignKey(item => item.TicketCommentID).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.UploadedByUserAccount)
                .WithMany(user => user.UploadedCommentAttachments)
                .HasForeignKey(item => item.UploadedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTicketHistory(ModelBuilder builder)
    {
        builder.Entity<TicketHistory>(entity =>
        {
            entity.ToTable("TicketHistory");
            entity.HasKey(history => history.ID);
            entity.Property(history => history.ID).UseIdentityColumn();
            entity.Property(history => history.ActionType).HasMaxLength(100).IsRequired();
            entity.Property(history => history.OldValue).HasMaxLength(500);
            entity.Property(history => history.NewValue).HasMaxLength(500);
            entity.Property(history => history.Description).HasMaxLength(1000);
            entity.Property(history => history.CreatedDate).HasColumnType("datetime2");
            entity.Property(history => history.WorkDurationMinutes);
            entity.HasIndex(history => history.PerformedByUserAccountID);
            entity.HasIndex(history => new { history.TicketID, history.CreatedDate });
            entity.HasOne(history => history.Ticket)
                .WithMany(ticket => ticket.History)
                .HasForeignKey(history => history.TicketID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(history => history.PerformedByUserAccount)
                .WithMany(user => user.TicketHistoryEntries)
                .HasForeignKey(history => history.PerformedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTicketWorkSession(ModelBuilder builder)
    {
        builder.Entity<TicketWorkSession>(entity =>
        {
            entity.ToTable("TicketWorkSession");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.ID).UseIdentityColumn();
            entity.Property(item => item.StartedAt).HasConversion(UtcDateTimeConverter).HasColumnType("datetime2");
            entity.Property(item => item.EndedAt).HasConversion(NullableUtcDateTimeConverter).HasColumnType("datetime2");
            entity.Property(item => item.CreatedDate).HasConversion(UtcDateTimeConverter).HasColumnType("datetime2");
            entity.Property(item => item.EndedReason).HasMaxLength(100);
            entity.HasIndex(item => new { item.TicketID, item.StartedAt });
            entity.HasIndex(item => new { item.ITAgentUserAccountID, item.StartedAt });
            entity.HasIndex(item => item.TicketID).HasFilter("[EndedAt] IS NULL").IsUnique();
            entity.HasOne(item => item.Ticket).WithMany(ticket => ticket.WorkSessions)
                .HasForeignKey(item => item.TicketID).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ITAgentUserAccount).WithMany(user => user.TicketWorkSessions)
                .HasForeignKey(item => item.ITAgentUserAccountID).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTicketPendingRecord(ModelBuilder builder)
    {
        builder.Entity<TicketPendingRecord>(entity =>
        {
            entity.ToTable("TicketPendingRecord");
            entity.HasKey(item => item.ID);
            entity.Property(item => item.ID).UseIdentityColumn();
            entity.Property(item => item.ReasonCode).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ReasonText).HasMaxLength(300).IsRequired();
            entity.Property(item => item.AdditionalNote).HasMaxLength(1000);
            entity.Property(item => item.CreatedDate)
                .HasConversion(UtcDateTimeConverter).HasColumnType("datetime2");
            entity.Property(item => item.ResumedDate)
                .HasConversion(NullableUtcDateTimeConverter).HasColumnType("datetime2");
            entity.HasIndex(item => new { item.TicketID, item.CreatedDate });
            entity.HasIndex(item => item.TicketID)
                .HasFilter("[ResumedDate] IS NULL").IsUnique();
            entity.HasOne(item => item.Ticket).WithMany(ticket => ticket.PendingRecords)
                .HasForeignKey(item => item.TicketID).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkSession).WithMany(session => session.PendingRecords)
                .HasForeignKey(item => item.WorkSessionID).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CreatedByUserAccount).WithMany()
                .HasForeignKey(item => item.CreatedByUserAccountID).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ResumedByUserAccount).WithMany()
                .HasForeignKey(item => item.ResumedByUserAccountID).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTicketAttachment(ModelBuilder builder)
    {
        builder.Entity<TicketAttachment>(entity =>
        {
            entity.ToTable("TicketAttachment");
            entity.HasKey(attachment => attachment.ID);
            entity.Property(attachment => attachment.ID).UseIdentityColumn();
            entity.Property(attachment => attachment.FileName).HasMaxLength(255).IsRequired();
            entity.Property(attachment => attachment.StoredFileName).HasMaxLength(100).IsRequired();
            entity.Property(attachment => attachment.FilePath).HasMaxLength(500).IsRequired();
            entity.Property(attachment => attachment.ContentType).HasMaxLength(150).IsRequired();
            entity.Property(attachment => attachment.IsPrivate).HasDefaultValue(true);
            entity.Property(attachment => attachment.IsDeleted).HasDefaultValue(false);
            entity.Property(attachment => attachment.UploadedDate).HasColumnType("datetime2");
            entity.HasIndex(attachment => new { attachment.TicketID, attachment.IsDeleted });
            entity.HasOne(attachment => attachment.Ticket)
                .WithMany(ticket => ticket.Attachments)
                .HasForeignKey(attachment => attachment.TicketID)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(attachment => attachment.UploadedByUserAccount)
                .WithMany(user => user.UploadedTicketAttachments)
                .HasForeignKey(attachment => attachment.UploadedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTicketDraft(ModelBuilder builder)
    {
        builder.Entity<TicketDraft>(entity =>
        {
            entity.ToTable("TicketDraft");
            entity.HasKey(draft => draft.ID);
            entity.Property(draft => draft.ID).UseIdentityColumn();
            entity.Property(draft => draft.Title).HasMaxLength(200);
            entity.Property(draft => draft.Description).HasMaxLength(5000);
            entity.Property(draft => draft.CreatedDate).HasColumnType("datetime2");
            entity.Property(draft => draft.UpdatedDate).HasColumnType("datetime2");
            entity.HasIndex(draft => new { draft.UserAccountID, draft.UpdatedDate });
            entity.HasOne(draft => draft.UserAccount)
                .WithMany(user => user.TicketDrafts)
                .HasForeignKey(draft => draft.UserAccountID)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(draft => draft.TicketCategory)
                .WithMany(category => category.TicketDrafts)
                .HasForeignKey(draft => draft.TicketCategoryID)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(draft => draft.TicketPriority)
                .WithMany(priority => priority.TicketDrafts)
                .HasForeignKey(draft => draft.TicketPriorityID)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureDepartment(ModelBuilder builder)
    {
        builder.Entity<Department>(entity =>
        {
            entity.ToTable("Department");

            entity.HasKey(department => department.ID);

            entity.Property(department => department.ID)
                .UseIdentityColumn();

            entity.Property(department => department.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(department => department.Name)
                .IsUnique();

            entity.Property(department => department.Description)
                .HasMaxLength(500);

            entity.Property(department => department.IsActive)
                .HasDefaultValue(true);

            entity.Property(department => department.CreatedDate)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }

    private static void ConfigureUserAccount(ModelBuilder builder)
    {
        builder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccount");

            entity.Property(user => user.Id)
                .HasColumnName("ID")
                .UseIdentityColumn();

            entity.Property(user => user.DepartmentID)
                .HasColumnName("DepartmentID");

            entity.Property(user => user.UserName)
                .HasMaxLength(50);

            entity.Property(user => user.NormalizedUserName)
                .HasMaxLength(50);

            entity.Property(user => user.Email)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(user => user.NormalizedEmail)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(user => user.PasswordHash)
                .HasMaxLength(500);

            entity.Property(user => user.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(user => user.LastName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(user => user.PhoneNumber)
                .HasMaxLength(30);

            entity.Property(user => user.JobTitle)
                .HasMaxLength(100);

            entity.Property(user => user.ProfileImagePath)
                .HasMaxLength(500);

            entity.Property(user => user.EmailConfirmed)
                .HasColumnName("IsEmailConfirmed");

            entity.Property(user => user.IsActive)
                .HasDefaultValue(true);

            entity.Property(user => user.LastLoginDate)
                .HasColumnType("datetime2");

            entity.Property(user => user.CreatedDate)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(user => user.UpdatedDate)
                .HasColumnType("datetime2");

            entity.HasOne(user => user.Department)
                .WithMany(department => department.UserAccounts)
                .HasForeignKey(user => user.DepartmentID)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureRole(ModelBuilder builder)
    {
        builder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");

            entity.Property(role => role.Id)
                .HasColumnName("ID")
                .UseIdentityColumn();

            entity.Property(role => role.Name)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(role => role.NormalizedName)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(role => role.Description)
                .HasMaxLength(300);

            entity.Property(role => role.IsSystemRole)
                .HasDefaultValue(true);

            entity.Property(role => role.IsActive)
                .HasDefaultValue(true);
        });
    }

    private static void ConfigureUserAccountRole(ModelBuilder builder)
    {
        builder.Entity<UserAccountRole>(entity =>
        {
            entity.ToTable("UserAccountRole");

            // ASP.NET Core Identity requires this composite primary key.
            entity.HasKey(userRole => new
            {
                userRole.UserId,
                userRole.RoleId
            });

            entity.Property(userRole => userRole.UserId)
                .HasColumnName("UserAccountID");

            entity.Property(userRole => userRole.RoleId)
                .HasColumnName("RoleID");

            entity.Property(userRole => userRole.AssignedByUserAccountID)
                .HasColumnName("AssignedByUserAccountID");

            entity.Property(userRole => userRole.AssignedDate)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne(userRole => userRole.UserAccount)
                .WithMany(user => user.UserAccountRoles)
                .HasForeignKey(userRole => userRole.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(userRole => userRole.Role)
                .WithMany(role => role.UserAccountRoles)
                .HasForeignKey(userRole => userRole.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(userRole => userRole.AssignedByUserAccount)
                .WithMany(user => user.RoleAssignmentsMade)
                .HasForeignKey(userRole => userRole.AssignedByUserAccountID)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureIdentitySupportTables(
        ModelBuilder builder)
    {
        builder.Entity<IdentityUserClaim<int>>(entity =>
        {
            entity.ToTable("UserAccountClaim");

            entity.Property(claim => claim.Id)
                .HasColumnName("ID");

            entity.Property(claim => claim.UserId)
                .HasColumnName("UserAccountID");
        });

        builder.Entity<IdentityUserLogin<int>>(entity =>
        {
            entity.ToTable("UserAccountLogin");

            entity.Property(login => login.UserId)
                .HasColumnName("UserAccountID");
        });

        builder.Entity<IdentityRoleClaim<int>>(entity =>
        {
            entity.ToTable("RoleClaim");

            entity.Property(claim => claim.Id)
                .HasColumnName("ID");

            entity.Property(claim => claim.RoleId)
                .HasColumnName("RoleID");
        });

        builder.Entity<IdentityUserToken<int>>(entity =>
        {
            entity.ToTable("UserAccountToken");

            entity.Property(token => token.UserId)
                .HasColumnName("UserAccountID");
        });
    }
}
