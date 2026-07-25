using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
        ConfigureIdentitySupportTables(builder);
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
