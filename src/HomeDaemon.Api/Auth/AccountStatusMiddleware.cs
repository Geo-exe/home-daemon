using HomeDaemon.DataAccess.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace HomeDaemon.Api.Auth;

/// <summary>
/// Enforces two account-level rules for cookie-authenticated API requests: a deactivated
/// account can no longer act, and an account flagged for a mandatory password change can only
/// reach the endpoints needed to change it. The rule lives here rather than in the client so
/// it cannot be bypassed.
/// </summary>
public sealed class AccountStatusMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> PasswordChangeAllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/auth/me",
        "/api/v1/auth/csrf",
        "/api/v1/auth/logout",
        "/api/v1/auth/change-password",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var users = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        if (users.GetUserId(context.User) is not string userId)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var user = await users.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "The account is not active").ConfigureAwait(false);
            return;
        }

        if (user.MustChangePassword && !PasswordChangeAllowedPaths.Contains(context.Request.Path))
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "A password change is required before continuing").ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            title,
            status = statusCode,
        }).ConfigureAwait(false);
    }
}
