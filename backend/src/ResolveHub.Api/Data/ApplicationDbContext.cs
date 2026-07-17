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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureDepartment(builder);
        ConfigureUserAccount(builder);
        ConfigureRole(builder);
        ConfigureUserAccountRole(builder);
        ConfigureIdentitySupportTables(builder);
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