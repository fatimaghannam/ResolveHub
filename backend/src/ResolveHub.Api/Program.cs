using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Data;
using ResolveHub.Api.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add controller-based API support.
builder.Services.AddControllers();

// Generate the OpenAPI document used by Swagger.
builder.Services.AddOpenApi();

// Read the SQL Server connection string from User Secrets.
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

// Register Entity Framework Core and SQL Server.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Required by Identity's default token providers.
builder.Services.AddDataProtection();

// Register ASP.NET Core Identity.
builder.Services
    .AddIdentityCore<UserAccount>(options =>
    {
        // User configuration.
        options.User.RequireUniqueEmail = true;

        // Password requirements.
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;

        // Account lockout configuration.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        // Email confirmation is not required for this assignment.
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<Role>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Register authorization services.
builder.Services.AddAuthorization();

var app = builder.Build();

// Enable OpenAPI and Swagger only during development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "ResolveHub API v1");
    });
}

// Redirect HTTP requests to HTTPS.
app.UseHttpsRedirection();

// Authentication middleware will be added when JWT is configured.
app.UseAuthorization();

// Map controller routes.
app.MapControllers();

app.Run();