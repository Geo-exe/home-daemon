using System.Security.Claims;
using HomeDaemon.Api.Auth;
using HomeDaemon.Application.Common;
using HomeDaemon.DataAccess.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// Account administration. Every route requires the administrator role. Accounts are never
/// deleted: deactivating keeps completion history and audit references intact.
/// </summary>
public static class AdminUserEndpoints
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/users")
            .WithTags("Admin")
            .RequireAuthorization(AccountRoles.AdminPolicy);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{userId:guid}", GetAsync);
        group.MapPut("/{userId:guid}", UpdateAsync);
        group.MapPost("/{userId:guid}/deactivate", DeactivateAsync);
        group.MapPost("/{userId:guid}/reactivate", ReactivateAsync);
        group.MapPost("/{userId:guid}/reset-password", ResetPasswordAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        string? cursor,
        int? pageSize,
        HomeDaemonDbContext db,
        CancellationToken cancellationToken)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var key = OpaqueCursor.Decode(cursor, 1)[0];
            query = query.Where(user => user.NormalizedEmail != null && user.NormalizedEmail.CompareTo(key) > 0);
        }

        var rows = await query
            .OrderBy(user => user.NormalizedEmail)
            .Take(size + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > size;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var roles = await RolesByUserAsync(db, rows, cancellationToken).ConfigureAwait(false);
        var items = rows
            .Select(user => AccountResponses.ToUser(user, RoleList(roles, user.Id)))
            .ToList();

        var nextCursor = hasMore
            ? OpaqueCursor.Encode(rows[^1].NormalizedEmail ?? rows[^1].Id.ToString())
            : null;

        return Results.Ok(new CursorPageDto<UserResponse>(items, nextCursor, hasMore));
    }

    private static async Task<IResult> CreateAsync(
        CreateUserRequest request,
        HomeDaemonDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await AdminBootstrapper.EnsureRolesAsync(roleManager).ConfigureAwait(false);
        await ValidateMemberLinkAsync(db, request.HouseholdMemberId, excludingUserId: null, cancellationToken).ConfigureAwait(false);

        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = NormaliseDisplayName(request.DisplayName),
            HouseholdMemberId = request.HouseholdMemberId,
            MustChangePassword = true,
            IsActive = true,
        };

        var password = TemporaryPasswordGenerator.Create();
        var created = await users.CreateAsync(user, password).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return AccountResponses.ValidationProblem(created);
        }

        var role = request.IsAdmin ? AccountRoles.Admin : AccountRoles.User;
        var assigned = await users.AddToRoleAsync(user, role).ConfigureAwait(false);
        if (!assigned.Succeeded)
        {
            return AccountResponses.ValidationProblem(assigned);
        }

        var response = AccountResponses.ToUser(user, [role]);
        return Results.Created($"/api/v1/admin/users/{user.Id}", new TemporaryPasswordResponse(response, password));
    }

    private static async Task<IResult> GetAsync(
        Guid userId,
        HomeDaemonDbContext db,
        UserManager<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var user = await FindAsync(db, userId, cancellationToken).ConfigureAwait(false);
        var roles = await users.GetRolesAsync(user).ConfigureAwait(false);
        return Results.Ok(AccountResponses.ToUser(user, roles));
    }

    private static async Task<IResult> UpdateAsync(
        Guid userId,
        UpdateUserRequest request,
        ClaimsPrincipal principal,
        HomeDaemonDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await FindAsync(db, userId, cancellationToken).ConfigureAwait(false);
        var isSelf = string.Equals(users.GetUserId(principal), user.Id.ToString(), StringComparison.Ordinal);
        var roles = await users.GetRolesAsync(user).ConfigureAwait(false);
        var wasAdmin = roles.Contains(AccountRoles.Admin, StringComparer.Ordinal);

        if (isSelf && wasAdmin && !request.IsAdmin)
        {
            throw new EntityConflictException("You cannot remove the administrator role from your own account.");
        }

        await AdminBootstrapper.EnsureRolesAsync(roleManager).ConfigureAwait(false);
        await ValidateMemberLinkAsync(db, request.HouseholdMemberId, excludingUserId: user.Id, cancellationToken).ConfigureAwait(false);

        var email = request.Email.Trim();
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await users.SetEmailAsync(user, email).ConfigureAwait(false);
            if (!emailResult.Succeeded)
            {
                return AccountResponses.ValidationProblem(emailResult);
            }

            var nameResult = await users.SetUserNameAsync(user, email).ConfigureAwait(false);
            if (!nameResult.Succeeded)
            {
                return AccountResponses.ValidationProblem(nameResult);
            }
        }

        user.DisplayName = NormaliseDisplayName(request.DisplayName);
        user.HouseholdMemberId = request.HouseholdMemberId;

        var updated = await users.UpdateAsync(user).ConfigureAwait(false);
        if (!updated.Succeeded)
        {
            return AccountResponses.ValidationProblem(updated);
        }

        if (request.IsAdmin && !wasAdmin)
        {
            await users.AddToRoleAsync(user, AccountRoles.Admin).ConfigureAwait(false);
        }
        else if (!request.IsAdmin && wasAdmin)
        {
            await users.RemoveFromRoleAsync(user, AccountRoles.Admin).ConfigureAwait(false);
        }

        var currentRoles = await users.GetRolesAsync(user).ConfigureAwait(false);
        return Results.Ok(AccountResponses.ToUser(user, currentRoles));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid userId,
        ClaimsPrincipal principal,
        HomeDaemonDbContext db,
        UserManager<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var user = await FindAsync(db, userId, cancellationToken).ConfigureAwait(false);

        if (string.Equals(users.GetUserId(principal), user.Id.ToString(), StringComparison.Ordinal))
        {
            throw new EntityConflictException("You cannot deactivate your own account.");
        }

        user.IsActive = false;
        await users.UpdateAsync(user).ConfigureAwait(false);

        var roles = await users.GetRolesAsync(user).ConfigureAwait(false);
        return Results.Ok(AccountResponses.ToUser(user, roles));
    }

    private static async Task<IResult> ReactivateAsync(
        Guid userId,
        HomeDaemonDbContext db,
        UserManager<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var user = await FindAsync(db, userId, cancellationToken).ConfigureAwait(false);

        user.IsActive = true;
        await users.UpdateAsync(user).ConfigureAwait(false);

        var roles = await users.GetRolesAsync(user).ConfigureAwait(false);
        return Results.Ok(AccountResponses.ToUser(user, roles));
    }

    private static async Task<IResult> ResetPasswordAsync(
        Guid userId,
        HomeDaemonDbContext db,
        UserManager<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        var user = await FindAsync(db, userId, cancellationToken).ConfigureAwait(false);

        var password = TemporaryPasswordGenerator.Create();
        var removed = await users.RemovePasswordAsync(user).ConfigureAwait(false);
        if (!removed.Succeeded)
        {
            return AccountResponses.ValidationProblem(removed);
        }

        var added = await users.AddPasswordAsync(user, password).ConfigureAwait(false);
        if (!added.Succeeded)
        {
            return AccountResponses.ValidationProblem(added);
        }

        user.MustChangePassword = true;
        await users.UpdateAsync(user).ConfigureAwait(false);

        var roles = await users.GetRolesAsync(user).ConfigureAwait(false);
        return Results.Ok(new TemporaryPasswordResponse(AccountResponses.ToUser(user, roles), password));
    }

    private static async Task<ApplicationUser> FindAsync(HomeDaemonDbContext db, Guid userId, CancellationToken cancellationToken) =>
        await db.Users
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new EntityNotFoundException($"Account '{userId}' was not found.");

    private static async Task ValidateMemberLinkAsync(
        HomeDaemonDbContext db,
        Guid? householdMemberId,
        Guid? excludingUserId,
        CancellationToken cancellationToken)
    {
        if (householdMemberId is not Guid memberId)
        {
            return;
        }

        var memberExists = await db.HouseholdMembers
            .AsNoTracking()
            .AnyAsync(member => member.Id == memberId, cancellationToken)
            .ConfigureAwait(false);

        if (!memberExists)
        {
            throw new ApplicationValidationException($"Member '{memberId}' was not found.");
        }

        var linked = db.Users.AsNoTracking().Where(user => user.HouseholdMemberId == memberId);
        if (excludingUserId is Guid excluded)
        {
            linked = linked.Where(user => user.Id != excluded);
        }

        if (await linked.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new EntityConflictException($"Member '{memberId}' is already linked to another account.");
        }
    }

    private static async Task<Dictionary<Guid, List<string>>> RolesByUserAsync(
        HomeDaemonDbContext db,
        List<ApplicationUser> rows,
        CancellationToken cancellationToken)
    {
        var userIds = rows.ConvertAll(user => user.Id);

        var pairs = await db.UserRoles
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(db.Roles, userRole => userRole.RoleId, role => role.Id, (userRole, role) => new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var roles = new Dictionary<Guid, List<string>>();
        foreach (var pair in pairs)
        {
            if (!roles.TryGetValue(pair.UserId, out var list))
            {
                list = [];
                roles[pair.UserId] = list;
            }

            if (pair.Name is not null)
            {
                list.Add(pair.Name);
            }
        }

        return roles;
    }

    private static List<string> RoleList(Dictionary<Guid, List<string>> roles, Guid userId) =>
        roles.TryGetValue(userId, out var list) ? list : [];

    private static string? NormaliseDisplayName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
