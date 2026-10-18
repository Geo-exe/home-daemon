using Microsoft.AspNetCore.Identity;

namespace HomeDaemon.DataAccess.Persistence;

/// <summary>
/// Application login account. It is intentionally separate from the domain
/// <c>HouseholdMember</c>: a person may exist without ever signing in.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
}
