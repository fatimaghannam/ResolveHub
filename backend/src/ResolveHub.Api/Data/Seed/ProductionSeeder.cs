using Microsoft.AspNetCore.Identity;
using ResolveHub.Api.Entities;

namespace ResolveHub.Api.Data.Seed;

public static class ProductionSeeder
{
    public static Task SeedAsync(
        ApplicationDbContext dbContext,
        RoleManager<Role> roleManager) =>
        DatabaseSeeder.SeedProductionDataAsync(dbContext, roleManager);
}
