using Microsoft.AspNetCore.Identity;
using ResolveHub.Api.Entities;

namespace ResolveHub.Api.Data.Seed;

// Development-only seed entry point for demonstration accounts and tickets.
public static class DemoDataSeeder
{
    public static Task SeedAsync(
        ApplicationDbContext dbContext,
        UserManager<UserAccount> userManager,
        IConfiguration configuration) =>
        DatabaseSeeder.SeedDemoDataAsync(
            dbContext,
            userManager,
            configuration);
}
