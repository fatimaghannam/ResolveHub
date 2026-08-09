// using statement tells C# that I want to use classes/functions from this namespace in this file
using System.Threading.RateLimiting; //Provides classes used to control how many requests a user can send 
using Microsoft.AspNetCore.Authentication; //Provides ASP.NET Core authentication functionality
using Microsoft.AspNetCore.Authentication.JwtBearer; //Provides JWT bearer authentication
using Microsoft.AspNetCore.Identity; //Provides user accounts, roles, Login and other identity features
using Microsoft.AspNetCore.OpenApi; //provides tools for generating and customizing the OpenAPI documentation
using Microsoft.AspNetCore.RateLimiting; //Provides ASP.NET Core rate limiting features
using Microsoft.Data.SqlClient; // provides SQL Server-specific classes and utilities
using Microsoft.EntityFrameworkCore; //Provides entity framework core for communicating with the database
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens; //provides classes used to create and validate JWT security tokens
using Microsoft.OpenApi; //provides OpenAPI classes used to describe the API in Swagger.
using Resend; //Provides the resend email service used for sending emails
using ResolveHub.Api.Constants; //gives access to constants definied inside the ResolveHub project
using ResolveHub.Api.Data; //gives access to the database context
using ResolveHub.Api.Data.Seed; //gives access to the code that adds initial data tp the database
using ResolveHub.Api.Entities; //Gives access to database entities such as UserAccount and Role
using ResolveHub.Api.Infrastructure; //
using ResolveHub.Api.Services.Implementations;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Settings; //gives access to confirmation setting classes such as JWT and frontend 

var builder = WebApplication.CreateBuilder(args); //created the ASP.NET Core application builder used to configure services and application settings. 

builder.Services.AddControllers(); //Add controllers allows ResolveHub to use API controllers
builder.Services.AddProblemDetails(); //Add a standard format for returning API error information
builder.Services.AddExceptionHandler<GlobalExceptionHandler>(); //handes unexpected backend errors

builder.Services.AddOpenApi(options => //Registers OpenAPI documentation and adds JWT authentication  information to it
{
    options.AddDocumentTransformer< //Adds a custom transformer that tells Swagger/OpenAPI about JWT authentication
        BearerSecuritySchemeTransformer>();
});

var connectionString = //Gets the SQL Server connection string from the application's configuration
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException( //Stops the application if the database connection string is missing 
        "Connection string 'DefaultConnection' was not found.");
//NOTE: ApplicationDbContext is the bridge between the C# backend and SQL Server (DbContext represents the database inside the C# application)
builder.Services.AddDbContext<ApplicationDbContext>(options => //Registers ResolveHub's Entity framework database context
{
    options.UseSqlServer(connectionString); //configures entity framework core to use SQL server 
});

builder.Services.AddDataProtection(); //data and tokens protection

builder.Services
    .AddIdentityCore<UserAccount>(options =>
    {
        options.User.RequireUniqueEmail = true; //unique email
        options.Password.RequiredLength = 8; 
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15); //protects ResolveHub from repeated password guessing

        options.SignIn.RequireConfirmedEmail = false; //Allows users to signin without requiring email confirmation => Currently, ResolveHub doesn't force the user to click a verification email before logging in.
    })
    .AddRoles<Role>() //enables role support using ResolveHub's Role entity 
    .AddEntityFrameworkStores<ApplicationDbContext>() //stores Identity users, roles, passwords, and related data using Entity Framework Core.
    .AddSignInManager() //Registers SignInManager for handling user sign-in operations (SignInManager helps ASP.NET Identity with authentication-related operations)
    .AddDefaultTokenProviders(); //default token providers  for example: forgot password-->reset token-->Reset Password

builder.Services
    .AddOptions<FrontendSettings>()
    .Bind(
        builder.Configuration.GetSection(
            FrontendSettings.SectionName))
    .Validate( //vaidating the frontend URL 
        settings =>
            Uri.TryCreate(
                settings.BaseUrl,
                UriKind.Absolute,
                out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps) && //only allows HTTP or HTTPS frontend URLs
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment),
        "Frontend:BaseUrl must be an absolute HTTP or HTTPS URL without a query string or fragment.")
    .ValidateOnStart(); //Validates the password reset settings when the backend starts
 
