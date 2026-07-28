using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Resend;
using ResolveHub.Api.Constants;
using ResolveHub.Api.Data;
using ResolveHub.Api.Data.Seed;
using ResolveHub.Api.Entities;
using ResolveHub.Api.Infrastructure;
using ResolveHub.Api.Services.Implementations;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<
        BearerSecuritySchemeTransformer>();
});

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddDataProtection();

builder.Services
    .AddIdentityCore<UserAccount>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<Role>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services
    .AddOptions<FrontendSettings>()
    .Bind(
        builder.Configuration.GetSection(
            FrontendSettings.SectionName))
    .Validate(
        settings =>
            Uri.TryCreate(
                settings.BaseUrl,
                UriKind.Absolute,
                out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps) &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment),
        "Frontend:BaseUrl must be an absolute HTTP or HTTPS URL without a query string or fragment.")
    .ValidateOnStart();

builder.Services
    .AddOptions<PasswordResetSettings>()
    .Bind(
        builder.Configuration.GetSection(
            PasswordResetSettings.SectionName))
    .Validate(
        settings =>
            settings.TokenLifetimeMinutes is >= 5 and <= 1440,
        "PasswordReset:TokenLifetimeMinutes must be between 5 and 1440.")
    .ValidateOnStart();

builder.Services
    .AddOptions<FileStorageSettings>()
    .Bind(builder.Configuration.GetSection(FileStorageSettings.SectionName))
    .Validate(settings =>
        !string.IsNullOrWhiteSpace(settings.UploadRoot) &&
        settings.MaxFileSizeBytes > 0 &&
        settings.MaxFilesPerTicket is > 0 and <= 20,
        "FileStorage settings are invalid.")
    .ValidateOnStart();

var resendSection =
    builder.Configuration.GetSection(
        ResendSettings.SectionName);

var resendSettings =
    resendSection.Get<ResendSettings>()
    ?? new ResendSettings();

builder.Services
    .AddOptions<ResendSettings>()
    .Bind(
        resendSection)
    .Validate(
        settings =>
            !string.IsNullOrWhiteSpace(settings.ApiToken) &&
            !string.IsNullOrWhiteSpace(settings.FromEmail) &&
            !string.IsNullOrWhiteSpace(settings.FromName),
        "Resend requires an API token, sender email, and sender name.")
    .ValidateOnStart();

builder.Services.AddResend(options =>
{
    options.ApiToken = resendSettings.ApiToken;
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(
    options =>
    {
        var tokenLifetimeMinutes =
            builder.Configuration.GetValue<int>(
                "PasswordReset:TokenLifetimeMinutes");

        options.TokenLifespan =
            TimeSpan.FromMinutes(tokenLifetimeMinutes);
    });

var jwtSection =
    builder.Configuration.GetSection(JwtSettings.SectionName);

builder.Services
    .AddOptions<JwtSettings>()
    .Bind(jwtSection)
    .ValidateOnStart();

builder.Services.AddSingleton<
    IValidateOptions<JwtSettings>,
    JwtSettingsValidator>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(
        JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>(
        (options, jwtOptions) =>
        {
            var jwtSettings = jwtOptions.Value;
            var signingKey = new SymmetricSecurityKey(
                Convert.FromBase64String(jwtSettings.Key));

            options.MapInboundClaims = false;
            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.Zero
                };
        });

var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? [];

if (allowedOrigins.Length == 0 ||
    allowedOrigins.Any(string.IsNullOrWhiteSpace))
{
    throw new InvalidOperationException(
        "At least one valid CORS allowed origin must be configured.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        SecurityPolicyNames.FrontendCors,
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        TimeSpan? retryAfter = null;

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var retryAfterValue))
        {
            retryAfter = retryAfterValue;
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        retryAfterValue.TotalSeconds))
                .ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                message =
                    "Too many requests. Please try again later.",
                retryAfterSeconds = retryAfter is null
                    ? (int?)null
                    : Math.Max(
                        1,
                        (int)Math.Ceiling(
                            retryAfter.Value.TotalSeconds))
            },
            cancellationToken);
    };

    options.AddPolicy(
        SecurityPolicyNames.LoginRateLimit,
        httpContext =>
        {
            var clientIdentifier =
                httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown-client";

            return RateLimitPartition.GetFixedWindowLimiter(
                clientIdentifier,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    options.AddPolicy(
        SecurityPolicyNames.ForgotPasswordRateLimit,
        httpContext =>
        {
            var clientIdentifier =
                httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown-client";

            return RateLimitPartition.GetFixedWindowLimiter(
                clientIdentifier,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    options.AddPolicy(
        SecurityPolicyNames.ResetPasswordRateLimit,
        httpContext =>
        {
            var clientIdentifier =
                httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown-client";

            return RateLimitPartition.GetFixedWindowLimiter(
                clientIdentifier,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
});

builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<
    IPasswordResetEmailSender,
    ResendPasswordResetEmailSender>();
builder.Services.AddScoped<
    IPasswordResetService,
    PasswordResetService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ITicketAttachmentService, TicketAttachmentService>();
builder.Services.AddScoped<ITicketDraftService, TicketDraftService>();
builder.Services.AddScoped<IAgentTicketService, AgentTicketService>();
builder.Services.AddScoped<IAdminTicketService, AdminTicketService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IManagerTicketService, ManagerTicketService>();
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await DatabaseSeeder.SeedAsync(
        app.Services,
        app.Configuration,
        app.Environment);
}

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

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors(SecurityPolicyNames.FrontendCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ActiveUserMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;

internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var authenticationSchemes =
            await authenticationSchemeProvider
                .GetAllSchemesAsync();

        var bearerSchemeExists =
            authenticationSchemes.Any(
                scheme =>
                    scheme.Name ==
                    JwtBearerDefaults.AuthenticationScheme);

        if (!bearerSchemeExists)
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes =
            new Dictionary<string, IOpenApiSecurityScheme>
            {
                [JwtBearerDefaults.AuthenticationScheme] =
                    new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        In = ParameterLocation.Header,
                        BearerFormat = "JWT",
                        Description =
                            "Paste the JWT access token only."
                    }
            };

        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(
                    new OpenApiSecurityRequirement
                    {
                        [
                            new OpenApiSecuritySchemeReference(
                                JwtBearerDefaults.AuthenticationScheme,
                                document)
                        ] = []
                    });
            }
        }
    }
}
