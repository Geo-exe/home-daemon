using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace HomeDaemon.Api.Auth;

/// <summary>
/// Validates the antiforgery token for cookie-authenticated requests that change state.
/// Requests authenticated with the Home Assistant API key carry no cookie and are exempt, as
/// are anonymous requests such as sign-in, which have no session to protect yet.
/// </summary>
public sealed class CsrfValidationMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace,
    };

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(antiforgery);

        if (!RequiresValidation(context))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Missing or invalid antiforgery token",
                status = StatusCodes.Status400BadRequest,
            }).ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool RequiresValidation(HttpContext context) =>
        !SafeMethods.Contains(context.Request.Method)
        && !context.Request.Headers.ContainsKey(ApiKeyOptions.HeaderName)
        && string.Equals(context.User.Identity?.AuthenticationType, IdentityConstants.ApplicationScheme, StringComparison.Ordinal);
}