builder.Services //Password Reset settings
    .AddOptions<PasswordResetSettings>()
    .Bind(
        builder.Configuration.GetSection(
            PasswordResetSettings.SectionName))
    .Validate(
        settings =>
            settings.TokenLifetimeMinutes is >= 5 and <= 1440, //Ensures the reset token lifetime is between 5 minutes and 24 hours.
        "PasswordReset:TokenLifetimeMinutes must be between 5 and 1440.")
    .ValidateOnStart();

builder.Services //File storage settings
    .AddOptions<FileStorageSettings>() //Registers configuration settings used for ticket file attachments 
    .Bind(builder.Configuration.GetSection(FileStorageSettings.SectionName)) //Loads the file storage settings from configuration
    .Validate(settings =>
        !string.IsNullOrWhiteSpace(settings.UploadRoot) &&
        settings.MaxFileSizeBytes > 0 && //file size must be greater than zero
        settings.MaxFilesPerTicket is > 0 and <= 20, 
        "FileStorage settings are invalid.")
    .ValidateOnStart(); //checks everything when starting 

var resendSection = //gets the Resend email configuration section
    builder.Configuration.GetSection(
        ResendSettings.SectionName);

var resendSettings = //converts the Resend configuration into ResendSettings object
    resendSection.Get<ResendSettings>()
    ?? new ResendSettings(); //creates an empty settings object 

builder.Services //Validate Resend Settings
    .AddOptions<ResendSettings>()
    .Bind(
        resendSection)
    .Validate(
        settings =>
            !string.IsNullOrWhiteSpace(settings.ApiToken) && //requires a Resend API token
            !string.IsNullOrWhiteSpace(settings.FromEmail) && //requires the sender email address
            !string.IsNullOrWhiteSpace(settings.FromName), //requires sender name
        "Resend requires an API token, sender email, and sender name.")
    .ValidateOnStart(); //Checks those at startup

builder.Services.AddResend(options =>
{
    options.ApiToken = resendSettings.ApiToken;
});

builder.Services.Configure<DataProtectionTokenProviderOptions>( //Configures how long ASP.NET Identity security tokens remain valid
    options =>
    {
        var tokenLifetimeMinutes =
            builder.Configuration.GetValue<int>(
                "PasswordReset:TokenLifetimeMinutes"); //Reads the password reset token lifetime from configuration

        options.TokenLifespan = //sets the expiration time of password reset tokens
            TimeSpan.FromMinutes(tokenLifetimeMinutes); 
    });

var jwtSection =
    builder.Configuration.GetSection(JwtSettings.SectionName);
//Registers and Loads JWT settings from configuration 
builder.Services
    .AddOptions<JwtSettings>()
    .Bind(jwtSection)
    .ValidateOnStart(); //Validates the JWT settings when the application starts

builder.Services.AddSingleton< //Registers the custom validator that checks whether the JWT settings are valid 
    IValidateOptions<JwtSettings>,
    JwtSettingsValidator>();

builder.Services 
    .AddAuthentication(options => //configures authentication for the application
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme; //Uses JWT Bearer tokens as the default method for identifying users

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme; //Uses JWT Bearer authentication when access to a protected endpoint is challenged
    })
    .AddJwtBearer(); //Enables JWT Bearer authentication

builder.Services //configures how incoming JWT access tokens should be validated
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

var allowedOrigins = //Gets the frontend URLs that are allowed to use the backend 
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
//NOTE: CORS is a security mechanism that controls which websites are allowed to access your backend API 
builder.Services.AddCors(options => //Configures CORS for communication between frontend and backend .
{
    options.AddPolicy(
        SecurityPolicyNames.FrontendCors,
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod(); //Allows HTTP methods such as GET, POST, PUT and DELETE
        });
});

