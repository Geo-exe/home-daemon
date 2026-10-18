using Microsoft.AspNetCore.Identity;

namespace HomeDaemon.DataAccess.Persistence;

/// <summary>
/// Application login account. It is intentionally separate from the domain
/// <c>HouseholdMember</c>: a person may exist without ever signing in. An account can be
/// linked to at most one household member, which is what lets a manual completion be
/// attributed both to the login and to the person.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    /// <summary>Optional link to the household member this account acts as.</summary>
    public Guid? HouseholdMemberId { get; set; }

    /// <summary>When set, the account must change its password before using anything else.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Deactivated accounts keep their history but can no longer sign in.</summary>
    public bool IsActive { get; set; } = true;
}
