using System.Security.Claims;
using HomeDaemon.Api.Auth;
using HomeDaemon.Api.Endpoints;
using HomeDaemon.Application.Common;
using HomeDaemon.DataAccess.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// Sign-in, sign-out and the authenticated account's own operations. Identity cookies carry the
/// session; no token is ever handed to JavaScript.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapGet("/csrf", GetCsrfToken).AllowAnonymous();
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", GetCurrentUserAsync);
        group.MapPost("/change-password", ChangePasswordAsync);

        return group;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.FindByEmailAsync(request.Email).ConfigureAwait(false);

        // An unknown, deactivated or locked-out account is reported the same way so the
        // endpoint does not reveal which accounts exist.
        if (user is null || !user.IsActive)
        {
            return AccountResponses.InvalidCredentials();
        }

        var result = await signInManager
            .PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return AccountResponses.InvalidCredentials();
        }

        var roles = await users.GetRolesAsync(user).ConfigureAwait(false);
        return Results.Ok(AccountResponses.ToCurrentUser(user, roles));
    }

    private static IResult GetCsrfToken(HttpContext httpContext, IAntiforgery antiforgery)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(antiforgery);

        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Ok(new CsrfTokenResponse(tokens.RequestToken ?? string.Empty));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync().ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> users)
    {
        var user = await users.GetUserAsync(principal).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("The current account no longer exists.");

        var roles = await users.GetRolesAsync(user).ConfigureAwait(false);
        return Results.Ok(AccountResponses.ToCurrentUser(user, roles));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.GetUserAsync(principal).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("The current account no longer exists.");

        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword).ConfigureAwait(false);
        if (!changed.Succeeded)
        {
            return AccountResponses.ValidationProblem(changed);
        }

        // Clearing the flag ends the mandatory-change state; the cookie is refreshed so the
        // client immediately sees a fully usable session.
        user.MustChangePassword = false;
        var updated = await users.UpdateAsync(user).ConfigureAwait(false);
        if (!updated.Succeeded)
        {
            return AccountResponses.ValidationProblem(updated);
        }

        await signInManager.RefreshSignInAsync(user).ConfigureAwait(false);
        return Results.NoContent();
    }
}