builder.Services.AddRateLimiter(options => //configures limits on how many requests a client can send 
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests; //Returns status code 429 when too many requests are sent

    options.OnRejected = async (context, cancellationToken) => //defines what happens when a request is blocked 
    {
        TimeSpan? retryAfter = null; //stores how long the client should wait before trying again

        if (context.Lease.TryGetMetadata( //checks if the rate limiter provides a retry time
                MetadataName.RetryAfter,
                out var retryAfterValue))
        {
            retryAfter = retryAfterValue; //saves the retry time
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

    options.AddPolicy( //Creates a rate limit for Login requests 
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
                    PermitLimit = 10, //Allow a maximum of 10 login requests
                    Window = TimeSpan.FromMinutes(1), //Limit reset every  1 minute 
                    QueueLimit = 0, //doesn't queue extra requests 
                    AutoReplenishment = true //Automatically resets the limit after the window ends
                });
        });

    options.AddPolicy(
        SecurityPolicyNames.ForgotPasswordRateLimit, //Creates a rate limit for forgot-password requests
        httpContext =>
        {
            var clientIdentifier =
                httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown-client";

            return RateLimitPartition.GetFixedWindowLimiter(
                clientIdentifier,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5, //Allows a maximum of 5 forgot-password requests
                    Window = TimeSpan.FromMinutes(15), //The limit reset every 15 minutes
                    QueueLimit = 0, //doesn't queue extra requests 
                    AutoReplenishment = true //Automatically resets the limit
                });
        });

    options.AddPolicy( //Creates a rate limit for password-reset requests 
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
                    PermitLimit = 10, //Allows a maximum of 10 reset password requests
                    Window = TimeSpan.FromMinutes(15), //The limit reset every 15 minutes
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
});
//Connects each interface to the service that implements it 
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<
    IPasswordResetEmailSender,
    ResendPasswordResetEmailSender>();
builder.Services.AddScoped<
    IPasswordResetService,
    PasswordResetService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ITicketActivityService, TicketActivityService>();
builder.Services.AddScoped<ITicketAttachmentService, TicketAttachmentService>();
builder.Services.AddScoped<ITicketDraftService, TicketDraftService>();
builder.Services.AddScoped<ITicketCommentService, TicketCommentService>();
builder.Services.AddScoped<IAgentTicketService, AgentTicketService>();
builder.Services.AddScoped<IAdminTicketService, AdminTicketService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<IAdminCategoryService, AdminCategoryService>();
builder.Services.AddScoped<ISystemAuditLogService, SystemAuditLogService>();
builder.Services.AddScoped<IManagerTicketService, ManagerTicketService>();
builder.Services.AddScoped<IAssignmentApprovalService, AssignmentApprovalService>();
builder.Services.AddScoped<ITicketCancellationRequestService, TicketCancellationRequestService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var databaseTarget = new SqlConnectionStringBuilder(connectionString); //Reads information from the database connection string 
    app.Logger.LogInformation(
        "ResolveHub database target: Server={DatabaseServer}; Database={DatabaseName}",
        databaseTarget.DataSource,
        databaseTarget.InitialCatalog);
}

if (!app.Environment.IsEnvironment("Testing"))
{
    await DatabaseSeeder.SeedAsync(
        app.Services,
        app.Configuration,
        app.Environment);
}

if (app.Environment.IsDevelopment()) //Enables Swagger/OpenAPI only during development 
{
    app.MapOpenApi(); 

    app.UseSwaggerUI(options => // Enables the Swagger interface for testing API endpoints
    {
        options.SwaggerEndpoint( //Tells Swagger where to find the ResolveHub API documentation 
            "/openapi/v1.json",
            "ResolveHub API v1");
    });
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors(SecurityPolicyNames.FrontendCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ActiveUserMiddleware>(); //checks if the Logged-in user's account is still alive
app.UseAuthorization();
app.MapControllers();//Connects API routes to their controllers

app.Run(); //Starts the backend application 

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
