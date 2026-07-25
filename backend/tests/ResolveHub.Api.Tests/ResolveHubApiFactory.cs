using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.Entities;
using ResolveHub.Api.DTOs.Auth;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Tests;

public sealed class ResolveHubApiFactory
    : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "ResolveHub.Api.Tests";
    public const string JwtAudience = "ResolveHub.Api.Tests.Client";
    public const string JwtKey =
        "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=";

    private readonly string _databaseName =
        $"ResolveHubTests-{Guid.NewGuid():N}";
    private readonly TimeSpan? _tokenLifespan;
    private readonly bool _throwPasswordResetRequests;

    public ResolveHubApiFactory(
        TimeSpan? tokenLifespan = null,
        bool throwPasswordResetRequests = false)
    {
        _tokenLifespan = tokenLifespan;
        _throwPasswordResetRequests =
            throwPasswordResetRequests;
    }

    public FakePasswordResetEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Server=(localdb)\\mssqllocaldb;Database=ResolveHubTests;");
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting(
            "Jwt:AccessTokenExpirationMinutes",
            "1440");
        builder.UseSetting(
            "Cors:AllowedOrigins:0",
            "https://localhost:5173");
        builder.UseSetting(
            "Frontend:BaseUrl",
            "https://frontend.resolvehub.test");
        builder.UseSetting(
            "PasswordReset:TokenLifetimeMinutes",
            "30");
        builder.UseSetting(
            "Resend:ApiToken",
            "re_test_placeholder");
        builder.UseSetting(
            "Resend:FromEmail",
            "onboarding@resend.dev");
        builder.UseSetting(
            "Resend:FromName",
            "ResolveHub");
        builder.UseSetting(
            "FileStorage:UploadRoot",
            Path.Combine(
                Path.GetTempPath(),
                $"ResolveHubAttachmentTests-{_databaseName}"));
        builder.UseSetting("FileStorage:MaxFileSizeBytes", "10485760");
        builder.UseSetting("FileStorage:MaxFilesPerTicket", "5");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<
                DbContextOptions<ApplicationDbContext>>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(
                options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                });

            services.AddDataProtection()
                .UseEphemeralDataProtectionProvider();
            services.Configure<PasswordHasherOptions>(
                options =>
                {
                    options.IterationCount = 1_000;
                });

            services.RemoveAll<IPasswordResetEmailSender>();
            services.AddSingleton<IPasswordResetEmailSender>(
                EmailSender);

            if (_throwPasswordResetRequests)
            {
                services.RemoveAll<IPasswordResetService>();
                services.AddScoped<
                    IPasswordResetService,
                    ThrowingPasswordResetService>();
            }

            if (_tokenLifespan is not null)
            {
                services.PostConfigure<
                    DataProtectionTokenProviderOptions>(
                    options =>
                    {
                        options.TokenLifespan =
                            _tokenLifespan.Value;
                    });
            }
        });
    }

    public HttpClient CreateHttpsClient()
    {
        return CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }

    public async Task<UserAccount> CreateUserAsync(
        string email,
        string password,
        string? roleName = RoleNames.Employee,
        bool isActive = true,
        bool emailConfirmed = true)
    {
        using var scope = Services.CreateScope();

        var roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<UserAccount>>();

        if (roleName is not null &&
            !await roleManager.RoleExistsAsync(roleName))
        {
            var roleResult = await roleManager.CreateAsync(
                new Role
                {
                    Name = roleName,
                    IsActive = true,
                    IsSystemRole = true
                });

            EnsureSucceeded(roleResult);
        }

        var user = new UserAccount
        {
            UserName = email,
            Email = email,
            EmailConfirmed = emailConfirmed,
            FirstName = "Test",
            LastName = "User",
            IsActive = isActive,
            LockoutEnabled = true
        };

        EnsureSucceeded(
            await userManager.CreateAsync(user, password));

        if (roleName is not null)
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, roleName));
        }

        return user;
    }

    public async Task<UserAccount> GetUserAsync(string email)
    {
        using var scope = Services.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<UserAccount>>();

        return await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException(
                $"Test user '{email}' was not found.");
    }

    public async Task SetLockoutAsync(
        string email,
        int failedCount,
        DateTimeOffset lockoutEnd)
    {
        using var scope = Services.CreateScope();
        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<UserAccount>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException(
                $"Test user '{email}' was not found.");

        user.AccessFailedCount = failedCount;
        user.LockoutEnd = lockoutEnd;
        EnsureSucceeded(await userManager.UpdateAsync(user));
    }

    public async Task SeedTicketLookupsAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        if (await context.TicketCategories.AnyAsync())
            return;

        context.TicketCategories.AddRange(
            new TicketCategory { Name = "Hardware", SortOrder = 1 },
            new TicketCategory { Name = "Software", SortOrder = 2 });
        context.TicketPriorities.AddRange(
            new TicketPriority { Name = "Low", SortOrder = 1 },
            new TicketPriority { Name = "High", SortOrder = 2 });
        context.TicketStatuses.AddRange(
            new TicketStatus { Name = TicketStatusNames.Open, SortOrder = 1 },
            new TicketStatus { Name = TicketStatusNames.Assigned, SortOrder = 2 },
            new TicketStatus { Name = TicketStatusNames.InProgress, SortOrder = 3 },
            new TicketStatus { Name = TicketStatusNames.Resolved, SortOrder = 4 });
        await context.SaveChangesAsync();
    }

    public async Task<(int CategoryId, int PriorityId)> GetTicketLookupIdsAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        return (
            await context.TicketCategories.Select(item => item.ID).FirstAsync(),
            await context.TicketPriorities.Select(item => item.ID).FirstAsync());
    }

    public async Task SetTicketStateAsync(
        int ticketId,
        string statusName,
        int? assignedUserId = null)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var ticket = await context.Tickets.FindAsync(ticketId)
            ?? throw new InvalidOperationException("Ticket not found.");
        ticket.TicketStatusID = await context.TicketStatuses
            .Where(status => status.Name == statusName)
            .Select(status => status.ID)
            .SingleAsync();
        ticket.AssignedToUserAccountID = assignedUserId;
        await context.SaveChangesAsync();
    }

    public async Task SetTicketCreatedDateAsync(
        int ticketId,
        DateTime createdDate)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var ticket = await context.Tickets.FindAsync(ticketId)
            ?? throw new InvalidOperationException("Ticket not found.");
        ticket.CreatedDate = createdDate;
        ticket.UpdatedDate = createdDate;
        await context.SaveChangesAsync();
    }

    public async Task<Asset> CreateAssetAsync(
        int? assignedUserId = null,
        int? departmentId = null,
        bool isActive = true)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        var asset = new Asset
        {
            AssetTag = $"TEST-{Guid.NewGuid():N}"[..18],
            AssetName = "Integration Test Laptop",
            AssetType = "Laptop",
            SerialNumber = Guid.NewGuid().ToString("N"),
            Location = "Test Office",
            AssignedToUserAccountID = assignedUserId,
            DepartmentID = departmentId,
            IsActive = isActive
        };
        context.Assets.Add(asset);
        await context.SaveChangesAsync();
        return asset;
    }

    public async Task<TicketTestSnapshot> GetTicketSnapshotAsync(int ticketId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        return await context.Tickets
            .Where(ticket => ticket.ID == ticketId)
            .Select(ticket => new TicketTestSnapshot(
                ticket.ID,
                ticket.CreatedByUserAccountID,
                ticket.TicketStatus.Name,
                ticket.AssignedToUserAccountID,
                ticket.IsDeleted,
                ticket.CancelledDate,
                ticket.CancelledReason))
            .SingleAsync();
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
    }
}

