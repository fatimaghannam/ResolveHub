using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.Entities;

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
            "60");
        builder.UseSetting(
            "Cors:AllowedOrigins:0",
            "https://localhost:5173");
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
        bool isActive = true)
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
            EmailConfirmed = true,
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
