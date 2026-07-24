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