public sealed record TicketTestSnapshot(
    int ID,
    int CreatedByUserAccountID,
    string StatusName,
    int? AssignedToUserAccountID,
    bool IsDeleted,
    DateTime? CancelledDate,
    string? CancelledReason);

public sealed class ThrowingPasswordResetService
    : IPasswordResetService
{
    public Task RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
            "Sensitive internal test detail.");
    }

    public Task<PasswordResetServiceResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
            "Sensitive internal test detail.");
    }
}

public sealed record SentPasswordResetEmail(
    string RecipientEmail,
    string RecipientName,
    string ResetUrl);

public sealed class FakePasswordResetEmailSender
    : IPasswordResetEmailSender
{
    private readonly List<SentPasswordResetEmail> _messages = [];
    private readonly object _sync = new();

    public Exception? ExceptionToThrow { get; set; }

    public IReadOnlyList<SentPasswordResetEmail> Messages
    {
        get
        {
            lock (_sync)
            {
                return _messages.ToArray();
            }
        }
    }

    public Task SendPasswordResetEmailAsync(
        string recipientEmail,
        string recipientName,
        string resetUrl,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        lock (_sync)
        {
            _messages.Add(
                new SentPasswordResetEmail(
                    recipientEmail,
                    recipientName,
                    resetUrl));
        }

        return Task.CompletedTask;
    }
}
