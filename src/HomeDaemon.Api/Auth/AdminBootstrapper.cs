using HomeDaemon.Application.Common;
using HomeDaemon.DataAccess.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Api.Auth;

/// <summary>
/// Creates the very first administrator. It is not exposed over HTTP on purpose: it runs as a
/// one-off command against the local database so a fresh instance can never be claimed through
/// a public registration endpoint.
/// </summary>
public static class AdminBootstrapper
{
    public const string EmailVariable = "HOMEDAEMON_ADMIN_EMAIL";
    public const string PasswordVariable = "HOMEDAEMON_ADMIN_PASSWORD";

    /// <summary>Creates the first administrator unless one already exists.</summary>
    public static async Task<AdminBootstrapResult> CreateFirstAdminAsync(
        IServiceProvider services,
        string email,
        string password)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ApplicationValidationException("An administrator email is required.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ApplicationValidationException("An administrator password is required.");
        }

        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureRolesAsync(roleManager).ConfigureAwait(false);

        var existing = await users.GetUsersInRoleAsync(AccountRoles.Admin).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            return AdminBootstrapResult.AlreadyInitialised;
        }

        var user = new ApplicationUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            DisplayName = "Administrator",
            MustChangePassword = true,
            IsActive = true,
        };

        var created = await users.CreateAsync(user, password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            throw new ApplicationValidationException(DescribeErrors(created));
        }

        var role = await users.AddToRoleAsync(user, AccountRoles.Admin).ConfigureAwait(false);
        if (!role.Succeeded)
        {
            throw new ApplicationValidationException(DescribeErrors(role));
        }

        return new AdminBootstrapResult(true, user.Id, user.Email);
    }

    /// <summary>Makes sure the two built-in roles exist; safe to call more than once.</summary>
    public static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        ArgumentNullException.ThrowIfNull(roleManager);

        foreach (var role in new[] { AccountRoles.Admin, AccountRoles.User })
        {
            var exists = await roleManager.RoleExistsAsync(role).ConfigureAwait(false);
            if (exists)
            {
                continue;
            }

            var created = await roleManager.CreateAsync(new IdentityRole<Guid>(role)).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                throw new ApplicationValidationException(DescribeErrors(created));
            }
        }
    }

    /// <summary>Applies pending migrations so the command works on a fresh database.</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeDaemonDbContext>();
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string DescribeErrors(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));
}
