using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ResolveHub.Api.Data;
using ResolveHub.Api.Data.Seed;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Services.Implementations;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add controller-based API support.
builder.Services.AddControllers();

// Generate the OpenAPI document used by Swagger.
builder.Services.AddOpenApi();

// Read the SQL Server connection string from configuration/User Secrets.
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

// Read and validate JWT configuration.
var jwtSection =
    builder.Configuration.GetSection(JwtSettings.SectionName);

var jwtSettings =
    jwtSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "JWT configuration could not be loaded.");

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer))
{
    throw new InvalidOperationException(
        "JWT issuer is missing.");
}

if (string.IsNullOrWhiteSpace(jwtSettings.Audience))
{
    throw new InvalidOperationException(
        "JWT audience is missing.");
}

if (string.IsNullOrWhiteSpace(jwtSettings.Key))
{
    throw new InvalidOperationException(
        "JWT signing key is missing.");
}

if (jwtSettings.AccessTokenExpirationMinutes <= 0)
{
    throw new InvalidOperationException(
        "JWT expiration must be greater than zero minutes.");
}

byte[] jwtSigningKey;

try
{
    jwtSigningKey =
        Convert.FromBase64String(jwtSettings.Key);
}
catch (FormatException exception)
{
    throw new InvalidOperationException(
        "JWT signing key must be a valid Base64 value.",
        exception);
}

if (jwtSigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must contain at least 32 bytes.");
}

// Make JwtSettings available through IOptions<JwtSettings>.
builder.Services.Configure<JwtSettings>(jwtSection);

// Register JWT bearer authentication.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(jwtSigningKey),

                ValidateLifetime = true,
                RequireExpirationTime = true,

                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role,

                // Prevent the default five-minute expiration allowance.
                ClockSkew = TimeSpan.Zero
            };
    });

// Register the JWT token-generation service.
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Register role-based authorization services.
builder.Services.AddAuthorization();

var app = builder.Build();

// Development-only database seed and Swagger UI.
if (app.Environment.IsDevelopment())
{
    await DatabaseSeeder.SeedAsync(
        app.Services,
        app.Configuration);

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

// Identify the user from the JWT before checking authorization.
app.UseAuthentication();
app.UseAuthorization();

// Map controller routes.
app.MapControllers();

app.Run();