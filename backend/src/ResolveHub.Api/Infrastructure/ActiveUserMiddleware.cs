using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Data;

namespace ResolveHub.Api.Infrastructure;

public sealed class ActiveUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ApplicationDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var isActive = int.TryParse(subject, out var userId) &&
                await dbContext.Users.AsNoTracking()
                    .AnyAsync(user => user.Id == userId && user.IsActive,
                        context.RequestAborted);

            if (!isActive)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    message =
                        "This account has been deactivated. Please contact your system administrator."
                }, context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}
